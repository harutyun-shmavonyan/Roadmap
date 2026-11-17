const {chromium}=require('playwright');
const assert=require('node:assert/strict');
(async()=>{
 const base='http://127.0.0.1:5056';
 const login=await fetch(base+'/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({password:'reading-test'})}).then(r=>r.json());
 async function api(path,body,method=body?'POST':'GET') {const r=await fetch(base+'/api/reading'+path,{method,headers:{Authorization:'Bearer '+login.token,'Content-Type':'application/json'},body:body?JSON.stringify(body):undefined});assert(r.ok,await r.clone().text());return r.json();}
 const history=await api('/history');const original=history.items.find(x=>x.article.decision==='original');
 const items=[];
 for(const [section,title,format] of [['now','A consequential development, without the repetition','summary'],['depth','Why some explanations survive their headlines','original'],['exploration','A different way to understand urban ecosystems','original']]) {
  const a={...original.article,evaluationKey:'ui-'+section,url:'https://example.org/ui-'+section,section,title,format,decision:format==='original'?'original':'briefing',imageUrl:null,explorationKind:section==='exploration'?'distant':null,whyRead:'Local UI fixture: a specific mechanism and its limits, rather than another general claim.',summary:'This is a local test fixture for rendering and feedback controls. No test material is published to the live app.',learningGain:'How the underlying mechanism changes what you would expect to observe.',originalValue:'Worked examples develop the reasoning in stages.',readMinutes:section==='now'?2:8};
  items.push(await api('/evaluations',a));
 }
 const editions=await api('/editions');const e=editions[0];
 await api('/editions',{issueDate:e.issueDate,title:'A broader view of the world',coveredFrom:e.coveredFrom,coveredUntil:e.coveredUntil,evaluationIds:items.map(i=>i.id),editorNote:'Local UI verification edition — three sections, original and generated provenance, score evidence and feedback.',expectedRevision:e.revision});
 const browser=await chromium.launch({headless:true,args:['--no-sandbox']});
 const page=await browser.newPage({viewport:{width:1280,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.addInitScript(token=>localStorage.setItem('roadmap_token',token),login.token);
 await page.goto(base+'/#/reading');await page.getByRole('heading',{name:'A broader view of the world'}).waitFor();
 await page.screenshot({path:'/tmp/morning-read-desktop.png',fullPage:true});
 assert.equal(await page.locator('.mr-card').count(),3);
 await page.locator('.mr-card').first().getByText('Your feedback',{exact:false}).click();
 await page.locator('.mr-card').first().getByLabel('Worth the time?').selectOption('yes');
 await page.locator('.mr-card').first().getByText('Your feedback · Saved').waitFor();
 await page.reload();await page.locator('.mr-card').first().getByText('Your feedback',{exact:false}).click();
 assert.equal(await page.locator('.mr-card').first().getByLabel('Worth the time?').inputValue(),'yes');
 await page.getByRole('button',{name:'Mark as read',exact:true}).first().click();
 await page.getByRole('button',{name:'Mark as unread',exact:true}).waitFor();
 await page.getByRole('button',{name:'Reading preferences'}).click();await page.getByLabel('Minutes per day').fill('35');await page.getByRole('button',{name:'Save preferences'}).click();
 await page.getByRole('button',{name:'Reading preferences'}).click();assert.equal(await page.getByLabel('Minutes per day').inputValue(),'35');await page.getByRole('button',{name:'Reading preferences'}).click();
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:'/tmp/morning-read-mobile.png',fullPage:true});
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),'Mobile document overflows');
 assert(await page.locator('.mr-page').evaluate(e=>e.scrollWidth<=e.clientWidth),'Mobile reading content overflows');
 assert.deepEqual(errors,[]);
 await browser.close();console.log('PASS desktop/mobile rendering, saved feedback, read toggle, preferences and no horizontal overflow');
})().catch(e=>{console.error(e);process.exit(1)});
