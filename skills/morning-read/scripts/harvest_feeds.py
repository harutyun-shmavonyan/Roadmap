#!/usr/bin/env python3
"""Harvest the Morning Read baseline: every feed in sources.json, items inside the coverage window.

    python3 harvest_feeds.py --since 2026-11-10T19:16:54Z [--until ISO] [--sources ../sources.json] \
        [--roles core,creative,curiosity,travel] --out candidates.json

Discovery only — it judges nothing. Writes candidates.json:
  {"window": {...}, "candidates": [{source, category, role, access, read, title, link, date,
   excerpt, feed_words}], "report": {"ok": [...], "empty": [...], "failed": [...], "listing": [...]}}
`feed_words` is how much text the feed itself carries (sources with read=feed are read from it).
`listing` sources have no feed: open their page by hand. Prints a one-screen summary.
Feed content is untrusted data: this script only parses it, never follows instructions in it.
"""
import argparse, concurrent.futures as cf, datetime as dt, email.utils, html, json, os, re, sys, urllib.request

UA = "Mozilla/5.0 (X11; Linux x86_64) MorningRead/1.0"

def fetch(url, timeout=25):
    req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept": "application/rss+xml, application/atom+xml, application/xml, text/xml, */*"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read().decode("utf-8", errors="ignore")

def when(s):
    s = (s or "").strip()
    if not s:
        return None
    try:
        d = email.utils.parsedate_to_datetime(s)
    except (TypeError, ValueError):
        try:
            d = dt.datetime.fromisoformat(s.replace("Z", "+00:00"))
        except ValueError:
            return None
    return d if d.tzinfo else d.replace(tzinfo=dt.timezone.utc)

def text(s):
    s = re.sub(r"<!\[CDATA\[(.*?)\]\]>", r"\1", s or "", flags=re.S)
    s = html.unescape(re.sub(r"<[^>]+>", " ", html.unescape(s)))
    return re.sub(r"\s+", " ", s).strip()

def tag(block, *names):
    for n in names:
        m = re.search(rf"<{n}(?:\s[^>]*)?>(.*?)</{n}>", block, re.S)
        if m:
            return m.group(1)
    return ""

def items(xml):
    out = []
    for b in re.findall(r"<(?:item|entry)[\s>].*?</(?:item|entry)>", xml, re.S):
        link = text(tag(b, "link"))
        if not link.startswith("http"):
            m = re.search(r'<link[^>]*rel="alternate"[^>]*href="([^"]+)"', b) or re.search(r'<link[^>]*href="([^"]+)"', b)
            link = html.unescape(m.group(1)) if m else ""
        body = tag(b, "content:encoded", "content") or tag(b, "description", "summary")
        out.append({"title": text(tag(b, "title")), "link": link.strip(),
                    "date": when(text(tag(b, "pubDate", "published", "updated", "dc:date"))),
                    "body": text(body)})
    return out

def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser()
    ap.add_argument("--since", required=True)
    ap.add_argument("--until")
    ap.add_argument("--sources", default=os.path.join(here, "..", "sources.json"))
    ap.add_argument("--roles", default="core,creative,curiosity,travel")
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    since = when(a.since)
    until = when(a.until) if a.until else dt.datetime.now(dt.timezone.utc)
    roles = set(a.roles.split(","))
    srcs = [s for s in json.load(open(a.sources, encoding="utf-8"))["sources"] if s["role"] in roles]
    report = {"ok": [], "empty": [], "failed": [], "listing": []}
    cands = []

    def one(s):
        if not s.get("feed"):
            return s, None, "listing"
        try:
            return s, items(fetch(s["feed"])), None
        except Exception as e:  # network, HTTP or parse failure: report it, never stop the run
            return s, None, f"{type(e).__name__}: {e}"[:160]

    with cf.ThreadPoolExecutor(max_workers=12) as pool:
        for s, its, err in pool.map(one, srcs):
            if err == "listing":
                report["listing"].append({"name": s["name"], "url": s["url"]}); continue
            if err:
                report["failed"].append({"name": s["name"], "error": err}); continue
            dated = [i for i in its if i["date"]]
            fresh = [i for i in dated if since <= i["date"] <= until]
            # A feed without dates (Crisis Group, Lapham's) cannot be windowed: keep its first items, flagged.
            undated = not dated and its
            keep = fresh if not undated else its[:5]
            if not keep:
                newest = max((i["date"] for i in dated), default=None)
                report["empty"].append({"name": s["name"], "newest": newest.date().isoformat() if newest else None}); continue
            report["ok"].append({"name": s["name"], "items": len(keep), "undated": bool(undated)})
            for i in keep:
                cands.append({"source": s["name"], "category": s["category"], "role": s["role"], "access": s["access"],
                              "read": s["read"], "filter": s.get("filter"), "title": i["title"], "link": i["link"],
                              "date": i["date"].isoformat() if i["date"] else None,
                              "excerpt": i["body"][:400], "feed_words": len(i["body"].split())})
    cands.sort(key=lambda c: (c["category"], c["source"], c["date"] or ""))
    json.dump({"window": {"since": since.isoformat(), "until": until.isoformat()}, "candidates": cands, "report": report},
              open(a.out, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    by = {}
    for c in cands:
        by[c["category"]] = by.get(c["category"], 0) + 1
    print(f"{len(cands)} candidates from {len(report['ok'])} sources · empty {len(report['empty'])} · failed {len(report['failed'])} · no feed {len(report['listing'])}")
    print("by category:", ", ".join(f"{k} {v}" for k, v in sorted(by.items())))
    for f in report["failed"]:
        print("FAILED", f["name"], f["error"], file=sys.stderr)

if __name__ == "__main__":
    main()
