#!/usr/bin/env python3
"""Render one AI Engineering Daily edition from scored stories.

The scoring is judgment and stays with the agent; this script only does what must come out the
same every run: pick what fits each category, build the Top 10 and the breaking-change strip,
render the fixed design system, and split the document into parts when it is too large for one
publish call.

    python3 render_edition.py --items items.json --details details.json --meta meta.json --out edition.html

items.json   [{"title", "url", "source", "date": "YYYY-MM-DD", "score": 0-100, "category": <key>,
               "summary", "learn" (every category but tips) | "try" (tips; optional "try_code"),
               "flags": ["breaking", "dotnet"] (optional)}, ...]
details.json {url: {"lead": str,
                    "digest": [paragraph, ...] (1-3: the story retold, or the part worth reading),
                    "focus": str (optional: which part the digest covers, when not the whole piece),
                    "sections": [{"h": heading, "b": [bullet, ...]}, ...]}}  (optional)
meta.json    {"date": "YYYY-MM-DD", "covered_from": "YYYY-MM-DD", "covered_until": ISO UTC,
              "sources": str, "notes": [str, ...] (optional)}

Writes the edition to --out. When it is larger than SINGLE_CALL_LIMIT it also writes the parts to
publish it in — <out stem>.part1.html, .part2.html, … — each at most PART_SIZE bytes, which joined
in order are exactly the edition. Prints a JSON summary: `stories` (publish_newsletter's
item_count), counts per category, what was dropped, the size, and `publish` — "single" (one
publish_newsletter call) or the list of part files (start_newsletter_upload, one
append_newsletter_part per file, finish_newsletter_upload). Exits 1 with every problem listed when
the input is invalid; nothing is written then.
"""
import argparse
import datetime as dt
import html
import json
import sys

# key, section heading, chip label — in the order the edition shows them.
CATEGORIES = [
    ("tips", "Agent tips & workflows", "Agent tips"),
    ("coding", "Coding agents & IDEs", "Coding agents"),
    ("models", "Models & APIs", "Models & APIs"),
    ("agents", "Agents & MCP", "Agents & MCP"),
    ("rag", "RAG, search & evals", "RAG & evals"),
    ("local", "Local inference", "Local inference"),
    ("data", "Data infrastructure", "Data infra"),
    ("craft", "Engineering craft", "Craft"),
]
CAT = {k: (h, c) for k, h, c in CATEGORIES}
MIN_SCORE = 60          # below the "context" band nothing is shown, so a slow category stays short
CAP = 10                # per category, so a busy one cannot flood the edition
TOP_N = 10
DETAILS_BUDGET = 850    # characters of lead + bullets per Details panel (the digest is apart)
DIGEST_MAX = 1800       # characters across a digest's paragraphs; longer is refused, not cut
DIGEST_PARAS = 3
SINGLE_CALL_LIMIT = 100_000  # bytes; above this, publish in parts (one call above ~135 KB has failed)
PART_SIZE = 80_000      # bytes per part, well inside what one tool call carries
SECTION_ORDER = ["What changed", "How it works", "Numbers", "Breaking changes", "Caveats", "Discussion"]
TRIM_ORDER = ["Discussion", "How it works", "What changed", "Numbers", "Caveats", "Breaking changes"]
FLAGS = {"breaking": ("pb", "Breaking change"), "dotnet": ("pn", ".NET")}

def t(s):
    """Text node: escape only what HTML needs, to keep the edition small."""
    return html.escape(str(s), quote=False)

def a(s):
    return html.escape(str(s), quote=True)

def band(score):
    return "b9" if score >= 90 else "b8" if score >= 80 else "b7" if score >= 70 else "b6"

# ── input ──

