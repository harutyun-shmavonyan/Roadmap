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
