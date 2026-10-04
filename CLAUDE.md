# Roadmap — agent notes

## Deploying (do NOT skip)

This app is deployed to **Railway**, which since 2026-10-02 **builds the root `Dockerfile` from the
GitHub repo `harutyun-shmavonyan/Roadmap`, branch `master`** (project `66371d0b-1c97-49f4-90b8-1f33106deb75`,
service `9271cb87-e63a-45e9-aa3c-74324561f4d3`, environment `43921fe0-f892-4f9e-97bd-bd3fd9b6e67f`).
Before that it pulled the image `h317280/roadmap-app:latest`, which needed a Docker daemon the
remote Claude sessions do not have; the switch was made so a deploy is a `git push`.

**To ship a change to `backend/` or `frontend/`: land it on `master` and push.** A push to a feature
branch does not deploy. Railway starts the build on the push (multi-stage image: `npm run build`,
`dotnet publish`), typically a few minutes; the Railway MCP `list-deployments` / `get-logs` tools
follow it, and `get-deployment-diagnosis` explains a failed one.

- **Never trigger a deploy yourself** (the user's call, 2026-10-04): no Railway `redeploy`, no
  `connect-service-source` to kick a build, no other deploy action. Push to `master` and tell the
  user; if Railway does not pick the push up, say so and leave the deploy to them. (On 2026-10-04
  pushes were not starting builds, and re-attaching the source was used to force them — don't.)
- **Verify, do not assume.** `https://roadmap-app.up.railway.app/mcp` needs no secret, so
  `tools/list` over JSON-RPC is the cheapest proof of what is actually running — look for a tool the
  change added. On 2026-09-26 an image push looked done while production was still a day-old build.
- Every push to `master` rebuilds, docs included (`*.md` is outside the image via `.dockerignore`, so
  a docs-only push is a wasted but harmless build). Keep docs changes in the same push as code when
  you can.
- The build ships the **committed tree**, not the working tree: uncommitted edits never deploy.
- Rollback: the Railway MCP `redeploy` tool with a previous `deploymentId`, or revert on `master`.
- The service's variables (`AUTH_PASSWORD`, `DATABASE_URL`, `JWT_SECRET`) are untouched by the
  source switch; `PORT` is injected by Railway.
- The `build and push image` workflow (`.github/workflows/deploy-image.yml`, from the Stock Signals
  branch) still builds and pushes `h317280/roadmap-app` on GitHub's runners, but Railway no longer
  pulls that image — it is a spare artifact, not the deploy path.

## Jobs tab — application tracking

Each posting carries an application outcome the user sets from the card:
`ApplicationStatus` (none/applied/screening/interviewing/offer/rejected/ghosted,
free text in the column so the vocabulary can grow without a migration),
`AppliedAt`, `RespondedAt` and `ApplicationNotes`. Written via
`PATCH /api/job-runs/postings/{id}/application` — PATCH semantics, so only the
fields present in the body change and an empty string clears one.

**`import_job_run` carries these across a replace, keyed by URL.** The tool
replaces the whole day, and the Finder scout re-imports a date whenever it
re-scores; without the carry-over every "applied" the user had recorded would be
silently erased. The tool's result reports `applications_carried` so a re-import
that dropped them would be visible rather than quiet. URL is the only identity
stable between two imports of the same posting — if that ever changes, this
carry-over has to change with it.

This is the pipeline's only feedback loop. Finder can measure how many postings
it produced but not whether any of them converted, so a supply problem, a
staleness problem and a CV problem are otherwise indistinguishable.

## Weighted sprint scoring

A Weighted sprint moves points between items each day; planned points never move. Each morning
(from history up to the previous day) every committed item is **overdone**, **underdone** or **on
plan**, measured as `(done − planned so far) ÷ max(planned so far, one week of its plan)`.
Overdone items give up that fraction of their points (at most 70%, so ×0.3 is the floor) into a
bank; underdone items planned that day split it by `u × planned points today` (u = shortfall,
saturating at 70%), each at most ×(1 + 2u), so ×3 is the ceiling. On-plan items stay at ×1. Only
what underdone items can absorb is ever taken. The day is re-settled on every log, and a boost is
paid only out of penalties actually paid that day.

The rules this was designed against, all enforced in `ComputeWeightedPricingAsync`:
- Doing exactly the day's plan earns exactly the day's planned points.
- **Delaying never pays.** Any bonus for being behind that is not funded by same-day penalties
  lets "skip it, catch up later at a higher rate" beat doing it on time — the two earlier schemes
  (budget re-split by due/done, then by 1.25^sessions-behind) both had this hole.
- Coefficients stay in 0.3..3 and move continuously; equal shortfalls get equal coefficients.

Everything is recomputed from the logs on every request, so any log — on any day, from the app or
MCP — moves every *later* day's coefficients; the day view re-fetches after each of its writes.
**A day's own coefficients never move because of that day's logs** (the user's call): they come
from history up to the previous day. Today's logs only decide how much of them is paid — a boost
is paid in full once enough penalty has been paid in. An overdone item logged off its planned day
still pays its penalty into the bank, but that only funds the shares fixed that morning. The schedule response carries `itemPrices` (every committed item's coefficient, scheduled
that day or not), which "Log to any item" uses to badge and preview the weighted points. Custom
achievements are deliberately left alone: they store fixed points at the nominal rate and never
touch an item, so they bypass weighting, progress and the bank — the user chose to keep them so.

**Relax days follow the sprint, even after Start** (the user's call). The commitment is otherwise
frozen at Start Sprint, but a relax day added or removed while the sprint runs takes its sessions
out of (or back into) the commitment — Performance, the week view and the weighted pricing — just
as the day view always did. A running sprint reads `Sprint.RelaxDays` live in
`ComputeCommitmentAsync`; `ReplanStartedSprintsAsync` copies them into the frozen snapshot, so an
ended sprint keeps the relax days it actually had and a toggle on a closed sprint changes nothing.

## Notes v2 — flashcards (FSRS), a system of its own

A separate feature beside the v1 daily notes: its own tables (`flashcards`, `flashcard_prompts`,
`flashcard_reviews`), its own tab (**Notes v2**), REST (`/api/flashcards`) and MCP tools
(`create_flashcard`, `get_due_flashcard_prompts`, `record_flashcard_review`, …). **Nothing is shared
with `notes`** — no foreign key, no shared logic — so either system can be killed later without
touching the other. During the trial the skills write both: `add-daily-note` saves the v1 note *and*
the v2 card, and `interval-repeat-quiz` asks which engine to run.

The model: a **flashcard is one note** (one top-level bullet with its subpoints) in one book
(red/green). The card is the entity; its **date is a property and a filter** ("all cards of
2026-09-30"), not an identity — a day holds any number of cards and adding a note always creates a
new card. What is *scheduled* is the card's **prompts**: one question/answer pair per fact, **as many
as the subpoints need** (`MaxPromptsPerCard` is a sanity limit of 50), each with its own review
history. One grade per fact is the whole point. Scheduler `Fsrs` (FSRS-4.5, default parameters, pure
functions like `Sm2`); rules in `FlashcardLogic`, shared by REST and MCP so they cannot drift on
what "due" means. `split_flashcard` turns one card into several, moving each prompt with its
schedule and history — that is how the migrated day-cards became one card per note.

The decisions, each simulated before being adopted:
- **Writing the card is the first exposure.** A new prompt starts in the state a Good first rating
  gives (stability ≈ 4 days, due in 4 days). Intake is never limited — the user's call — and nothing
  new ever spends a slot of the cap. Asking first exposures ahead of reviews was simulated and held
  *fewer* memories, because it starved the reviews of things already learned.
- **Red and green are two separate worlds** (the user's call). The tab chooses the book first and
  every view, session, stat, dashboard and cap belongs to that book; no REST endpoint or MCP tool
  aggregates across books (`book` is required on every set-level read).
- **The cap is 25 recorded questions per day per book** (`FlashcardLogic.DailyCap`, Asia/Yerevan day;
  red and green each have their own). Reviews are never capped per prompt. At steady state each carried prompt costs
  about 7 questions a day at 0.9 retention, so the cap carries ≈ 3.6 new prompts a day; the tab
  shows that number so a growing backlog is visible rather than silent.
- **Triage inside the cap:** yesterday's lapses first, then due prompts by predicted recall,
  highest first. A prompt at 85% is cheap to reinforce and leaps to a long interval; one at 30% is
  mostly gone and costs the same to relearn next week.
- **Reviewed overflow below 50% predicted recall is parked** (`State = Parked`), not deleted, and
  pulled back in on a day with spare slots. Overflow above it simply stays due. **Never-answered
  prompts are never parked** — a backlog of first answers stays visibly due and shrinks under the cap. Parking happens only in a
  real session (`get_due_flashcard_prompts`, `POST /api/flashcards/session`); `GET …/session` is a
  read-only preview.
- **A backdated card is due when it would have been.** `backfill=true` (the default for a card dated
  before today) makes the card's own date the exposure — so a fact still recalled after months leaps
  to a long interval on its first pass, and a lost one comes back tomorrow — and its first review is
  that date + 4 days, i.e. today for anything older. (Until 2026-10-02 backfill was spread 10 a day
  from +4; the user found nothing due and asked for the whole backlog now — migration
  `FlashcardsDueFromExposure` re-dated every never-answered prompt the same way.)
- **Grades are again / hard / good / easy.** In chat the quiz skill grades; in the tab the learner
  self-grades. "O" from the user means "I know this perfectly" and is recorded as easy. Lapses come
  back tomorrow flagged `Relearning`; the eighth lapse suspends the prompt as a leech
  (`update_flashcard_prompt reset=true` after a rewrite).
- **Desired retention 0.9.** Lowering it buys almost no capacity (≈ 7.2 → 6.4 questions per carried
  prompt at 0.8), so the cap is the only knob. `TrueRetention30d` in the stats should hover near
  0.9; drifting away means the parameters want re-fitting from `flashcard_reviews`.
- Same EF caveat as `VocabStore.ApplyReview`: the review row is returned unattached and added by
  the caller, never through the navigation.

**Seeding (2026-10-02).** Migration `FlashcardsV2` *copied* every v1 note-day into a card (new ids)
and moved the 544 prompts written that day onto them; migration `FlashcardPerNote` dropped the
per-day identity (`DayNumber`, one card per book+date); then every card holding several top-level
bullets was split with `split_flashcard` into one card per note, each prompt assigned to the note
it asks about. The copies are snapshots — a later edit to a v1 note does not reach v2, by design.

The skills live in `skills/` in this repo (`interval-repeat-quiz`, `add-daily-note`); the copies
Claude actually runs are synced from the user's account, so a change here has to be uploaded there.

## Nutrition tab — meal book and food log

Two views in one tab: the **meal book** (`meals` — what is worth eating, a cookbook) and the
**food log** (`food_log` — what was actually eaten, day by day, with each day's calories, protein,
carbs and fat). REST `/api/food-log`, MCP `log_food`, `list_food_log`, `update_food_log_entry`,
`delete_food_log_entry`; `FoodLogLogic` owns the rules for both.

- **An entry is a snapshot.** Logging from the meal book (`mealId`, or "🍴 I ate this" on a meal)
  copies the name and per-serving macros as they are now; a later edit to the meal never rewrites
  history, and deleting the meal only nulls `MealId` (FK `ON DELETE SET NULL`).
- **Macros are per serving** × `Servings` (fractions fine) — the same unit a meal stores, so changing
  the portion is one field. Doubles, one decimal.
- **Unknown is not zero.** A missing macro stays null; day totals add the known figures only and
  `incompleteEntries` says how many entries had a gap, so the tab marks that day's totals as a lower
  bound and hides its calorie split (protein/carbs 4 kcal/g, fat 9) rather than misstate it.
- Days with nothing logged are not returned. Slot defaults by the Asia/Yerevan hour
  (`SlotForNow`), or to the meal's own slot when logged from the book in the tab.
- REST PUT replaces an entry; MCP `update_food_log_entry` patches (null keeps, empty note clears).
- **Day by day charts** (`FoodLog.tsx`): four small multiples — calories, protein, carbs, fat — one
  bar per day, each on its own axis (kcal and grams never share one). A range control (7/14/30/90
  days) scopes the charts and the day cards alike; unlogged days are gaps, incomplete days faded
  bars (lower bounds); hover per day column; a table view carries every value. Colours are
  `--macro-*` tokens over the validated `--k*` palette (calories teal, protein red, carbs blue, fat
  amber) — red and amber fail the dark-surface floor side by side (ΔE 13), so they never touch in
  the split bar's protein | carbs | fat order.
- **Targets** (`nutrition_targets`, migration `NutritionTargets`): maintenance calories and optional
  protein/carbs/fat grams, each set **effective from a date** (unique) and holding until the next,
  so a changed maintenance never re-judges earlier days. `FoodLogLogic.TargetOn` picks the set for
  a day; every `FoodLogDayDto` carries its `target`. REST `GET/PUT /api/food-log/targets`,
  `DELETE /targets/{date}`; MCP `set_nutrition_targets`, `get_nutrition_targets` (0 or blank = no
  target). The charts draw them as a dashed step line, and the "on track" line above each chart
  averages value − target over logged days **with complete macros** (an incomplete day undercounts
  intake and would flatter a deficit) and, for calories, turns the net into kg at 7,700 kcal/kg.
- **7-day moving average** on every chart (solid 2px line over a surface halo; target lines are
  dashed; a key above the charts names both): the trailing mean of the day and the 6 before it over
  days logged **with complete macros** (unlogged days are not zeros, incomplete ones undercount),
  drawn only where at least 3 such days fall in the window (`MA_DAYS`, `MA_MIN_DAYS`). The view
  fetches 6 days of lead-in before the range so the line is whole from the first day shown. Also in
  the tooltip and the table view.

## Professional Newsletter tab

Agent-published HTML editions of professional news, read in the app. The newsletter agent
(`ai-engineering-daily` skill) drives it over MCP in three steps:

1. `get_newsletter_cursor` — where to resume. `since` is the moment the newest edition the user
   ticked **read** stopped scanning, so consecutive editions abut exactly instead of overlapping or
   leaving a gap. No read edition yet → the newest edition's end; empty tab → 7 days. Clamped to 14
   days (`cappedToMaxWindow` says when that bit).
2. the agent's own scan/score/render.
3. `publish_newsletter` — stores the edition as one self-contained HTML document.

**One edition per calendar date** (unique index on `IssueDate`): republishing a date replaces it and
marks it unread again, because the tick is what the cursor reads and ticking a shorter edition was
never a statement about the longer one. Editions older than 14 days are pruned on every publish —
this is a rolling window, not an archive, and `NewsletterLogic` owns both rules so the REST
endpoints and the MCP tools cannot drift.

The edition is rendered in a sandboxed iframe that auto-sizes to its content (same reason as the
Articles reader: the pane scrolls, not the frame), and "Open in new tab" hands it over as a blob so
it stays behind the app's auth.

## Experiences tab

Things planned and things done, with photos — one list split by `Status` (`planned` | `done`),
because an experience moves from one half to the other rather than being copied. Only the title is
required: a plan is worth saving before it has a place, a date or a picture.

- **Categories are free text, folded on write.** `ExperienceLogic.ResolveCategoryAsync` matches a
  new category case-insensitively against the ones in use and stores the existing spelling, so
  "travel" lands on "Travel" instead of beside it. Free text so the vocabulary can grow without a
  migration; folded so it does not drift. `list_experience_categories` exists for the agent to
  reuse rather than invent.
- **Tags are free text too, folded the same way** (`ExperienceLogic.ResolveTagsAsync`): a leading
  `#` is dropped, whitespace collapsed, case-duplicates removed, and each one spelled the way it
  already is elsewhere. Up to 20 of up to 40 characters — too many is refused, never truncated.
  `text[]` with a GIN index and an `'{}'` default (the default is what let the column be added to
  a table that already had rows). `list_experience_tags` is the reuse list.
- **REST PUT replaces, MCP `update_experience` patches.** Same split as the meal book: the tab's
  form always sends every field, an assistant rarely restates what it is not changing. In the MCP
  tool a null argument keeps the stored value and an empty string clears it.
- **Photos live in `experience_images`**, many per experience, bytes never loaded by a list query.
  The lowest `SortOrder` is the cover. They are fetched with the bearer token and shown from
  object URLs, like meal photos.
- **`ImageLogic` is the shared image code** — type sniffing, base64/data-URI decoding, and the
  guarded server-side URL download. Meals and experiences both go through it; `MealLogic` keeps
  its old method names as forwards.

## Stock Signals tab

The market screener (repo `Screener`) publishes **every** run here after the US close — the quiet days too,
because "nothing fired" is the message and the tab shows it as one. One `SignalRun` per trading day (unique
on `RunDate`); each `Signal` is one thing the screener proposed, keyed by the screener's own `EventId`
(`2026-10-01_A_us_semiconductors_t1`), which is the identity that holds across two publishes of a day.

Three layers live on a signal, written by three hands:

1. **The rule's case** — arrives with `publish_signal_run`: a headline in numbers, the evidence table (each
   metric against its threshold), the thesis in words, the horizon, the proposal (sizes are proposals for a
   human; nothing executes), the candidates, and what would prove it wrong. Deterministic, from the screener.
2. **The triage verdict** — `set_signal_triage`, written by the `screener-alert-triage` skill after it has
   read filings and news: the verdict JSON plus a phone-readable narrative. Makes the day unread again.
3. **The reader's decision** — status `new | reviewed | acted | dismissed` and notes, from the tab
   (`PATCH /api/signals/items/{id}`) or `update_signal`.

`publish_signal_run` replaces a date the way `import_job_run` does, and **carries layers two and three across
by `EventId`**; its result reports `carried`, so a re-publish that dropped a decision is visible rather than
quiet. A re-publish with the same set of event ids keeps the day's read tick; one that changes the set makes
the day unread. Quiet days older than 120 days are pruned on publish; days that fired are never pruned — they
are the audit trail of what was proposed and why. `SignalLogic` owns every rule so the REST endpoints
(`/api/signals`) and the MCP tools cannot drift. The JSON blobs (markets, watch, evidence, candidates,
proposal detail, triage) are stored as text and parsed on the way out; their keys are the screener's
snake_case, mirrored by the tab's types. Opening a day in the tab ticks it read.
