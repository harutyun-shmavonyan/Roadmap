---
name: interval-repeat-quiz
description: "Run a spaced repetition quiz session on the user's notes (Red and Green books) from the Roadmap app. Two engines, and the skill ALWAYS asks which one to run first: v1 — the fixed schedule (entries 0, 1, 2, 7, 14, 30, 60, 180, 360 back from the newest, one question per bullet, no memory of results); v2 — adaptive FSRS over flashcards (one per day's note, one prompt per subpoint), 25 questions a day, every answer recorded so a forgotten prompt comes back tomorrow and a known one disappears for months. Answering just \"O\" means \"I know this perfectly\". Trigger on phrases like \"run interval repeat\", \"quiz me\", \"review session\", \"test me on X book\", \"spaced repetition\", \"let's review my notes\", \"time for review\"."
---

# Interval Repeat Quiz

Runs a spaced-repetition quiz on the user's knowledge books, stored in the
**Roadmap app** (PostgreSQL) and accessed through its **MCP server**. Two
engines live side by side while v2 is being proven; the user picks one at the
start of every session.

## Step 0 — Ask which version (every session)

Before anything else, ask one short question and wait for the answer:

> v1 (fixed intervals, whole notes) or v2 (adaptive, 25 questions/day)?

Use `ask_user_input_v0` with the two options if it is available, otherwise plain
text. Do **not** assume a default. Skip the question only when the trigger
already names a version ("run v2", "quiz me on v1", "adaptive review").

Then follow **Version 1** or **Version 2** below. The two never mix in one
session.

## The "O" rule (both versions)

A reply consisting solely of the letter **O** (upper or lower case, nothing
else) means **"I know this perfectly"**. The user does not have to type the
answer.

- v1: mark it ✓ with no confirmation text and move on.
- v2: record the grade **easy** with `answer: "O"` and `note: "declared known"`.

Never argue with an O and never reveal the answer after one. If a prompt the
user keeps marking O comes back anyway, the scheduler has already stretched its
interval as far as the model allows — say so once if asked, nothing more.

---

# Version 1 — fixed intervals

## Tool contract (Roadmap MCP)

Load the Roadmap tools via `tool_search` (query: "roadmap get notes") if not
already available, then use:

- `get_recent_notes(book, n)` → up to `n` most recent entries, **day_number
  descending** (so the first element is the current max). Each entry:
  `{ day_number, entry_date, content }`.
- `get_note(book, number)` → `{ day_number, entry_date, content }`, or empty if
  that number doesn't exist.

`book` is `"red"` or `"green"`. `entry_date` comes back as ISO `YYYY-MM-DD`.

Version 1 is **read-only** — it never writes notes and keeps no memory cache.

## The 9 intervals

Target entries are `max − offset` for each offset in:

```
[0, 1, 2, 7, 14, 30, 60, 180, 360]
```

So with a max of 240, targets are 240, 239, 238, 233, 226, 210, 180, 60, -120.
Any target `< 1` is skipped (not enough history yet). These are "entries back"
by number, not by calendar date.

## Workflow

### Step 1 — Pick the book

Default **Green**. Use **Red** only if the user names it ("quiz me on Red book",
"review my technical notes").

### Step 2 — Find the current max

Call `get_recent_notes(book, 1)`. The first (only) entry's `day_number` is the
max. If it returns nothing, the book is empty — say so and stop.

### Step 3 — Calculate target numbers

```
targets = [max - o for o in [0, 1, 2, 7, 14, 30, 60, 180, 360]]
```

Filter out any `target < 1`. De-duplicate.

### Step 4 — Fetch each entry

For each target, call `get_note(book, target)`. Collect `day_number`,
`entry_date`, and `content`. If a target returns empty (a gap where that number
was merged away or never written), **silently skip it** — the user doesn't need
to hear about gaps.

### Step 5 — Generate one quiz question per bullet

For each entry's content:

- Read the bulleted facts.
- Generate **one question for every substantive top-level bullet** — each a
  definition, mechanism, comparison, specific value, or "when to use" judgement.
  A bullet with meaningful sub-bullets either folds them into that one question,
  or, if the sub-bullets are independently quiz-worthy, gets one question each.
