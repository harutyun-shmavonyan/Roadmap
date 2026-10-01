---
name: add-daily-note
description: "Add a single day's note to the user's Red book (professional/technical content) or Green book (general/personal learning), then extract 1-3 spaced-repetition prompts from it (Notes v2). Trigger whenever the user wants to record, log, save, or add a note for a day — phrases like \"add today's note\", \"remember this for today\", \"log this\", \"note this down\", \"add an entry\", \"save this\", \"I learned that...\". The skill decides between Red and Green book based on the content (technical/work → Red; general personal learning → Green) and defaults to today's date. Use this even if the user doesn't explicitly name a book."
---

# Add Daily Note

Adds a single day's entry to one of two notebook systems, now stored in the
**Roadmap app** (PostgreSQL) and accessed through its **MCP server** — not
Google Drive anymore — and then splits the new note into the atomic prompts
that the Notes v2 spaced-repetition quiz will ask later.

- **Red book** — professional/technical notes (software engineering, system design, work learning)
- **Green book** — general personal learning (linguistics, history, film, food, psychology, languages, trivia)

The defining behavior of the new storage: **one entry number per day.** The
first note on a given day creates a new numbered entry; any later note on the
same day appends to that same entry. The day_number never duplicates a date.

## Tool contract (Roadmap MCP)

Load the Roadmap tools via `tool_search` (query: "roadmap add note") if they
aren't already available, then use:

- `add_note(book, content, date?)` → `{ day_number, entry_date, action }`
  - `book`: `"red"` or `"green"`
  - `content`: the note body to store — one bullet per line, each as `- {text}`
  - `date`: optional ISO `YYYY-MM-DD`. **Omit** to default to today in Asia/Yerevan.
  - Behavior: upsert by `(book, entry_date)`. If no entry exists for that
    book+date, it creates one with `day_number = max + 1`. If one already
    exists, it appends `content` to that entry and returns the existing number.
  - `action` is `"created"` or `"appended"`.
- `create_note_prompts(book, number, prompts)` → the created prompts and their
  due dates. `prompts` is a list of `{ question, answer }`. Writing the note
  counts as the first exposure, so each prompt is already scheduled: **first
  review in about 4 days**, nothing is asked today. Hard ceiling 5 prompts per
  note; duplicates of an existing question on the note are refused.
- `list_note_prompts(book, number)` → the prompts a note already has. Call it
  before `create_note_prompts` when `action` was `"appended"`, so a fact the
  morning's note already covered is not asked twice.

There is no number to look up beforehand and no memory cache to maintain — the
database assigns and tracks the number. Do **not** read or write any
`... book max entry: N` memory lines; that mechanism is retired.

## When to trigger

- "Add today's note: <content>"
- "Remember this for today: <content>"
- "I just learned that <fact>. Save it."
- "Log this: <content>" / "Note this down" / "Add an entry about <topic>"
- "Add a Green/Red book entry for <date>"

Do **not** trigger for: batch transcription from photos (separate workflow),
searching past entries, or editing a specific existing entry's wording.

## Step 1 — Classify: Red or Green

Decide from the content:

