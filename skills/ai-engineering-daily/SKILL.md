---
name: ai-engineering-daily
description: "Build Harutyun's daily AI Engineering newsletter: scan all configured sources (including practical agent tips), LLM-score every candidate out of 100, and render the best of each of eight categories with a Top 10 overall as one HTML page. Use for the morning brief, \"run my AI newsletter\", or the scheduled daily task."
---

# AI Engineering Daily

Produce a scored HTML newsletter of what's new in AI engineering, in eight categories with a Top 10 overall on top. Run it as four phases: **scan → score and categorize → render → deliver**.

## Reader profile

Senior backend engineer, ~10 years. C#/.NET and Python, distributed systems, AWS data infrastructure (Kinesis, Firehose, Iceberg, ClickHouse, Athena, Kafka) at ~1TB/day, AdTech. AWS SA Associate. Building AI engineering skills: MCP servers, RAG, agents, evals, local-first inference (Ollama, Qdrant, Lucene.NET, ONNX Runtime, Langfuse). Goal: transition into AI Systems Engineering. Also wants **practical tips for using agents** — coding agents and everyday agents that make life easier — that he can try himself.

Use this profile to **rank and select**, never to editorialize. Tone is neutral reporting — do not write "this matters for your project" lines.

## Phase 1 — Scan (parallel)

Window: the last 24 hours for a daily run; widen to 7 days for a first run or a Monday edition covering the weekend.

Spawn **nine subagents in a single message** so they run concurrently. Each returns a flat list, each item as: `TITLE | URL | DATE | SOURCE | 2-3 sentences of concrete factual substance`. Instruct every agent: do NOT score or rank, do NOT editorialize, never invent a URL, and flag any item whose date or content could not be verified.

