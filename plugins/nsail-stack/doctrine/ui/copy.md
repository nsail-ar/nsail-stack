# Spanish copy

The word a string uses for a thing, so the next string follows it. Voice is voseo (`abrí`,
`pegá`, `sos`); the words are neutral Spanish, never River Plate slang.

- **Slang out.** `plata` → `dinero`; `chequear` → `revisar`; no `guita`, `laburo`, `un toque`,
  `dale`. Plain Spanish stays (`ficha`, `de una [cosa]`). A word that reads as slang or a typo
  is said by what it means. The banned list is `Slang` in
  `test/Stack/Localization/NSail.Localization.Tests/SpanishCopyTests.cs`, which fails the build
  on any `strings.es.json` under `src`; add a word there, nowhere else.
- **One word per concept.** Money in general is *dinero*; a received payment is *cobro*; an
  amount on a document is *importe*; a payment request is *Link de pago*; a customer's balance is
  *cuenta corriente*. A new string picks the pair's word.
- **Short.** Say the fact and the next step; no filler, no doubled clause. A string keeps its
  placeholders, its entity capitalization (*Editar Rol*) and its key. English shortens the same
  way.
- **Assistant profiles** are model prompts, not UI copy: slang out, text otherwise untouched.
