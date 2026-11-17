#!/usr/bin/env python3
"""Real REST + MCP regression tests against an isolated, disposable Postgres database.
Usage: BASE=http://127.0.0.1:5056 python3 .github/reading-test/run.py
The app must be running with AUTH_PASSWORD=reading-test and MCP_OPEN=true.
Never run this against production: the suite creates and revises fixture editions.
"""
import json, os, urllib.request, urllib.error, uuid, datetime
BASE = os.environ.get('BASE', 'http://127.0.0.1:5056')
assert BASE.startswith(('http://127.0.0.1:', 'http://localhost:')), 'Local test server required'
token = None

def req(method, path, data=None, expected=200):
    headers = {'Content-Type':'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    r = urllib.request.Request(BASE+path, data=json.dumps(data).encode() if data is not None else None, headers=headers, method=method)
    try:
        with urllib.request.urlopen(r) as f: status, body = f.status, f.read()
    except urllib.error.HTTPError as e: status, body = e.code, e.read()
    assert status == expected, (method, path, status, expected, body[:1000])
    return json.loads(body) if body else None

def mcp(name, args={}):
    r=urllib.request.Request(BASE+'/mcp', data=json.dumps({'jsonrpc':'2.0','id':1,'method':'tools/call','params':{'name':name,'arguments':args}}).encode(), headers={'Content-Type':'application/json','Accept':'application/json, text/event-stream'})
    with urllib.request.urlopen(r) as f: body=f.read().decode()
    data=json.loads(body if body.lstrip().startswith('{') else next(line[5:] for line in body.splitlines() if line.startswith('data:')))
    assert 'error' not in data, data
    return json.loads(data['result']['content'][0]['text'])

def ok(message): print('PASS', message, flush=True)
req('GET','/api/reading/editions',expected=401)
token=req('POST','/api/auth/login',{'password':'reading-test'})['token']
ok('REST requires authentication')
start=req('GET','/api/reading/context')
contract=mcp('get_reading_contract')
article=contract['evaluation']; article['evaluationKey']='test-'+uuid.uuid4().hex
article.update(url='https://example.org/reading-test?utm_source=feed', title='Test original', section='exploration', explorationKind='distant')
original=req('POST','/api/reading/evaluations',article)
retry=req('POST','/api/reading/evaluations',article)
assert original['id']==retry['id']
assert original['article']['url']=='https://example.org/reading-test'
ok('idempotent evaluation and canonical URL')
wrong={**article,'access':'excerpt','evaluationKey':uuid.uuid4().hex}
req('POST','/api/reading/evaluations',wrong,400)
wrong={**article,'scores':{**article['scores'],'evidence':{'value':2,'reason':'Unsupported'}},'evaluationKey':uuid.uuid4().hex}
req('POST','/api/reading/evaluations',wrong,400)
req('POST','/api/reading/evaluations',{**article,'title':'Changed'},400)
req('POST','/api/reading/evaluations',{**article,'url':'javascript:alert(1)'},400)
req('POST','/api/reading/evaluations',{**article,'evaluationKey':uuid.uuid4().hex,'category':None},400)
req('POST','/api/reading/evaluations',{**article,'evaluationKey':uuid.uuid4().hex,'score':None},400)
req('POST','/api/reading/evaluations',{**article,'evaluationKey':uuid.uuid4().hex,'category':'sports'},400)
req('POST','/api/reading/evaluations',{**article,'evaluationKey':uuid.uuid4().hex,'score':101},400)
ok('rejects weak evidence, excerpt originals, mutable keys, unsafe URLs and a missing or unknown category/score')
now=datetime.datetime.now(datetime.timezone.utc)
day=(now+datetime.timedelta(hours=4)).date().isoformat()
edition={'issueDate':day,'title':'Morning Read fixture','coveredFrom':start['since'],'coveredUntil':now.isoformat(),'evaluationIds':[original['id']],'editorNote':'Test fixture only'}
published=req('POST','/api/reading/editions',edition)
e=published['edition']
assert not e['isRead']
assert req('GET','/api/reading/context')['anchor']=='cold-start'
assert req('GET','/api/reading/context')['since']==start['since']
ok('publishing does not advance cold-start checkpoint')
req('POST',f"/api/reading/editions/{e['id']}/read",{'isRead':True,'expectedRevision':e['revision']})
assert req('GET','/api/reading/context')['anchor']=='last-read'
assert req('POST','/api/reading/editions',edition)['edition']['isRead']
ok('only marking read advances checkpoint; exact publish retry preserves read')
feedback=req('PATCH',f"/api/reading/evaluations/{original['id']}/feedback",{'worthTime':'no','understanding':'known'})
req('PATCH',f"/api/reading/evaluations/{original['id']}/feedback",{'note':'Empty after reading'})
revision_article={**article,'evaluationKey':uuid.uuid4().hex,'whyRead':'New assessment'}
revision=mcp('save_reading_evaluation',{'evaluation_json':json.dumps(revision_article)})
replacement={**edition,'evaluationIds':[revision['id']],'expectedRevision':e['revision']}
r=req('POST','/api/reading/editions',replacement)
assert r['edition']['revision']==2 and not r['edition']['isRead']
assert r['feedback'][0]['response']['worthTime']=='no'
assert r['feedback'][0]['response']['note']=='Empty after reading'
req('POST',f"/api/reading/editions/{e['id']}/read",{'isRead':True,'expectedRevision':1},400)
req('POST','/api/reading/editions',{**replacement,'title':'Stale edit'},400)
ok('feedback survives revisions; stale read and replacement rejected')
discard={**article,'evaluationKey':uuid.uuid4().hex,'url':'https://example.org/rejected','decision':'discard'}
d=mcp('save_reading_evaluation',{'evaluation_json':json.dumps(discard)})
mcp('save_reading_evaluation',{'evaluation_json':json.dumps({**discard,'evaluationKey':uuid.uuid4().hex,'url':'https://example.org/rejected-2','category':None,'score':None})})
req('POST','/api/reading/editions',{**replacement,'evaluationIds':[d['id']],'expectedRevision':2},400)
req('POST','/api/reading/editions',{**replacement,'evaluationIds':[original['id'],revision['id']],'expectedRevision':2},400)
ok('discarded and duplicate source articles cannot enter an edition')
empty=req('POST','/api/reading/editions',{**replacement,'evaluationIds':[],'expectedRevision':2})
assert empty['edition']['itemCount']==0
settings=req('PUT','/api/reading/settings',{'dailyMinutes':25,'explorationPercent':25,'preferences':'Broad topics'})
assert req('GET','/api/reading/context')['settings']==settings
assert mcp('get_reading_context')['settings']==settings
history=mcp('list_reading_evaluations',{'offset':0,'limit':2})
assert len(history['items'])==2 and history['total']>=3
assert len(req('GET','/api/reading/history?offset=2&limit=2')['items'])>=1
ok('empty editions, persistent preferences, pagination and MCP/REST parity')
req('GET','/api/newsletters')
ok('existing newsletter API remains available')
print('All reading integration checks passed.')
