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