def validate(items, details):
    errors, seen = [], set()
    for n, it in enumerate(items, 1):
        where = f"item {n} ({str(it.get('title', '?'))[:50]})"
        for f in ("title", "url", "source", "date", "score", "category", "summary"):
            if not it.get(f) and it.get(f) != 0:
                errors.append(f"{where}: missing {f}")
        cat = it.get("category")
        if cat not in CAT:
            errors.append(f"{where}: unknown category {cat!r}; use one of {', '.join(CAT)}")
        if cat == "tips" and not it.get("try"):
            errors.append(f"{where}: a tip needs `try` (the concrete step)")
        if cat in CAT and cat != "tips" and not it.get("learn"):
            errors.append(f"{where}: needs `learn`")
        if not isinstance(it.get("score"), int) or not 0 <= it["score"] <= 100:
            errors.append(f"{where}: score must be an integer 0-100")
        url = str(it.get("url", ""))
        if not url.startswith(("https://", "http://")):
            errors.append(f"{where}: url must be http(s)")
        if url in seen:
            errors.append(f"{where}: duplicate url {url} — dedupe before rendering")
        seen.add(url)
        try:
            dt.date.fromisoformat(str(it.get("date")))
        except ValueError:
            errors.append(f"{where}: date must be YYYY-MM-DD")
        bad = set(it.get("flags") or []) - set(FLAGS)
        if bad:
            errors.append(f"{where}: unknown flags {sorted(bad)}; use {sorted(FLAGS)}")
    for url, d in (details or {}).items():
        if not isinstance(d, dict) or "lead" not in d:
            errors.append(f"details for {url}: needs a lead")
            continue
        dg = d.get("digest")
        if dg is None:
            continue
        paras = [dg] if isinstance(dg, str) else dg
        if not isinstance(paras, list) or not all(isinstance(p, str) and p.strip() for p in paras):
            errors.append(f"details for {url}: digest must be 1-{DIGEST_PARAS} non-empty paragraphs")
        elif len(paras) > DIGEST_PARAS:
            errors.append(f"details for {url}: digest has {len(paras)} paragraphs; at most {DIGEST_PARAS}")
        elif sum(len(p) for p in paras) > DIGEST_MAX:
            errors.append(f"details for {url}: digest is {sum(len(p) for p in paras)} characters; shorten it to {DIGEST_MAX}")
    return errors

def select(items, cap=CAP):
    """Per category: drop below MIN_SCORE, rank by score, keep `cap`."""
    by_cat, dropped = {k: [] for k in CAT}, {"below_min": 0, "over_cap": {}}
    for it in items:
        if it["score"] < MIN_SCORE:
            dropped["below_min"] += 1
        else:
            by_cat[it["category"]].append(it)
    for k, lst in by_cat.items():
        lst.sort(key=lambda i: (-i["score"], i["title"]))
        if len(lst) > cap:
            dropped["over_cap"][k] = len(lst) - cap
            del lst[cap:]
    return by_cat, dropped

# ── details ──

def trimmed(d, budget):
    """The Details content cut to `budget` characters, dropping trailing bullets in TRIM_ORDER."""
    lead = d.get("lead", "")
    secs = {s["h"]: list(s.get("b") or []) for s in d.get("sections", []) if s.get("h") in SECTION_ORDER}
    size = lambda: len(lead) + sum(len(b) for bs in secs.values() for b in bs)
    while size() > budget:
        h = next((h for h in TRIM_ORDER if secs.get(h)), None)
        if h is None:
            break
        secs[h].pop()
    return lead, [(h, secs[h]) for h in SECTION_ORDER if secs.get(h)]

def modal(n, it, d, budget):
    lead, secs = trimmed(d, budget)
    kind = {"Breaking changes": " sb", "Caveats": " sc", "Numbers": " sn"}
    body = "".join(f'<div class="s{kind.get(h, "")}"><b>{t(h)}</b><ul>' + "".join(f"<li>{t(b)}</li>" for b in bs) + "</ul></div>"
                   for h, bs in secs)
    paras = d.get("digest") or []
    paras = [paras] if isinstance(paras, str) else paras
    digest = ""
    if paras:
        label = f'The part worth reading &middot; {t(d["focus"])}' if d.get("focus") else "The story"
        digest = f'<div class="dg"><b class="dl">{label}</b>' + "".join(f"<p>{t(p)}</p>" for p in paras) + "</div>"
    if body:
        body = '<b class="dl">Key facts</b>' + body if digest else body
    m = f"m{n}"
    return (f'<input type="checkbox" id="{m}" class="mtog"><label for="{m}" class="dbtn">Details</label>'
            f'<div class="modal"><label for="{m}" class="mback"></label><div class="mbox">'
            f'<label for="{m}" class="mclose">&times;</label>'
            f'<a class="mtitle" href="{a(it["url"])}">{t(it["title"])}&nbsp;&rarr;</a>'
            f'<p class="mlead">{t(lead)}</p>{digest}{body}</div></div>')

# ── render ──