- Phrase each as a **direct, practical question**: "What does X do?", "When
  should you use Y?", "What's the difference between A and B?", "What happens
  if…?".
- **No hints.** Don't restate context, don't say "you wrote about…", don't leak
  fragments of the answer. Ask as if the topic is fresh.
- Within an entry, order questions by how likely the fact is to be **forgotten**
  (counter-intuitive details, exact numbers, edge cases first).
- Skip any bullet that's trivial, pure meta, or just restates an earlier bullet.
  Skip an empty / trivially short / all-meta entry entirely.

### Step 6 — Present the quiz

Default pacing is **one question at a time**.

Open the session with a plan line and the first question only:

```
**Quiz session — N entries, M questions**

**1/M · [DD.MM.YYYY]**
{question}
```

`N` is the number of entries that survived Step 5; `M` is the total number of
questions generated across all of them. Count both from the finished question
list before sending — never estimate, and never revise `M` mid-session.

Then, on each user reply, send exactly two things in one message: the grade for
the question just answered, then the next question.

```
✓ {one-line confirmation}      ← or ~ / ✗ per Step 7

**k/M · [DD.MM.YYYY]**
{question}
```

Show the entry date on each question. **Do not** show the source content, the
entry numbers, or any hints unless asked. Order entries from most recent
(offset 0) to oldest (offset 360), and questions within an entry by
forgettability; ask them in that order.

**Every entry fetched in Step 4 must appear in the session.** Never drop an
entry because the session is running long, because the entry is old, or because
it's short — only Step 5's explicit skip rules (empty / trivial / all-meta) may
remove one, and a skipped entry is skipped silently at Step 5, not quietly lost
here. Keep asking until question `M` has been asked; the count in the opening
line is a commitment.

