# Less Code

If code can be derived safely and clearly, it is not written by hand. Anything that can be
resolved by reflection, emitted, generated with Roslyn, or reused generically is resolved that
way. The goal is not "low code" — it is less handwritten code, less repetition, less ceremony,
more consistency (principles.md 3).

What NSail already generates — DI registration, endpoints, HTTP clients, service discovery:
[generation.md](generation.md).

---

## Preferred order

When solving repetitive code:

1. Reuse an existing generic solution
2. Resolve it by convention or metadata
3. Generate it with Roslyn
4. Emit it if runtime generation is the right fit
5. Use reflection if the tradeoff is acceptable
6. Handwrite it only if the above do not fit well — the fallback, not the default

Typical candidates, not to be written by hand again and again: endpoint mapping, handler wiring /
DI registration, HTTP client plumbing, parameter binding, transport adapters, repetitive UI
wrappers that follow a known pattern.

---

## Suggestion rule

Actively look for code that is unnecessarily handwritten. When a repeated pattern appears,
suggest a convention, a generated approach or a generic abstraction — but do **not** introduce a
generator or framework-level mechanism unless explicitly requested. Think "this should probably
be handled by the framework", not "I will implement a generator right now".

---

## When handwritten code is still correct

- the case is unique
- the pattern is not stable yet
- the generated solution would be harder to understand
- the abstraction would add more complexity than it removes
- the framework does not yet provide a good mechanism and the manual code is still small

Manual code is acceptable. Repeated manual code is the smell.

---

## Readability rule

Prefer compile-time certainty over repeated guesswork. Generated solutions must stay readable,
debuggable and aligned with handwritten style: less code with more clarity, never magic.
