const {chromium}=require('playwright');
const assert=require('node:assert/strict');
(async()=>{
 const base='http://127.0.0.1:5056';
 const login=await fetch(base+'/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({password:'reading-test'})}).then(r=>r.json());
 async function api(path,body,method=body?'POST':'GET') {const r=await fetch(base+'/api/reading'+path,{method,headers:{Authorization:'Bearer '+login.token,'Content-Type':'application/json'},body:body?JSON.stringify(body):undefined});assert(r.ok,await r.clone().text());return r.json();}
 const history=await api('/history');const original=history.items.find(x=>x.article.decision==='original');
 const items=[];
 for(const [section,title,format,category,score] of [['now','A consequential development, without the repetition','summary','world',72],['depth','Why some explanations survive their headlines','original','ideas',88],['exploration','A different way to understand urban ecosystems','original','science',81]]) {
  const a={...original.article,evaluationKey:'ui-'+section,url:'https://example.org/ui-'+section,section,title,format,category,score,decision:format==='original'?'original':'briefing',imageUrl:null,explorationKind:section==='exploration'?'distant':null,whyRead:'Local UI fixture: a specific mechanism and its limits, rather than another general claim.',summary:'This is a local test fixture for rendering and feedback controls. No test material is published to the live app.',learningGain:'How the underlying mechanism changes what you would expect to observe.',originalValue:'Worked examples develop the reasoning in stages.',readMinutes:section==='now'?2:8};
  items.push(await api('/evaluations',a));
 }
 const editions=await api('/editions');const e=editions[0];
 await api('/editions',{issueDate:e.issueDate,title:'A broader view of the world',coveredFrom:e.coveredFrom,coveredUntil:e.coveredUntil,evaluationIds:items.map(i=>i.id),editorNote:'Local UI verification edition — three categories, original and generated provenance, score evidence and feedback.',expectedRevision:e.revision});
 const browser=await chromium.launch({headless:true,args:['--no-sandbox']});
 const page=await browser.newPage({viewport:{width:1280,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.addInitScript(token=>localStorage.setItem('roadmap_token',token),login.token);
 await page.goto(base+'/#/reading');await page.getByText('A broader view of the world').first().waitFor();
 await page.screenshot({path:'/tmp/morning-read-desktop.png',fullPage:true});
 // Newsletter layout: a section per category, highest score first in the overall ranking, badges.
 assert.equal(await page.locator('.mrn-entry').count(),3);
 assert.deepEqual(await page.locator('.mrn-ch').allInnerTexts(),['World & geopolitics 1','Science & mathematics 1','Ideas, history & culture 1']);
 assert.equal(await page.locator('.mrn-top').count(),0);
 // AI-written cards carry the AI mark and no article link; originals keep theirs.
 const aiCard=page.locator('.mrn-entry',{hasText:'A consequential development'});
 assert.equal(await aiCard.locator('.mrn-ai-mark').count(),1);
 assert.equal(await aiCard.locator('.mrn-url').count(),0);
 const origCard=page.locator('.mrn-entry',{hasText:'Why some explanations survive'});
 assert.equal(await origCard.locator('.mrn-ai-mark').count(),0);
 assert.equal(await origCard.locator('.mrn-url').count(),1);
 assert.equal(await page.locator('.mrn-exp').count(),0);
 assert.equal(await page.locator('.mrn-entry',{hasText:'urban ecosystems'}).locator('.mrn-ex-mark').count(),1);
 assert.equal(await page.locator('.mrn-ex-mark').count(),1);
 await page.locator('.mrn-chips button',{hasText:'Science'}).click();
 assert.equal(await page.locator('.mrn-entry').count(),1);
 await page.locator('.mrn-chips button',{hasText:'All'}).click();
 assert.equal(await page.locator('.mrn-entry').count(),3);
 // Feedback lives in the Details panel and persists.
 const first=page.locator('.mrn-entry').first();
 await first.getByRole('button',{name:'Details'}).click();
 const dialog=page.getByRole('dialog');
 await dialog.getByText('AI summary',{exact:true}).waitFor();
 await dialog.getByLabel('Worth the time?').selectOption('yes');
 await dialog.getByText('Saved',{exact:true}).waitFor();
 await page.keyboard.press('Escape');assert.equal(await page.getByRole('dialog').count(),0);
 await page.reload();await page.locator('.mrn-entry').first().getByRole('button',{name:'Details'}).click();
 assert.equal(await page.getByRole('dialog').getByLabel('Worth the time?').inputValue(),'yes');
 await page.getByRole('dialog').getByRole('button',{name:'Close'}).click();
 await page.getByRole('button',{name:'Mark as read'}).first().click();
 await page.getByRole('button',{name:'Mark as unread'}).first().waitFor();
 await page.getByRole('button',{name:'Preferences',exact:true}).click();await page.getByLabel('Minutes per day').fill('35');await page.getByRole('button',{name:'Save preferences'}).click();
 await page.getByRole('button',{name:'Preferences',exact:true}).click();assert.equal(await page.getByLabel('Minutes per day').inputValue(),'35');await page.getByRole('button',{name:'Preferences',exact:true}).click();
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:'/tmp/morning-read-mobile.png',fullPage:true});
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),'Mobile document overflows');
 assert(await page.locator('.mr-page').evaluate(e=>e.scrollWidth<=e.clientWidth),'Mobile reading content overflows');
 assert.deepEqual(errors,[]);
 await browser.close();console.log('PASS newsletter layout by category, AI marks without article links, no overall ranking or exploration list, compass mark on exploration items, filter chips, ranking, Details feedback saved, read toggle, preferences and no horizontal overflow');
})().catch(e=>{console.error(e);process.exit(1)});
