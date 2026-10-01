# Roadmap — agent notes

## Deploying (do NOT skip)

This app is deployed to **Railway** from the Docker image
**`h317280/roadmap-app:latest`**. Railway pulls that image — it does **not** build from git.

**After ANY change to `backend/` or `frontend/`, you MUST run, from the repo root:**

```bash
docker build -t h317280/roadmap-app:latest .
docker push h317280/roadmap-app:latest
```

- A `git push` alone does **not** deploy. The image push is what ships the change.
- **The image push alone does not deploy either** — Railway does not watch the tag. After pushing,
  trigger a redeploy (dashboard, or the Railway MCP `redeploy` tool with project
  `66371d0b-1c97-49f4-90b8-1f33106deb75`, service `9271cb87-e63a-45e9-aa3c-74324561f4d3`,
  environment `43921fe0-f892-4f9e-97bd-bd3fd9b6e67f`); it re-pulls `:latest`. It takes ~15 s.
- **Verify, do not assume.** `https://roadmap-app.up.railway.app/mcp` needs no secret, so
  `tools/list` over JSON-RPC is the cheapest proof of what is actually running. On 2026-09-26 an
  image push looked done while production was still a day-old build.
- **No Docker on the machine?** The `build and push image` workflow (`.github/workflows/deploy-image.yml`) does the
  same build and push on GitHub's runners: automatically on every push to `main` that touches `backend/`,
  `frontend/` or the `Dockerfile`, or by hand (Actions → run workflow, pick the branch). It needs the repository
  secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` (a hub.docker.com access token with read/write). It only pushes
  the image; the Railway redeploy is still a separate step.
- Docs-only changes (this file, `README.md`) are not in the image, so they don't need a rebuild.
- The Docker build compiles the frontend (`npm run build`) and backend (`dotnet publish`) inside
  the image, so a clean working tree isn't required — but note the build ships the **entire
  working tree**, including any unrelated uncommitted changes.

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
