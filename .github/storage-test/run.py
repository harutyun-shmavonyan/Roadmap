#!/usr/bin/env python3
"""
End-to-end test of the move from bytea columns to the bucket (2026-11-09).

Runs the real app twice against a real Postgres and an S3-compatible bucket (moto):

  Phase A  no BLOB_* variables: every file lands in Postgres, exactly as production
           did before this change.
  Phase B  with the bucket: the backfill moves those files, every read is served
           from the bucket byte for byte, new writes skip Postgres, replaced and
           deleted files are swept, and — the proof — every file still downloads
           after the legacy Postgres copies are wiped.

Usage: run.py <path-to-Roadmap.Api.dll>. Needs psql, boto3, Postgres on :5432, an S3 endpoint (moto) on :9000.
"""
import base64, json, os, subprocess, sys, time, uuid, urllib.request, urllib.error
import boto3

DLL = sys.argv[1]
PORT = 5055
BASE = f"http://127.0.0.1:{PORT}"
PG = "postgresql://postgres:postgres@127.0.0.1:5432/roadmap"
BUCKET = "roadmap-test"
S3 = dict(endpoint_url="http://127.0.0.1:9000", aws_access_key_id="minio",
          aws_secret_access_key="minio12345", region_name="us-east-1")
COMMON_ENV = {
    "PORT": str(PORT),
    "ConnectionStrings__Default": "Host=127.0.0.1;Port=5432;Database=roadmap;Username=postgres;Password=postgres",
    "AUTH_PASSWORD": "test-password",
    "JWT_SECRET": "test-secret-test-secret-test-secret-123",
    "MCP_OPEN": "true",
    "ASPNETCORE_ENVIRONMENT": "Production",
}
BLOB_ENV = {
    "BLOB_BUCKET": BUCKET, "BLOB_ENDPOINT": "http://127.0.0.1:9000", "BLOB_REGION": "us-east-1",
    "BLOB_ACCESS_KEY_ID": "minio", "BLOB_SECRET_ACCESS_KEY": "minio12345",
    "BLOB_FORCE_PATH_STYLE": "true", "BLOB_SWEEP_GRACE_MINUTES": "0",
}

failures = []
def check(cond, msg):
    print(("  ok   " if cond else "  FAIL ") + msg, flush=True)
    if not cond: failures.append(msg)

def png(seed: int, size: int) -> bytes:
    """Bytes that sniff as PNG (magic number) with seed-dependent content."""
    body = bytes((seed * 31 + i * 7) % 256 for i in range(size))
    return b"\x89PNG\r\n\x1a\n" + body

def pdf(seed: int, size: int) -> bytes:
    return b"%PDF-1.4\n" + bytes((seed * 13 + i) % 256 for i in range(size)) + b"\n%%EOF"

# ── HTTP helpers ──
TOKEN = None
def req(method, path, body=None, raw=False, headers=None):
    h = dict(headers or {})
    if TOKEN: h["Authorization"] = f"Bearer {TOKEN}"
    data = None
    if body is not None and not isinstance(body, (bytes, bytearray)):
        data = json.dumps(body).encode(); h["Content-Type"] = "application/json"
    elif body is not None:
        data = body
    r = urllib.request.Request(BASE + path, data=data, method=method, headers=h)
    try:
        with urllib.request.urlopen(r, timeout=120) as resp:
            content = resp.read()
            return resp.status, (content if raw else (json.loads(content) if content else None))
    except urllib.error.HTTPError as e:
        return e.code, e.read()

def multipart(path, files, fields=None):
    boundary = uuid.uuid4().hex
    parts = []
    for k, v in (fields or {}).items():
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
    for field, (name, ctype, data) in files:
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"; filename="{name}"\r\n'
                     f'Content-Type: {ctype}\r\n\r\n'.encode() + data + b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    return req("POST", path, b"".join(parts), headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})