1. **Labs & IDE changelogs** — `raw.githubusercontent.com/anthropics/claude-code/main/CHANGELOG.md` (be thorough; highest-value source), `platform.claude.com/docs/en/release-notes/overview`, the Claude Code docs changelog (`code.claude.com/docs/en/changelog`), `claude.dev/blog`, `anthropic.com/news`, `anthropic.com/engineering`, OpenAI blog + API changelog, Codex changelog, Gemini/DeepMind dev announcements, Cursor, GitHub Copilot, VS Code release notes, Windsurf/Zed. **Fallback:** `openai.com/news` usually returns 403 — when it does, run `WebSearch` with `allowed_domains: ["openai.com"]` for the window's dates instead of reporting the source as unreachable.
2. **OSS frameworks & infra** — MCP spec/SDKs/registry, LangChain/LangGraph, LlamaIndex, DSPy, Pydantic AI, Haystack; vLLM, llama.cpp, Ollama, SGLang, ONNX Runtime; Qdrant, pgvector, LanceDB, Weaviate, Chroma, Lucene/Elasticsearch vector work, plus the turbopuffer and LanceDB blogs; Langfuse, Braintrust, Arize Phoenix, Ragas, OTel GenAI conventions; LiteLLM and OpenRouter changelogs; the MCP blog (`blog.modelcontextprotocol.io`), the Hugging Face blog, Cloudflare's developer changelog (`developers.cloudflare.com/changelog`), and Earendil/Pi (`earendil.com/posts`); **.NET AI stack** (Microsoft.Extensions.AI, Semantic Kernel, MCP C# SDK, Microsoft Agent Framework, ML.NET, the .NET blog at `devblogs.microsoft.com/dotnet` and the Semantic Kernel blog at `devblogs.microsoft.com/semantic-kernel`); agent runtimes (E2B, Daytona, OpenAI Agents SDK, Temporal). **Fallback:** the GitHub API is often blocked (403) — read `/releases/latest` and `/releases/tag/<v>` as HTML, confirm dates on PyPI/npm/NuGet, and when those also fail use `WebSearch` with `allowed_domains: ["github.com"]`; do not report a GitHub-only project as unscanned until all three have been tried.
3. **Practitioner writing** — simonwillison.net (posts *and* link-blog), hamel.dev, eugeneyan.com, jxnl.co, Chip Huyen, Lilian Weng, Sebastian Raschka, Phil Schmid (philschmid.de), Vicki Boykis (vickiboykis.com), Latent Space, Interconnects, Anthropic Engineering, OpenAI Cookbook, and company engineering blogs shipping real LLM systems (Cloudflare, Netflix, Uber, Stripe, Shopify, Meta, LinkedIn, Vercel, Datadog, Netlify, Figma, Discord, Notion). Prioritise **tips** (concrete techniques) and **usage cases** (how a team shipped it, what broke).
4. **X/Twitter** — x.com cannot be fetched (robots.txt). Use `WebSearch` with `allowed_domains: ["x.com", "twitter.com"]`; tweet text arrives inside result titles. Run 12-15 separate searches across topics and handles: @AnthropicAI, @OpenAIDevs, @simonw, @swyx, @HamelHusain, @jxnlco, @eugeneyan, @omarsar0, @LangChainAI, @vllm_project, @ollama, @qdrant_engine. Also try threadreaderapp.com unrestricted. Want all four flavors: lab announcements, practitioner threads, trending discourse, and viral educational threads when genuinely substantive.
5. **HN & Reddit** — HN via the Algolia API (`hn.algolia.com/api/v1/search_by_date?tags=story&numericFilters=created_at_i>UNIX,points>80`), varying thresholds and query terms. Reddit is blocked; reach r/LocalLLaMA, r/ClaudeAI, r/MachineLearning and r/ExperiencedDevs via the Arctic Shift archive API or WebSearch scoped to reddit.com. Note when the comment thread is the valuable part.
6. **AWS & ClickHouse** — AWS What's New RSS (`aws.amazon.com/about-aws/whats-new/recent/feed/`), no service filter: AI/ML, his data stack, and core compute. ClickHouse blog release posts, `clickhouse.com/docs/whats-new/changelog` and the ClickHouse GitHub releases (`github.com/ClickHouse/ClickHouse/releases`, which also works when the docs changelog is bot-blocked), **features only**. Include adjacent data-infra news: Apache Iceberg and Kafka release notes, the Confluent blog, the MotherDuck blog, DuckDB, Snowflake/Databricks engineering.
7. **Broad SE craft** — language/runtime releases (.NET, Python, Go, Rust, TS, JVM), databases and storage engines, distributed systems and architecture writing, public postmortems, developer tooling, staff/principal-level technical writing. Sources: InfoQ, Pragmatic Engineer, ACM Queue, Martin Fowler, Marc Brooker, Jepsen, Dan Luu, lobste.rs, company blogs.
8. **Trending & talks** — GitHub trending (see caveat below), HF trending models/Spaces, Latent Space, AI Engineer conference talks, MLOps Community, Software Engineering Daily. Summarize talk **substance** — he will not watch them.

9. **Agent tips & workflows** — practical ways people use agents, not news about them. Coding agents: workflows for Claude Code, Codex, Cursor and Copilot (prompting, CLAUDE.md/AGENTS.md and rules files, hooks, subagents, parallel sessions and worktrees, review and test loops, context management). Everyday agents: assistants with connectors (email, calendar, Drive), browser and computer-use agents, scheduled tasks and routines, personal automations people actually run. Sources: simonwillison.net, Ethan Mollick's One Useful Thing, Every's Chain of Thought, Harper Reed (harper.blog), Peter Steinberger (steipete.me), Anthropic and OpenAI best-practice docs and cookbooks (e.g. Claude Code best practices), r/ClaudeAI, r/ChatGPTPro, HN "Show HN" / "Ask HN" threads, and X threads from the Claude Code, Codex and Cursor teams (WebSearch scoped to x.com as in cluster 4). For each item return the **concrete step** — the exact command, prompt, config snippet or setup — alongside the usual fields; an item with no concrete step is not a tip. Skip generic "N prompts to 10x your productivity" listicles and sponsored tool roundups.

