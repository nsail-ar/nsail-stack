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
- **Short.** Say the fact and the next step; no filler, no doubled clause. A string keeps its
  placeholders, its entity capitalization (*Editar Rol*) and its key. English shortens the same
  way.
- **Assistant profiles** are model prompts, not UI copy: slang out of their own voice, text
  otherwise untouched. A line that glosses the user's slang for the model stays, which is why the
  sweep skips `*.Assistant.Profile` keys.