**Batch mode on request.** If the user asks for all questions up front ("give me
the whole list", "all at once"), present them grouped under dated headings and
numbered 1..M continuously, then grade the lot in one pass on the next turn.

### Step 7 — Grade each answer

Grade against the source content:

- **Correct**: ✓ + one-line confirmation.
- **"O"**: ✓, no text (see the O rule).
- **Partial**: ~ + what's missing.
- **Wrong / blank**: ✗ + the correct answer, concisely.

Grade **every** question asked, including ones answered "idk", "skip" or left
blank — a blank is a ✗ with the answer supplied. Keep the grade to one or two
lines so it doesn't bury the next question.

For any answer the user contests, quote the relevant excerpt from that entry.

### Step 8 — Close the session

After grading the answer to question `M`, don't ask another. Close with:

- the tally: `**Score: X ✓, Y ~, Z ✗**`
- if a pattern is visible, one short line naming the topics worth revisiting.

No praise, no encouragement padding.

## Edge cases (v1)

- **Fewer than 9 targets** (max < 360): quiz on whatever exists. Don't apologise.
- **Missing target number**: silently skip.
- **Mixed-language entries**: ask in English by default; the user may answer in
  any language.
- **Harder/easier requested**: regenerate the affected questions at adjusted depth.
- **Custom interval set** ("only the 30- and 60-day entries"): honor it.
- **Different book mid-session**: start a fresh session for that book.
- **User answers several questions at once** (or asks "what's left?"): grade what
  they gave, then continue from the next unasked number.
- **User abandons mid-session** ("stop", "enough"): grade the last answer, close
  with the tally over the questions actually asked, and say how many were left.
- **Backend not yet deployed**: if the Roadmap notes tools aren't found via
  `tool_search`, tell the user the notes feature hasn't shipped to the Roadmap
  MCP server yet rather than falling back to Drive.

---

# Version 2 — adaptive (FSRS)

Notes v2 is a system of its own beside the v1 notes: each day's learning is a
**flashcard** (one per book per day, same content as the v1 note during the
trial), and each card carries **prompts** — one fact each, as many as its
subpoints need — written by the `add-daily-note` skill when the card is
created, or later through this skill's backfill. The server schedules every
prompt with FSRS: a success multiplies the days until it is asked again, a
failure brings it back tomorrow, and eight failures suspend it as a leech. The
skill never decides intervals; it asks what the server hands it and reports
how each answer went.

## Tool contract (Roadmap MCP)

Load via `tool_search` (query: "flashcard prompts due review") if not already
available.

- `get_due_flashcard_prompts(book?)` → today's queue, already triaged under the
  daily cap of **25 recorded questions** (shared by both books): yesterday's
  lapses first, then due prompts by predicted recall, highest first. Fields:
  `Date, DailyCap, AskedToday, Remaining, DueTotal, Returned, Overflow,
  ParkedNow, Unparked, ParkedTotal, CarryCapacityPerDay, Prompts[]`. Each prompt:
  `Id, FlashcardId, Book, DayNumber, EntryDate, Question, Answer, State,
  Stability, Retrievability, DueOn, Relearning, Lapses, Reviews`. Safe to call
  again the same day — it returns what is still due within the remaining budget.
- `record_flashcard_review(prompt_id, grade, answer?, note?)` — grade is
  `again | hard | good | easy`. Returns `status, stability_days, interval_days,
  due_on, relearning, lapses, leech, asked_today, remaining_today`.
- `update_flashcard_prompt(prompt_id, question?, answer?, state?, reset?)` —
  fix a prompt; `reset=true` restarts its schedule (for a rewritten leech).
- `list_flashcards(book?, limit?, without_prompts_only?)` → cards newest first
  with content and prompt counts; `without_prompts_only=true` is the backfill list.
- `get_flashcard(book, number)` → one card with its prompts.
- `add_flashcard_prompts(book, number, prompts)` — attach `{question, answer}`
  prompts to a card (backfill mode is automatic for a card older than today).
- `get_flashcard_stats()` → totals: cards, due today, asked/cap, parked,
  leeches, true retention over 30 days, carry capacity, due per day next week.

## Workflow

### Step 1 — Pull the queue

Call `get_due_flashcard_prompts()` for both books, or with `book` when the user
named one. Then:

- `Returned == 0` and `Remaining == 0` → "Daily cap of 25 reached — nothing more
  today." Stop (offer backfill, Step 6).
- `Returned == 0` and `DueTotal == 0` → "Nothing due today." Say when the next
  prompts come due if `get_flashcard_stats().UpcomingLoad` shows some, then offer
  backfill (Step 6). Stop.
- Otherwise continue with the returned prompts **in the order returned**. Do not
  re-sort, group by note, or drop any.

### Step 2 — Present

One question at a time. Open with a plan line and the first question:

```
**Review — N prompts** (K asked today, cap 25)

**1/N · Red · [DD.MM.YYYY]**
{Question}
```

`N` is `Returned`. The date is the prompt's `EntryDate`. Show nothing else from
the prompt: not the answer, not the stability, not the note content, not the
day number.

Then, on each user reply, send exactly two things in one message: the grade for
the question just answered (after recording it), then the next question.

```
✓ {one-line confirmation} → next in 15d

**k/N · Green · [DD.MM.YYYY]**
{Question}
```

**Batch mode on request** works as in v1: list all N questions numbered, grade
the lot on the next turn, recording each.

### Step 3 — Grade and record, every time

Compare the user's reply with the prompt's `Answer`:

| Reply | grade | Mark |
|---|---|---|
| "O" alone | `easy` | ✓✓ (no text) |
| Correct, instant, complete | `easy` | ✓✓ |
| Correct | `good` | ✓ |
| Right idea but a key piece missing or wrong, or visibly effortful / hedged | `hard` | ~ + what was missing |
| Wrong, blank, "idk", "skip" | `again` | ✗ + the correct answer, concisely |

Call `record_flashcard_review(prompt_id, grade, answer=<the user's reply,
verbatim>, note=<one line: why this grade>)` **before** writing the grade line,
and append the schedule from the response: `→ next in {interval_days}d` for a
pass, `→ back tomorrow` for a fail. Keep the whole grade to one or two lines.

If the response says `leech: true`: the prompt has now failed eight times and
is suspended. Say so in one line and propose a rewritten question/answer (the
usual cause is an ambiguous prompt). On yes, call
`update_flashcard_prompt(prompt_id, question, answer, reset=true)`.

A prompt the user wants to skip ("skip this one, not now") is **not** recorded —
it stays due. A blank or "idk" **is** recorded as `again`.

If the user contests a grade, quote the stored `Answer`; if they are right and
the prompt is wrong, fix it with `update_flashcard_prompt` and leave the recorded
grade alone.

### Step 4 — Re-ask the failures

After question `N`, re-ask each ✗ prompt once, in the same format, headed
`**again · Red · [date]**`. These re-asks are **not recorded** — they are a
confirmation so the session ends on a success; the real retest is tomorrow,
when the server puts the prompt first. Show the answer once more if it is
still wrong. Then close.

### Step 5 — Close

```
**Score: a ✓✓, b ✓, c ~, d ✗**
Asked K/25 today · M still due beyond the cap · P parked · L leeches
```

`K` = the last `asked_today`; `M` = `Overflow` from Step 1 (omit the clause
when 0); `P` = `ParkedTotal`; `L` from `get_flashcard_stats().Leeches` (omit
when 0). If `ParkedNow > 0`, add one line: "The cap was full — {ParkedNow}
prompts below 50% recall were parked; they come back on a lighter day." One
short line on a visible pattern is fine. No praise, no encouragement padding.

### Step 6 — Backfill (offer once per session)

Some cards have no prompts yet (every v1 note was copied into a card; most got
prompts in the 2026-10-02 migration, the rest were skipped as cue-only). At
the close — or when the queue was empty — call
`list_flashcards(without_prompts_only=true, limit=3)`. If `total > 0`:

> {total} cards have no prompts yet. Write prompts for the 3 newest?

On yes, for each returned card write its prompts by the rules below — one per
subpoint worth recalling — show the questions (not the answers), and call
`add_flashcard_prompts(book, day_number, prompts)`. Backfill mode is automatic
for a card older than today: its own date counts as the exposure and first
reviews are spread ten a day, so even a large backfill never floods one date.
Three cards per session is the default; honor a request for more.

## Prompt rules (for backfill and rewrites)

- **One fact per prompt, one prompt per subpoint worth recalling** — as many
  as the card needs, a specific cue with one specific answer.
- **No hint in the question** — not the answer, not half of it, not the note's
  own wording. Ask about the fact, not about the note.
- **Mechanism over label** when the note is about how something works: "Why…",
  "What happens if…", "When would you use…".
- **Answerable without the note**, days later, in a mixed queue.
- **Checkable answer**: a short statement, alternative phrasings in parentheses.
- **Skip decoration** — arbitrary numbers, dates and names, unless the note
  exists for them.
- **Zero is fine** for a plan, a reflection or a mood: no prompts, say so.
- Question in English by default; the answer in the note's language.

## Edge cases (v2)

- **The user answers several at once** or asks "what's left?": grade and record
  what they gave, in order, then continue from the next unasked prompt.
- **The user abandons mid-session** ("stop", "enough"): record the last answer,
  close with the tally over what was asked and say how many were left; they stay
  due and nothing is lost.
- **`record_flashcard_review` errors**: tell the user in one line, keep the
  session going, and list the unrecorded prompts at the close.
- **A prompt is wrong or badly worded**: fix it with `update_flashcard_prompt` when
  the user points it out; add `reset=true` only when the rewrite changes what is
  being asked.
- **"Harder / easier"**: v2 questions are fixed text; offer to rewrite the
  specific prompt instead of improvising a different question.
- **The user wants more than 25**: the cap is the server's; say so and offer v1
  for an extra, unrecorded round.
- **Backend not yet deployed**: if the `get_due_flashcard_prompts` tool isn't found
  via `tool_search`, say v2 hasn't shipped to the Roadmap MCP server yet and
  offer v1.

## Notes

- A v2 session is done once the last returned prompt is graded, the failures
  re-asked and the tally is out, unless the user asks for more.
- One question per message is the default — resist the pull to dump the
  remaining questions into one giant message, even late in a session.
- Record before you grade. The grade line should quote the server's interval,
  never an estimate.
