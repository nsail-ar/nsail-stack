# Skills

How a module contributes a playbook to the Assistant — the same contribution shape the menu
and the guide use, read beside [navigation.md](navigation.md) and [guide.md](guide.md).

---

Abstraction in `NSail.Components` (Stack), beside the contributor seams it copies —
`INavMenuContributor`, `IGuideContributor` — with no kit of its own and no Assistant
dependency: Sales and Accounting contribute skills without referencing the kit that reads
them.

- **A skill is written for the model, once, in English, and is never shown to a person.** It
  is not a guide chapter, which is prose a reader opens — it is an instruction an agent
  follows: "to record an expense, use this template and these accounts." Who reads a skill is
  the Assistant kit (nsail#2429), never a screen.
- **Contribution**: a module implements `ISkillContributor` (`Task<IReadOnlyList<SkillItem>>
  GetSkills()`, `Task<string> GetBody(string name)`) and registers it with
  `services.AddSkills<Skills>()` in its `Add<Kit>Components` setup. The class is named by the
  contribution alone — `Skills`, at the Shared project root beside `Menu.cs` and `Guide.cs`
  (structure.md, naming.md).
- **`SkillItem`** carries `Name` (kebab-case, unique across every contributor), `Description`
  (one line, at most `MaxDescriptionLength` (160) characters — the cost of listing what is
  available), `Tools` (the message keys the skill's steps send, e.g.
  `Accounting.JournalEntries.CreateJournalEntry`) and `Weight` (`NavMenuItem`'s bands, ties
  keeping contribution order). `GetSkills()` never carries a body on the item.
- **The body is read only through `SkillItem.GetBody()`**, which forwards to the winning
  contributor's own `GetBody(name)` — a lister that only enumerates skills never reads or
  holds one. A kit's own declaration stays a few lines:

  ```csharp
  public sealed class Skills : ISkillContributor
  {
      public Task<IReadOnlyList<SkillItem>> GetSkills()
      {
          return Task.FromResult<IReadOnlyList<SkillItem>>([
              new SkillItem
              {
                  Name = "record-expense",
                  Description = "Record an expense against an account.",
                  Tools = ["Accounting.JournalEntries.CreateJournalEntry"],
                  Weight = 30
              }
          ]);
      }

      public Task<string> GetBody(string name)
      {
          return EmbeddedSkillBody.Read(GetType().Assembly, name);
      }
  }
  ```

- **The body is an embedded resource, read with no HTTP call** (`EmbeddedSkillBody.Read`,
  `GetManifestResourceStream` off the contributing assembly) — unlike a guide chapter's
  `wwwroot` page, which `GuideReader` fetches: a skill has to be readable from a background
  job or a handler as freely as from a web host. The markdown lives at `Skills/{name}.md`
  beside the kit's `Skills.cs`, shipped via `<EmbeddedResource Include="Skills/*.md" />`, and
  its length is bound by `MaxBodyLength` (4000 characters, the context a model's call pays).
- **Merge** (`SkillItem.Collect`): every contributor's skills in one list, ordered by Weight
  ascending. A later contributor naming a `Name` already seen **replaces the earlier skill
  whole** — Description, Tools and Weight all come from the winner, none of it merged field
  by field the way the menu's items are (navigation.md) — so an app overriding a kit's skill
  does not inherit a Tool list it never named. The replaced skill keeps its first-appearance
  slot; the override's own Weight still moves it like any other skill, the slot only breaks a
  tie among skills that land on the same Weight.
- **Lazy by construction**: `Collect` never calls `GetBody` on anything it gathers — gathering
  every contributor's skills to list them costs nothing past their `Description`s. The item
  resolves its body through the contributor that won the merge, stamped on `Collect`.
- **Registration is scoped, the same lifetime `AddSkills<T>` gives the contributor** — a
  caller that holds a composed list past its own scope (a cached singleton, a background job
  that outlives the request it ran in) holds an item whose `GetBody()` reaches a disposed
  contributor. Collect once per scope, the way a guide's chapters are collected once per
  request.
- **Guard** (`SkillItem.Verify(skills, registry)`): over a host's composed contributors, every
  skill whose `Tools` names a key the host's `MessageRegistry` cannot resolve (a message
  renamed or removed out from under it), and every skill whose `Description` or body is empty
  or past its bound, named by skill and key in one list a build's own test asserts empty.
  Reading every body is the cost of this call — a lister never pays it, a guard test pays it
  once. A kit wires its own test against its real contributors and registry once it has
  skills to check (nsail#2431, nsail#2432).