**Red book** — programming, code, languages/frameworks (C#, .NET, Python, JS…),
databases/SQL, system design, distributed systems, concurrency/async,
OS/networking/infra/DevOps, algorithms, software engineering practice — anything
that reads as work/professional/technical learning.

**Green book** — languages/linguistics/etymology, history/geography/culture/
mythology, film/music/art/literature, food/wine/cooking, psychology/philosophy/
sociology, hobby automotive, science-as-curiosity, trivia.

**Overrides:**
- If the user names a book explicitly, honor it and skip content classification.
- If genuinely ambiguous after reading the content, ask briefly with `ask_user_input_v0`.

## Step 2 — Build the content

- Each note is **one top-level bullet**. Any supporting detail goes in
  **nested subpoints** indented two spaces beneath it — never as sibling
  top-level bullets.
  - Top level: `- {text}`
  - Subpoint: `  - {text}` (two leading spaces, then hyphen-space)
- If the user dictates wording, preserve it **exactly** — don't paraphrase or
  "clean up". If the user gives only a topic, compose a concise note yourself,
  still as a single top-level bullet with subpoints for the details.

**Keep it short.** A note is a memory hook, not a summary of the conversation.
The default is the smallest version that would still make sense in six months.

- **Max 3 subpoints.** If a topic seems to need more, either it's two separate
  notes or the extra points aren't earning their place. Four is an exception
  needing a reason, not a target.
- **One line per subpoint** — a single fact. Don't stack clauses with semicolons
  or dashes to smuggle in extra facts.
- **Numbers, dates and proper nouns only when they carry weight** — i.e. when
  they explain *why* something happened or fix an order that would otherwise be
  wrong. Founding years, exact ABVs, percentages, town names and founders' names
  are usually decoration; "19th century", "recent", "low-alcohol" is enough.
  Arbitrary numbers become quiz questions that can't be answered and shouldn't
  have been asked.
- **Prefer the non-obvious fact.** Keep what would be hard to reconstruct or
  what corrects a wrong assumption; drop what's easy to look up or already
  implied by the topic name.
- When the conversation was long, resist transcribing it. Ask what the user
  would actually want to be reminded of, and write only that.

- Adding a second, distinct note on the same day appends **another** top-level
  bullet (with its own subpoints) to that day's entry.
- Do **not** add a `# Day N — date` header. The number and date are stored as
  columns, not in the body.
- Mixed languages (Russian, English, Armenian) are normal — keep as written.

Example:

    - Cosine similarity — similarity between two vectors via the cosine of the angle between them; orientation, not magnitude.
      - Formula: cos(θ) = (A·B) / (||A|| · ||B||).
      - Range -1 to 1; 0 to 1 for non-negative vectors.
      - Magnitude-independent — good when length shouldn't matter.

## Step 3 — Determine the date

- **Default:** omit the `date` argument — the tool stamps today (Asia/Yerevan).
- If the user specifies a date ("for yesterday", "for 15.06.2026"), convert it
  to ISO `YYYY-MM-DD` and pass it as `date`.

## Step 4 — Call add_note

Call `add_note(book, content, date?)` and read back `day_number`, `entry_date`,
and `action`.

## Step 5 — Extract the prompts (Notes v2)

Every note goes straight into spaced repetition; there is no daily limit on new
notes or prompts — the quiz caps *questions* per day, not intake. From the
content you just added (only the new bullet, not the whole day when
`action` was `"appended"`), write **1–3 prompts** and call
`create_note_prompts(book, day_number, prompts)`.

Prompt rules — these decide whether the schedule can do its job:

- **One fact per prompt.** A specific cue with one specific answer. The quiz
  grades each prompt on its own, so the parts the user knows must not be glued
  to the parts they don't.
- **The question carries no hint.** Never quote the answer, half the answer, or
  the note's own wording. Ask about the fact, not about the note ("What does a
  NOT NULL column added to a populated table require?", never "What did I note
  about NOT NULL columns?").
- **Ask for the mechanism when the mechanism is the point.** "Why…", "What
  happens if…", "When would you use…" beats "What is the name of…" whenever the
  note is about how something works.
- **Answerable without the note.** The user will see only the question, in a
  mixed queue, days later.
- **The answer is checkable.** A short statement of the fact, with acceptable
  alternative phrasings in parentheses when the wording varies. Not a paragraph.
- **Skip decoration.** No prompts for arbitrary numbers, dates or names unless
  the note exists for them (the same rule as subpoints above).
- **Language:** question in English by default; the answer in the language the
  note uses. Mixed is fine.
- **Zero is a valid count.** A plan, a reflection, a mood, a to-do — nothing to
  recall, so create no prompts and say so in the confirmation.
- **Appended note:** `list_note_prompts` first; don't repeat a fact already
  covered. The note holds at most 5 prompts in total.

Example, for the cosine-similarity note above:

    [
      { "question": "What does cosine similarity measure between two vectors, and what does it ignore?",
        "answer": "The cosine of the angle between them — orientation; it ignores magnitude" },
      { "question": "What is the range of cosine similarity, and what does it narrow to for non-negative vectors?",
        "answer": "-1 to 1; 0 to 1 for non-negative vectors" }
    ]

Three subpoints, two prompts: the formula is skipped because it is easy to look
up and the first prompt already carries the idea.

## Step 6 — Confirm

One or two lines, reflecting whether it was new or appended and how many
prompts it got:

- created → "Added Green book entry 240 for 20.06.2026 · 2 prompts, first review in 4 days."
- appended → "Appended to Green book entry 240 (20.06.2026) — same day, same number · 1 prompt added."
- no prompts → "Added Red book entry 241 for 21.06.2026 · no prompts (nothing to recall)."

Show the prompt questions (not the answers) only if the user asks or if you
chose zero when the note looked factual. Don't narrate the classification
reasoning unless asked.

## Edge cases

- **Second+ note on the same day**: appends to that day's existing entry and
  keeps its number. This is the intended behavior, not a conflict.
- **User overrides classification**: honor it.
- **Mixed content** (substantial technical *and* general): ask which book.
- **User dictates over multiple messages**: gather the full content first, then
  make a single `add_note` call and a single `create_note_prompts` call.
- **User says "no prompts" / "don't quiz me on this"**: skip Step 5 and say so.
- **`create_note_prompts` refuses** (ceiling of 5, duplicate question): tell the
  user in one line; the note itself is already saved.
- **Backend not yet deployed**: if the Roadmap notes tools aren't found via
  `tool_search`, tell the user the notes feature hasn't shipped to the Roadmap
  MCP server yet rather than falling back to Drive. If only `create_note_prompts`
  is missing, save the note and say prompts will be backfilled by the quiz.

## Formatting conventions

- One top-level bullet per note: `- ` (hyphen-space).
- Details as nested subpoints indented two spaces: `  - `.
- Multiple notes on the same day = multiple top-level bullets in one entry.
- Preserve dictated wording exactly; keep mixed languages as written.