def card(n, it, d, budget):
    pills = "".join(f'<span class="pill {FLAGS[f][0]}">{FLAGS[f][1]}</span>' for f in (it.get("flags") or []))
    if it["category"] == "tips":
        code = f'<pre>{t(it["try_code"])}</pre>' if it.get("try_code") else ""
        take = f'<div class="try"><span class="lbl">Try it</span><p>{t(it["try"])}</p>{code}</div>'
    else:
        take = f'<div class="learn"><span class="lbl">What you\'ll learn</span><p>{t(it["learn"])}</p></div>'
    return (f'<article class="entry" id="i{n}"><div class="badge-col"><div class="badge {band(it["score"])}">{it["score"]}</div></div>'
            f'<div class="content"><a class="title" href="{a(it["url"])}">{t(it["title"])}</a>'
            + (f'<div class="pills">{pills}</div>' if pills else "")
            + f'<span class="lbl">Summary</span><p class="summary-text">{t(it["summary"])}</p>{take}'
            f'<a class="url" href="{a(it["url"])}">{t(it["url"])}</a>'
            f'<div class="mrow"><span class="meta">{t(it["source"])} &middot; {t(it["date"])}</span>'
            + (modal(n, it, d, budget) if d else "") + "</div></div></article>")

def rows(title, cls, entries):
    li = "".join(f'<li><a href="#i{n}"><span class="mb {band(it["score"])}">{it["score"]}</span>'
                 f'<span class="rt">{t(it["title"])}</span><span class="rc">{t(CAT[it["category"]][1])}</span></a></li>'
                 for n, it in entries)
    return f'<section class="overview {cls}"><h2>{title}</h2><ol class="rows">{li}</ol></section>'

