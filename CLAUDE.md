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
- Railway auto-redeploys once the new `:latest` is pushed (~1 min). Verify by hard-refreshing
  the deployed app.
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