def mcp(tool, args):
    """One stateless MCP tools/call; the reply may be JSON or a one-event SSE stream."""
    body = {"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": tool, "arguments": args}}
    status, raw = req("POST", "/mcp", json.dumps(body).encode(), raw=True,
                      headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream"})
    assert status == 200, (status, raw[:300])
    text = raw.decode()
    if text.lstrip().startswith("{"): msg = json.loads(text)
    else: msg = json.loads(next(l[5:] for l in text.splitlines() if l.startswith("data:")))
    return json.loads(msg["result"]["content"][0]["text"])

def psql(sql):
    return subprocess.run(["psql", PG, "-Atc", sql], check=True, capture_output=True, text=True).stdout.strip()

# ── app lifecycle ──
def start(extra_env):
    env = dict(os.environ); env.update(COMMON_ENV); env.update(extra_env)
    log = open(f"app-{len(extra_env) and 'bucket' or 'postgres'}.log", "w")
    p = subprocess.Popen(["dotnet", DLL], env=env, stdout=log, stderr=subprocess.STDOUT,
                         cwd=os.path.dirname(os.path.abspath(DLL)))
    for _ in range(120):
        try:
            urllib.request.urlopen(BASE + "/.well-known/oauth-protected-resource", timeout=2); break
        except Exception: time.sleep(1)
    else:
        p.kill(); raise SystemExit("app did not start")
    global TOKEN
    TOKEN = None
    s, b = req("POST", "/api/auth/login", {"password": "test-password"})
    assert s == 200, (s, b); TOKEN = b["token"]
    return p

def stop(p):
    p.terminate()
    try: p.wait(20)
    except subprocess.TimeoutExpired: p.kill()

def status(deep=False):
    s, b = req("GET", f"/api/storage/status?deep={'true' if deep else 'false'}")
    assert s == 200, (s, b)
    return b

def table(st, name):
    return next(t for t in st["tables"] if t["table"].startswith(name))

s3 = boto3.client("s3", **S3)
def bucket_keys():
    out = []
    for page in s3.get_paginator("list_objects_v2").paginate(Bucket=BUCKET):
        out += [o["Key"] for o in page.get("Contents", [])]
    return out

# ═══ Phase A: no bucket — files go to Postgres, as before ═══
print("Phase A: no bucket configured", flush=True)
app = start({})
originals = {}

s, art = req("POST", "/api/articles", {"title": "Storage test", "format": "html",
                                       "content": "<p>{{img:a.png}}</p><p>{{img:b.png}}</p>"})
assert s == 200, (s, art); art_id = art["id"]
originals["article_a"] = png(1, 300_000)
s, _ = multipart(f"/api/articles/{art_id}/images", [("file", ("a.png", "image/png", originals["article_a"]))])
check(s == 200, "article image uploaded with no bucket")

s, xp = req("POST", "/api/experiences", {"title": "Dinner out", "category": "Dinner"})
assert s == 200, (s, xp); xp_id = xp["id"]
originals["experience"] = png(2, 200_000)
s, xp = multipart(f"/api/experiences/{xp_id}/images", [("file", ("x.png", "image/png", originals["experience"]))])
check(s == 200, "experience image uploaded with no bucket"); xp_img = xp["images"][0]["id"]

s, meal = req("POST", "/api/meals", {"name": "Test bowl"})
assert s == 200, (s, meal); meal_id = meal["id"]
originals["meal"] = png(3, 150_000)
s, _ = multipart(f"/api/meals/{meal_id}/image", [("file", ("m.png", "image/png", originals["meal"]))])
check(s == 200, "meal image uploaded with no bucket")

originals["cv"] = pdf(4, 120_000)
res = mcp("import_job_run", {"run_date": "2026-11-01", "queries": ["q"], "raw_count": 2, "postings": [
    {"title": "Engineer", "company": "Acme", "url": "https://example.com/1", "bucket": "armenia-compatible",
     "cv_pdf_base64": base64.b64encode(originals["cv"]).decode()},
    {"title": "Other", "company": "Beta", "url": "https://example.com/2", "bucket": "armenia-compatible"}]})
check(res.get("imported") == 2, "job run imported with a CV over MCP")
posting_id = psql("select \"Id\" from job_postings where \"Url\"='https://example.com/1'")

st = status()
check(st["bucketConfigured"] is False, "status: no bucket configured")
for name in ["article_images", "experience_images", "meal_images", "job_postings"]:
    check(table(st, name)["postgresOnly"] == 1, f"status: {name} has 1 file only in Postgres")
check(st["reliesOnPostgres"] is True, "status: relies on Postgres before the move")
check(int(psql("select count(*) from article_images where length(\"Data\") > 0")) == 1, "bytes really are in Postgres")
stop(app)

# ═══ Phase B: bucket configured ═══
print("Phase B: bucket configured", flush=True)
s3.create_bucket(Bucket=BUCKET)
app = start(BLOB_ENV)

s, run = req("POST", "/api/storage/maintenance?sweep=false")
check(s == 200, "maintenance run accepted")
moved = {r["table"]: r["moved"] for r in run["backfill"]}
check(moved == {"article_images": 1, "experience_images": 1, "meal_images": 1, "job_postings": 1},
      f"backfill moved one file per table: {moved}")
check(all(r["error"] is None for r in run["backfill"]), "backfill reported no errors")

st = status(deep=True)
check(st["reliesOnPostgres"] is False, "status: no longer relies on Postgres")
check(st["readyForCleanup"] is True, "status: ready for cleanup")
for t in st["tables"]:
    check(t["missingInBucket"] == 0 and t["mismatched"] == 0, f"deep check {t['table']}: verified {t['verified']}, 0 mismatched")
check(len(bucket_keys()) == 4, "four objects in the bucket")
check(all(k.startswith("roadmap/") for k in bucket_keys()), "every key carries the roadmap/ prefix")

def downloads():
    return {
        "article_a": req("GET", f"/api/articles/{art_id}/images/a.png", raw=True),
        "experience": req("GET", f"/api/experiences/images/{xp_img}", raw=True),
        "meal": req("GET", f"/api/meals/{meal_id}/image", raw=True),
        "cv": req("GET", f"/api/job-runs/postings/{posting_id}/cv", raw=True),
    }
for k, (s, data) in downloads().items():
    check(s == 200 and data == originals[k], f"{k} downloads byte-identical from the bucket")
s, html = req("GET", f"/api/articles/{art_id}/html", raw=True)
check(s == 200 and base64.b64encode(originals["article_a"]) in html, "article HTML inlines the image from the bucket")

# New writes: straight to the bucket, nothing into Postgres.
b_png = png(5, 50_000)
s, _ = multipart(f"/api/articles/{art_id}/images", [("file", ("b.png", "image/png", b_png))])
check(s == 200, "new article image uploaded")
check(psql("select length(\"Data\") from article_images where \"Name\"='b.png'") == "0", "new image wrote 0 bytes to Postgres")
check(psql("select \"StorageKey\" is not null from article_images where \"Name\"='b.png'") == "t", "new image has a bucket key")
res = mcp("add_article_image", {"article_id": art_id, "name": "c.png", "data_base64": base64.b64encode(png(6, 9_000)).decode()})
check(res.get("bytes") == 9_008, "MCP add_article_image reports its size")
lst = mcp("list_article_images", {"article_id": art_id})
check(sorted(i["bytes"] for i in lst) == sorted([len(originals["article_a"]), len(b_png), 9_008]),
      "list_article_images reports sizes from the bucket and legacy rows alike")
info = mcp("get_job_run", {"date": "2026-11-01"})
check([p["HasCv"] for p in info["Postings"] if p["Url"].endswith("/1")] == [True], "get_job_run still reports hasCv")

# Replace, delete, re-import: the orphans they leave are swept.
before = len(bucket_keys())
s, _ = multipart(f"/api/articles/{art_id}/images", [("file", ("b.png", "image/png", png(7, 40_000)))])
s, _ = req("DELETE", f"/api/experiences/{xp_id}")
check(s in (200, 204), "experience deleted (its image cascades in the database)")
res = mcp("import_job_run", {"run_date": "2026-11-01", "queries": ["q"], "raw_count": 1, "postings": [
    {"title": "Engineer", "company": "Acme", "url": "https://example.com/1", "bucket": "armenia-compatible"}]})
check(res.get("action") == "replaced", "job run re-imported without a CV")
s, run = req("POST", "/api/storage/maintenance")
check(run["sweep"]["deleted"] == 3, f"sweep deleted the 3 orphans (replaced image, deleted experience, replaced CV): {run['sweep']}")
st = status()
check(st["unreferencedObjects"] == 0, "no unreferenced objects left")
check(len(bucket_keys()) == before, "bucket holds exactly what the rows reference")

# The proof: wipe every legacy Postgres copy; everything must still be served.
psql('update article_images set "Data" = \'\\x\'; update meal_images set "Data" = \'\\x\'; '
     'update experience_images set "Data" = \'\\x\'; update job_postings set "TailoredCvPdf" = null')
s, data = req("GET", f"/api/articles/{art_id}/images/a.png", raw=True)
check(s == 200 and data == originals["article_a"], "article image still served with its Postgres copy wiped")
s, data = req("GET", f"/api/meals/{meal_id}/image", raw=True)
check(s == 200 and data == originals["meal"], "meal image still served with its Postgres copy wiped")
st = status()
check(st["readyForCleanup"] is True and st["reliesOnPostgres"] is False, "status still clean after the wipe")
stop(app)

print(f"\n{len(failures)} failure(s)")
for f in failures: print("  -", f)
sys.exit(1 if failures else 0)