## Phase 2 — Score and categorize (do this yourself, never with code)

Dedupe across clusters first; the same story routinely arrives from three agents. Keep the best URL.

Score every surviving candidate **0-100 by judgment** on **one shared scale**. Never use keyword rules, regex, or a formula — the scoring is the product. Weigh:

- **Relevance** to AI engineering practice and to his stack (.NET/C#, Python, AWS data infra, ClickHouse, MCP, agents, evals, local-first inference).
- **Informativeness** — does reading it teach something concrete? Real numbers, breaking changes, named techniques and failure modes score high; announcements with no mechanism score low.
- **Novelty and consequence** — does it change how he'd build something?
- **Source credibility** — discount vendor-authored benchmarks; keep the methodology, discount the headline.

Bands: **90+** drop everything · **80s** high value · **70s** worth a scan · **60s** context. A breaking change in a tool he runs outranks an interesting essay. The bands mean the same in every category — a 92 tip and a 92 release are equally worth his time — because the Top 10 compares across categories.

**Agent tips are scored on their own criteria, on the same bands:** could he try it tomorrow in under an hour, and does it show the exact prompt, config or command? A concrete setup with a before/after scores high; a vague "AI changed my workflow" post scores low or is dropped.

**Categorize every story into exactly one category** (its primary one):

| key | Category | What goes in |
|---|---|---|
| `tips` | Agent tips & workflows | A way of *using* an agent — coding or everyday — with a concrete step to try |
| `coding` | Coding agents & IDEs | News about Claude Code, Codex, Cursor, Copilot, VS Code, Windsurf, Zed: releases, changelogs, features |
| `models` | Models & APIs | New models, API features, pricing and deprecations from Anthropic, OpenAI, Google and others; benchmarks, lightly |
| `agents` | Agents & MCP | MCP spec, SDKs, registry and servers; agent frameworks and runtimes (LangGraph, Agents SDK, Semantic Kernel, Microsoft Agent Framework, Temporal, E2B) |
| `rag` | RAG, search & evals | Vector databases, retrieval and search, evals and LLM observability (Qdrant, pgvector, LanceDB, Langfuse, Braintrust, Ragas, OTel GenAI) |
| `local` | Local inference | Ollama, vLLM, llama.cpp, SGLang, ONNX Runtime, quantization, serving |
| `data` | Data infrastructure | AWS (his stack and core compute), ClickHouse features, Kafka, Iceberg, DuckDB, Snowflake/Databricks engineering |
| `craft` | Engineering craft | Language and runtime releases (.NET, Python, Go, Rust, TS, JVM), databases, distributed systems, postmortems, staff-level writing |

The line between `tips` and `coding` is one question: **is it news about the tool, or a way of using it?** A Claude Code release or a new Cursor feature → `coding`; "running four Claude Code sessions in parallel with git worktrees" → `tips`. A practitioner write-up of how a team *built* an LLM system goes to the category of what they built (`rag`, `agents`, …), not `tips`.

**Flags**, on top of the category: `breaking` for a breaking change in a tool or service he runs (they are also listed in a strip under the Top 10), `dotnet` for anything in the .NET AI stack.

Hand **every** story scoring 60 or more to the renderer — do not cut to a count yourself. The renderer ranks each category by score, keeps at most 10 per category, and builds the Top 10 overall from all of them. Aim for **at least 5 tips** a day by scanning cluster 9 thoroughly; if fewer than 5 score 60+, publish what there is — never pad a category.

## Phase 3 — Render

Do not hand-write the HTML. Write three JSON files and run the renderer bundled with this skill, `scripts/render_edition.py` in this skill's folder (the folder holding this SKILL.md):

```
python3 <skill folder>/scripts/render_edition.py --items items.json --details details.json --meta meta.json --out ai-engineering-daily-YYYY-MM-DD.html
```

- **items.json** — a list, one object per story: `title` (the original if it has one, otherwise write one), `url`, `source`, `date` (YYYY-MM-DD), `score` (integer), `category` (a key from the table), `summary` (1-2 sentences: what it is and what changed), and then **either** `learn` (every category but tips: 1-2 sentences, the concrete takeaway from reading it) **or** `try` (tips only: the concrete step in 1-2 sentences) with an optional `try_code` (the exact command, prompt or config, verbatim, a few lines at most). Optional `flags`: `["breaking"]`, `["dotnet"]`.
- **details.json** — `{url: {"lead": ..., "digest": [paragraph, ...], "focus": ... (optional), "sections": [{"h": ..., "b": [...]}]}}`, produced as described under Details below.
- **meta.json** — `{"date": "YYYY-MM-DD", "covered_from": "YYYY-MM-DD", "covered_until": "<UTC ISO time the scan reached>", "sources": "<the sources actually scanned, one line>", "notes": ["<anything the reader should know: unreachable sources, fallbacks used>"]}`.

The script refuses invalid input (unknown category, a tip without `try`, a duplicate URL, …) and lists every problem; fix them and run it again. On success it prints a JSON summary: `stories` (the edition's `item_count`), stories per category, what was dropped below 60 or over the cap, the size in bytes, and `publish` — how to publish it (see Phase 4).

**What the page is.** The fixed design system approved on 2026-09-20 (cards with the score badge in a left column, Summary, the tinted "What you'll learn" block, the monospace URL, the source · date row with its Details button, the colour tokens and the score ramp, light/dark via `prefers-color-scheme` with a `[data-theme]` override, readable at 400px) is implemented in the script — change the script, not a one-off edition, to change it. On top of it the script adds:

1. **Filter chips** under the header — *All* and one per non-empty category, with counts. Choosing one shows only that category. CSS-only (radio inputs and sibling selectors), since the Roadmap frame may block scripts.
2. **Top 10 overall** — compact one-line rows (mini score badge, title, category) that jump to the full card in its category.
3. **⚠ Breaking changes** — the same compact rows for every story flagged `breaking`, only when there are any.
4. **The categories**, in the table's order, each a heading with its count and its stories ranked by score. Empty categories are left out.
5. On tip cards, a green **"Try it"** block (with the command or config in a monospace box) in place of "What you'll learn". `breaking` and `dotnet` show as small pills under the title.
6. The **footer**: scoring method, categories and counts, sources scanned, exclusions, the window, and how the Details were produced and capped, plus your `notes`.

**Details content.** Every card carries a Details button opening a CSS-only modal that sits between the 2-sentence Summary and the full piece. It holds, top to bottom:

1. **`lead`** — one sentence, ≤170 chars: what it is and what changed.
2. **`digest`** — the story retold, so he can take it instead of reading the piece: 1-3 paragraphs, 80-250 words (≤1,800 characters in all; the renderer refuses longer rather than cut it). Tell it in order — the problem or occasion, what was done or claimed, how it works or the reasoning, what came out of it, and the author's conclusion — with the concrete details that carry it (names, numbers, versions). Neutral reporting, no "this matters for you" lines; vendor or poster numbers get "(vendor)" / "(self-reported)". **When only part of a long piece matters** — one section of a release post, one segment of a talk, one finding in a long essay — summarize that part and name it in **`focus`** ("the section on prompt caching", "the Q&A at 41:00"); the modal then labels the digest "The part worth reading · <focus>" instead of "The story". A piece that is all one thing has no `focus`.
3. **`sections`** — the key facts as 3-7 bullets grouped under these headings only, in this order, using only those with content: **What changed, How it works, Numbers, Breaking changes, Caveats, Discussion**. Bullets are fragments ≤150 chars, no trailing period, no markdown; keep every concrete fact that fits (numbers, flags, API/class/endpoint names, versions, dates, PR numbers) and drop filler; quantitative claims from the vendor or the poster get "(vendor)" or "(self-reported)" appended; a page that could not be fetched gets a first Caveats bullet "Page not fully readable; written from <what was used>". Lead + bullets ≤ 850 characters — fewer, denser bullets beat cut facts (the renderer drops trailing bullets beyond that, Discussion first). The bullets should add facts the digest does not already state, not repeat it.

Produce them after scoring, for the stories that will be shown, in two passes: (1) ~4 reader subagents fetch each URL and **read the whole piece**, returning `{url: {"digest": [...], "focus": ... or null, "facts": "5-9 factual sentences"}}` — the digest written by the agent that actually read the page, never reconstructed from the summary; (2) ~4 restructuring subagents turn each `facts` paragraph into the `lead` and `sections` with the rules above, passing `digest` and `focus` through unchanged.

## Phase 4 — Deliver

Write to `/mnt/user-data/outputs/ai-engineering-daily-YYYY-MM-DD.html`.

**Publish to the Roadmap Newsletter tab** the way the renderer's `publish` says:

- `"single"` — the edition is under 100 KB: one `publish_newsletter` call with the file's contents as `html`, `stories` as `item_count`, and the window as `covered_from` / `covered_until`.
- **a list of part files** — the edition is larger than one tool call can carry (one call above ~135 KB has failed on the output limit; 180 KB failed, 135 KB passed). Publish it in parts, which has no size limit:
  1. `start_newsletter_upload` with the same metadata (`issue_date`, `title`, `covered_from`, `covered_until`, `item_count`) → `upload_id`.
  2. For each part file in order, `append_newsletter_part(upload_id, part=N, html=<that file's contents, verbatim>)`. Each is at most 80 KB; send them one per call, never merged. A part number sent again replaces that part, so on an error just send it again.
  3. `finish_newsletter_upload(upload_id, parts=<how many files>)`. It joins the parts and publishes them exactly as `publish_newsletter` would. If it reports a missing part, send that part and finish again — nothing is published until the finish succeeds, so a failure midway leaves yesterday's edition untouched.

If a Gmail connector is connected, email it to harut.shmavonyan@gmail.com with subject "AI Engineering Daily — <date>" and the top 3 headlines of the Top 10 and their scores at the top of the body. If Gmail is not connected or cannot send, say so plainly in the final message rather than silently skipping the email.

## Exclusions — drop these every time

- Research papers (entirely — tools, techniques and shipped systems only)
- Funding, VC, valuations, acquisitions, market-cap talk
- AGI and policy discourse: safety debates, regulation, timelines, future-of-work op-eds
- AWS regional expansions ("now available in X region") and routine SageMaker JumpStart / Bedrock model-listing announcements
- ClickHouse patch and LTS version bumps with no feature content
- Engagement bait with no technical substance
- Generic prompt listicles ("50 ChatGPT prompts…") and sponsored tool roundups, in any category

Consumer AI product news and benchmark news are **allowed**, lightly — they were not excluded.

## Known source hazards

- **`github.com/trending` is not fetchable** and a fetch attempt once returned **fabricated 2017-era repos** because the summarizer filled in from training data. Never trust it. Either skip GitHub trending or verify every repo on its own GitHub page (individual repo pages do fetch).
- **Reddit** is blocked; the archive API stores score-at-ingest, so upvote counts are unavailable. Report content, not popularity.
- **GitHub `/releases` listing pages** served through the fetch layer can be weeks stale. Verify dates against PyPI/NuGet or `/releases/tag/<v>` and `/releases/latest`.
- Dates can drift several days between a GitHub release page and the package registry. Pick one source and say which.
- If any agent reports a fetch that looks like training-data recall rather than live content, discard the item.

## Connector startup race

MCP connectors (including Roadmap) attach to a scheduled session asynchronously and may not be ready in the first few seconds. If a Roadmap tool call fails, or a tool search finds no Roadmap tools, do not conclude the connector is missing after a single check. Call `RefreshMcpTools` with no server filter and inspect the full server list: if "Roadmap" is absent entirely (not just missing tools), wait roughly 15-20 seconds and check again, up to 3 total attempts (~1 minute), before concluding it is genuinely unavailable and notifying Harutyun that the run could not proceed.
