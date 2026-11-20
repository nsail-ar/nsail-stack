# Spanish copy

The word a string uses for a thing, so the next string follows it. Voice is voseo (`abrí`,
`pegá`, `sos`); the words are neutral Spanish, never River Plate slang.

- **Slang out.** `plata` → `dinero`; `chequear` → `revisar`. Plain Spanish stays (`ficha`,
  `de una [cosa]`). A word that reads as slang or a typo is said by what it means. The banned
  list is `SpanishCopy.Slang` in the `NSail.Localization.Testing` package; the Stack's
  Localization tests and a product's Architecture suite both fail the build on any
  `strings.es.json` that carries one, inflections included (`pibes`, `bárbara`, `chequealo`).
  An entry is the word with its bounded endings (`pib[ea]s?`), never an open stem, and the match
  is whole-word with accents folded, so `plataforma` and `manguera` pass. A word, or an exception
  for a legitimate use (a silver colour, La Plata), is added there, nowhere else.
- **One word per concept.** Money in general is *dinero*; a received payment is *cobro*; an
  amount on a document is *importe*; a payment request is *Link de pago*; a customer's balance is
  *cuenta corriente*. A new string picks the pair's word.
- **Short and formal.** A string says the fact and, when there is one, the next step. Nothing else.
  - **One sentence, one idea.** A second idea is a second sentence; a third is a hint or a doc.
    No aside between dashes or parentheses, no clause that restates the screen, no "so that
    you…" tour of the mechanism.
  - **Impersonal, declarative.** *Un mensaje fuera de horario espera a la próxima apertura.* Not
    the story of what the system does and why. Voseo stays for the imperative of an act
    (*Abrí*, *Pegá*), never for chat.
  - **Cut by half, then check nothing true was lost.** If a rewrite is not clearly shorter and
    equally exact, the original stays. A string with a number, a limit or a consequence keeps it.
  - **A label is a noun or an act, not a sentence**: *Abrir (80 mm)*, not *Vista previa (rollo
    80 mm)*. Units in the house form (`80 mm`, a space).
  - **Explanations are hints.** A field's explanation shows behind its `?` icon, not as a
    paragraph in the form's flow. A hint keeps the same rules and rarely passes two sentences.
  - A string keeps its placeholders, its entity capitalization (*Editar Rol*) and its key.
    English shortens the same way, and a rewrite changes both catalogs in one commit.

  Worked example. Before: *Un mensaje que vence fuera de este horario espera a la próxima
  apertura. El que no serviría después de su hora —el link de una sesión— se adelanta al último
  momento dentro del horario, y sale en el acto si ya no queda ninguno: nunca se pierde.* After:
  *Un mensaje fuera de horario espera a la próxima apertura. Si es un link de sesión y vence
  antes, se envía en el último momento disponible o de inmediato, para que no se pierda.*
- **Assistant profiles** are model prompts, not UI copy: slang out of their own voice, text
  otherwise untouched. A line that glosses the user's slang for the model stays, which is why the
  sweep skips `*.Assistant.Profile` keys.