def render(by_cat, details, meta, budget, cap=CAP):
    shown = [k for k in CAT if by_cat[k]]
    numbered, n = {}, 0
    for k in shown:
        for it in by_cat[k]:
            n += 1
            numbered[it["url"]] = n
    everything = [it for k in shown for it in by_cat[k]]
    total = len(everything)
    top = sorted(everything, key=lambda i: (-i["score"], i["title"]))[:TOP_N]
    breaking = sorted((i for i in everything if "breaking" in (i.get("flags") or [])), key=lambda i: -i["score"])

    day = dt.date.fromisoformat(meta["date"])
    until = meta.get("covered_until", "")
    try:
        until = dt.datetime.fromisoformat(until.replace("Z", "+00:00")).astimezone(dt.timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    except ValueError:
        pass
    radios = '<input type="radio" name="f" id="f-all" class="fr" checked>' + "".join(
        f'<input type="radio" name="f" id="f-{k}" class="fr">' for k in shown)
    chips = (f'<nav class="chips"><label for="f-all">All <span>{total}</span></label>' + "".join(
        f'<label for="f-{k}">{t(CAT[k][1])} <span>{len(by_cat[k])}</span></label>' for k in shown) + "</nav>")
    # One radio per chip: a checked category hides every other section and the cross-category
    # overview; the checked chip is highlighted. Sibling selectors only, so no script is needed.
    filters = "".join(f"#f-{k}:checked~.cat:not(#c-{k}),#f-{k}:checked~.overview{{display:none}}" for k in shown)
    filters += ",".join(f"#f-{k}:checked~.chips label[for=f-{k}]" for k in ["all"] + shown) + (
        "{background:var(--link);border-color:var(--link);color:var(--card-bg)}")
    focus = ",".join(f"#f-{k}:focus-visible~.chips label[for=f-{k}]" for k in ["all"] + shown) + "{outline:2px solid var(--link);outline-offset:2px}"

    sections = "".join(
        f'<section class="cat" id="c-{k}"><h2 class="ch">{t(CAT[k][0])} <span>{len(by_cat[k])}</span></h2>'
        + "".join(card(numbered[it["url"]], it, details.get(it["url"]), budget) for it in by_cat[k]) + "</section>"
        for k in shown)
    overview = rows("Top 10 overall", "top", [(numbered[i["url"]], i) for i in top])
    if breaking:
        overview += rows("&#9888; Breaking changes", "brk", [(numbered[i["url"]], i) for i in breaking])

    counts = ", ".join(f"{CAT[k][0]} {len(by_cat[k])}" for k in shown)
    notes = "".join(f"<p>{t(x)}</p>" for x in meta.get("notes") or [])
    footer = (
        '<footer class="footer">'
        f'<p><strong>Scoring.</strong> Every story scored 0-100 by judgment on one shared scale (90+ drop everything, 80s high value, '
        f'70s worth a scan, 60s context), then ranked within its category; at most {cap} per category and nothing under {MIN_SCORE}. '
        f'The Top 10 is the ten highest scores across all categories. Agent tips are scored on how soon and how concretely they can be tried.</p>'
        f'<p><strong>Categories.</strong> {t(counts)}.</p>'
        f'<p><strong>Sources scanned.</strong> {t(meta.get("sources", ""))}</p>'
        '<p><strong>Excluded.</strong> Research papers; funding and acquisitions; AGI and policy discourse; AWS region expansions and '
        'routine model listings; ClickHouse patch/LTS bumps without features; engagement bait; generic prompt listicles and sponsored roundups.</p>'
        f'<p><strong>Window.</strong> {t(meta.get("covered_from", ""))} &rarr; {t(until)}.</p>'
        f'<p><strong>Details.</strong> Each Details panel was written from the fetched page: a digest of the story (or of the part '
        f'worth reading, named when it is only a part), then the key facts in labelled bullets, capped at {budget} characters.</p>{notes}</footer>')

    return (
        '<!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
        f'<title>AI Engineering Daily — {day.strftime("%d.%m.%Y")}</title><style>{CSS}{filters}{focus}</style></head><body><div class="page">'
        f'<header class="header"><div class="eyebrow">AI Engineering Daily</div><h1 class="date">{day:%A, %B} {day.day}, {day.year}</h1>'
        f'<div class="window">Covering {t(meta.get("covered_from", ""))} &rarr; {t(until)} &middot; {total} stories in {len(shown)} categories</div></header>'
        f'{radios}{chips}{overview}{sections}{footer}</div></body></html>'), total, shown

CSS = (
    ':root,:root[data-theme="light"]{--bg:#f7f7f5;--card-bg:#ffffff;--card-border:#e6e4df;--text:#1c1c1e;--text-muted:#6b6b6f;'
    '--link:#2952a3;--learn-bg:#eef4ff;--learn-border:#3b6fd6;--footer-border:#e0ded8;'
    '--card-shadow:0 1px 2px rgba(15,15,15,0.05),0 1px 1px rgba(15,15,15,0.03);'
    '--try-bg:#ebf6ef;--try-border:#0f7a42;--warn:#c2611f;--num:#0f7a42;'
    '--pb-bg:#fdecdd;--pb-fg:#8a3f0c;--pn-bg:#ede7fb;--pn-fg:#4b2f9c;'
    '--b8-bg:#cdead9;--b8-fg:#14532a;--b7-bg:#ffe4b0;--b7-fg:#7a4a00;--b6-bg:#e6e6e6;--b6-fg:#55565b}'
    '@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--bg:#14151a;--card-bg:#1c1e24;--card-border:#2a2d35;'
    '--text:#e7e7ea;--text-muted:#93949c;--link:#7aa2f7;--learn-bg:#1b2433;--learn-border:#4c7fdb;--footer-border:#2a2d35;--card-shadow:none;'
    '--try-bg:#16261d;--try-border:#3fb27f;--warn:#d9772e;--num:#3fb27f;--pb-bg:#3a2414;--pb-fg:#f0a56a;--pn-bg:#2a2340;--pn-fg:#c3b4f5;'
    '--b8-bg:#1f3d2c;--b8-fg:#9be8bb;--b7-bg:#40300f;--b7-fg:#ffcf80;--b6-bg:#2c2e34;--b6-fg:#c7c7cc}}'
    ':root[data-theme="dark"]{--bg:#14151a;--card-bg:#1c1e24;--card-border:#2a2d35;--text:#e7e7ea;--text-muted:#93949c;--link:#7aa2f7;'
    '--learn-bg:#1b2433;--learn-border:#4c7fdb;--footer-border:#2a2d35;--card-shadow:none;--try-bg:#16261d;--try-border:#3fb27f;'
    '--warn:#d9772e;--num:#3fb27f;--pb-bg:#3a2414;--pb-fg:#f0a56a;--pn-bg:#2a2340;--pn-fg:#c3b4f5;'
    '--b8-bg:#1f3d2c;--b8-fg:#9be8bb;--b7-bg:#40300f;--b7-fg:#ffcf80;--b6-bg:#2c2e34;--b6-fg:#c7c7cc}'
    '*{box-sizing:border-box}body{margin:0;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;'
    '-webkit-font-smoothing:antialiased;background:var(--bg);color:var(--text)}'
    '.page{max-width:720px;margin:0 auto;padding:0 16px 40px}'
    '.header{padding:28px 0 20px;border-bottom:1px solid var(--footer-border);margin-bottom:16px}'
    '.eyebrow{font-size:11px;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;color:var(--link);margin-bottom:8px}'
    '.date{font-size:26px;font-weight:700;letter-spacing:-0.02em;margin:0 0 6px}.window{font-size:13px;color:var(--text-muted)}'
    '.fr{position:absolute;opacity:0;pointer-events:none}'
    '.chips{display:flex;flex-wrap:wrap;gap:6px;margin:0 0 20px}'
    '.chips label{font-size:12.5px;font-weight:600;padding:5px 11px;border-radius:999px;border:1px solid var(--card-border);'
    'background:var(--card-bg);color:var(--text);cursor:pointer;user-select:none}.chips label span{opacity:.65;font-weight:500}'
    '.overview{background:var(--card-bg);border:1px solid var(--card-border);border-radius:14px;padding:14px 16px 8px;margin-bottom:14px;box-shadow:var(--card-shadow)}'
    '.overview h2{font-size:11px;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;color:var(--text-muted);margin:0 0 6px}'
    '.brk h2{color:var(--warn)}.rows{list-style:none;margin:0;padding:0}'
    '.rows a{display:flex;align-items:center;gap:10px;padding:7px 0;text-decoration:none;color:var(--text);border-top:1px solid var(--card-border)}'
    '.rows li:first-child a{border-top:0}.rt{flex:1;min-width:0;font-size:14px;line-height:1.35}'
    '.rc{font-size:11.5px;color:var(--text-muted);white-space:nowrap}'
    '.mb{flex:0 0 34px;height:24px;border-radius:7px;display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:700}'
    '.ch{font-size:19px;font-weight:700;letter-spacing:-0.01em;margin:30px 0 12px}.ch span{font-size:13px;font-weight:600;color:var(--text-muted)}'
    '.entry{display:flex;gap:16px;padding:18px;border-radius:14px;border:1px solid var(--card-border);background:var(--card-bg);'
    'margin-bottom:14px;box-shadow:var(--card-shadow);scroll-margin-top:12px}'
    '.badge-col{flex:0 0 56px}.badge{width:56px;height:56px;border-radius:14px;display:flex;align-items:center;justify-content:center;'
    'font-size:21px;font-weight:700;letter-spacing:-0.02em}'
    '.b9{background:#0f7a42;color:#ffffff}.b8{background:var(--b8-bg);color:var(--b8-fg)}.b7{background:var(--b7-bg);color:var(--b7-fg)}'
    '.b6{background:var(--b6-bg);color:var(--b6-fg)}'
    '.content{flex:1;min-width:0}'
    '.title{font-size:16.5px;font-weight:600;letter-spacing:-0.01em;color:var(--text);text-decoration:none;display:block;margin-bottom:10px}'
    '.pills{display:flex;flex-wrap:wrap;gap:6px;margin:-4px 0 10px}'
    '.pill{font-size:11px;font-weight:700;padding:2px 8px;border-radius:999px}'
    '.pb{background:var(--pb-bg);color:var(--pb-fg)}.pn{background:var(--pn-bg);color:var(--pn-fg)}'
    '.lbl{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:0.05em;color:var(--text-muted);display:block;margin-bottom:4px}'
    '.summary-text{font-size:14px;line-height:1.55;color:var(--text);margin:0 0 12px}'
    '.learn,.try{border-left:3px solid var(--learn-border);background:var(--learn-bg);border-radius:0 10px 10px 0;padding:10px 12px;margin-bottom:12px}'
    '.learn .lbl{color:var(--learn-border)}.try{border-left-color:var(--try-border);background:var(--try-bg)}.try .lbl{color:var(--try-border)}'
    '.learn p,.try p{font-size:14px;line-height:1.5;margin:0;color:var(--text)}'
    '.try pre{margin:8px 0 0;padding:8px 10px;background:var(--card-bg);border:1px solid var(--card-border);border-radius:8px;'
    'font:12.5px/1.45 "SF Mono","Cascadia Code",Consolas,"Liberation Mono",monospace;white-space:pre-wrap;word-break:break-word;color:var(--text)}'
    '.url{font-family:"SF Mono","Cascadia Code",Consolas,"Liberation Mono",monospace;font-size:12.5px;color:var(--link);'
    'word-break:break-all;display:block;margin-bottom:5px;text-decoration:none}'
    '.mrow{display:flex;align-items:center;gap:8px}.meta{font-size:12px;color:var(--text-muted);flex:1;min-width:0}'
    '.mtog{position:absolute;opacity:0;pointer-events:none}'
    '.dbtn{font-size:12px;font-weight:600;border-radius:999px;padding:4px 11px;color:var(--link);border:1px solid var(--card-border);'
    'cursor:pointer;white-space:nowrap}'
    '.modal{display:none;position:fixed;inset:0;z-index:50}.mtog:checked~.modal{display:block}'
    '.mback{position:absolute;inset:0;background:rgba(0,0,0,0.45)}'
    '.mbox{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);width:calc(100% - 24px);max-width:640px;max-height:88vh;'
    'overflow:auto;background:var(--card-bg);border:1px solid var(--card-border);border-radius:14px;padding:18px 20px 20px}'
    '.mclose{position:absolute;top:8px;right:12px;font-size:22px;line-height:1;cursor:pointer;color:var(--text-muted)}'
    '.mtitle{display:block;font-size:17px;font-weight:600;color:var(--text);text-decoration:none;margin:0 28px 10px 0}'
    '.mlead{font-size:15px;line-height:1.5;font-weight:500;margin:0 0 12px}'
    '.s{margin:0 0 10px}.s b{display:block;font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:0.05em;'
    'color:var(--learn-border);margin-bottom:4px}.sb b{color:var(--warn)}.sc b{color:var(--text-muted)}.sn b{color:var(--num)}'
    '.s ul{margin:0;padding:0;list-style:none}.s li{position:relative;padding-left:14px;font-size:14px;line-height:1.5;margin:2px 0}'
    '.s li:before{content:"";position:absolute;left:1px;top:0.6em;width:6px;height:6px;border-radius:50%;background:currentColor;opacity:0.55}'
    '.dl{display:block;font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:0.05em;color:var(--text-muted);margin:0 0 6px}'
    '.dg{margin:0 0 14px;padding:0 0 12px;border-bottom:1px solid var(--card-border)}'
    '.dg p{font-size:14.5px;line-height:1.6;margin:0 0 10px}.dg p:last-child{margin-bottom:0}'
    'body:has(.mtog:checked){overflow:hidden}'
    '.footer{margin-top:28px;padding-top:20px;border-top:1px solid var(--footer-border);font-size:12.5px;line-height:1.6;color:var(--text-muted)}'
    '.footer strong{color:var(--text)}.footer p{margin:0 0 8px}'
    '@media (max-width:480px){.entry{gap:12px;padding:14px}.badge-col{flex-basis:44px}.badge{width:44px;height:44px;font-size:18px;border-radius:12px}'
    '.rc{display:none}}'
)

def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--items", required=True)
    ap.add_argument("--details")
    ap.add_argument("--meta", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    items = json.load(open(args.items, encoding="utf-8"))
    details = json.load(open(args.details, encoding="utf-8")) if args.details else {}
    meta = json.load(open(args.meta, encoding="utf-8"))

    errors = validate(items, details)
    if not meta.get("date"):
        errors.append("meta: date is required")
    if errors:
        print("Not rendered:\n- " + "\n- ".join(errors), file=sys.stderr)
        sys.exit(1)

    by_cat, dropped = select(items)
    doc, total, shown = render(by_cat, details, meta, DETAILS_BUDGET)
    data = doc.encode("utf-8")
    with open(args.out, "w", encoding="utf-8") as f:
        f.write(doc)

    publish = "single"
    if len(data) > SINGLE_CALL_LIMIT:
        stem = args.out[:-5] if args.out.endswith(".html") else args.out
        publish = []
        for n, chunk in enumerate(split(data), 1):
            path = f"{stem}.part{n}.html"
            with open(path, "wb") as f:
                f.write(chunk)
            publish.append(path)
        assert b"".join(open(p, "rb").read() for p in publish) == data

    print(json.dumps({
        "stories": total,
        "categories": {k: len(by_cat[k]) for k in shown},
        "empty_categories": [k for k in CAT if k not in shown],
        "dropped_below_min": dropped["below_min"],
        "dropped_over_cap": dropped["over_cap"],
        "bytes": len(data),
        "publish": publish,
    }, indent=2))

def split(data):
    """The document in parts of at most PART_SIZE bytes, each ending just after a tag where one is
    near the limit and never inside a UTF-8 character, so the parts read cleanly and join exactly."""
    parts, i = [], 0
    while i < len(data):
        end = min(i + PART_SIZE, len(data))
        if end < len(data):
            gt = data.rfind(b">", end - 4096, end)
            if gt > i:
                end = gt + 1
            while end > i and (data[end] & 0xC0) == 0x80:
                end -= 1
        parts.append(data[i:end])
        i = end
    return parts

if __name__ == "__main__":
    main()
