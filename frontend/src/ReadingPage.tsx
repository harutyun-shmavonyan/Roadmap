import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent } from 'react';
import { api } from './api';
import './reading.css';

export interface ReadingFeedback { worthTime?: string; understanding?: string; worthDiscovering?: string; direction?: string; note?: string }
export interface ReadingItem {
  id: string; contentKey: string;
  article: { title: string; url: string; publisher: string; author: string; access: string; format: string; section: string;
    whyRead: string; learningGain: string; summary: string; originalValue: string; durability: string; uncertainty: string;
    readMinutes: number; imageUrl?: string; imageCredit?: string; publishedDate?: string; explorationKind?: string; thinker?: string;
    scores: Record<string, {value: number | null; reason: string}>; topics: string[]; sources: {title: string; url: string}[]; model?: string; rubricVersion: string;
    category?: string | null; score?: number | null };
}
export interface ReadingEdition { id: string; issueDate: string; title: string; revision: number; isRead: boolean; coveredFrom: string; coveredUntil: string; itemCount: number }
export interface ReadingDetail { edition: ReadingEdition; editorNote: string; items: ReadingItem[]; feedback: {contentKey: string; response: ReadingFeedback}[] }
export interface ReadingSettings { dailyMinutes: number; explorationPercent: number; preferences: string }

// The edition is laid out like the AI Engineering newsletter: one section per category in this
// order, chips to filter, an overall ranking, score badges and a Details panel. The keys are
// mirrored by ReadingService.Categories on the server; a new one is a change in both places.
// key, section heading, chip label.
const CATEGORIES: [string, string, string][] = [
  ['world', 'World & geopolitics', 'World'],
  ['economy', 'Economy & markets', 'Economy'],
  ['climate', 'Climate & energy', 'Climate & energy'],
  ['tech', 'Technology & infrastructure', 'Technology'],
  ['science', 'Science & mathematics', 'Science'],
  ['health', 'Health & mind', 'Health'],
  ['society', 'Society, law & cities', 'Society'],
  ['ideas', 'Ideas, history & culture', 'Ideas & culture'],
  ['other', 'Other', 'Other'], // items saved before categories existed
];
const CAT = Object.fromEntries(CATEGORIES.map(([k, h, c]) => [k, {heading: h, chip: c}]));
const TOP_N = 10;
const SECTION: Record<string, string> = {now: 'Now', depth: 'Depth', exploration: 'Exploration'};
const FORMAT: Record<string, string> = {original: 'Original article', summary: 'AI summary', synthesis: 'AI synthesis'};
const scoreLabels: Record<string, string> = {substance: 'Substance', evidence: 'Evidence', depth: 'Explanatory depth', learning: 'New understanding', durability: 'Durability', originalValue: 'Original-reading value', consequence: 'Consequence', distance: 'Unfamiliarity', expansion: 'Perspective expansion'};

const safeUrl = (url?: string) => url && /^https?:\/\//i.test(url) ? url : undefined;
const catOf = (i: ReadingItem) => i.article.category && CAT[i.article.category] ? i.article.category : 'other';
const band = (s?: number | null) => s == null ? 'b6' : s >= 90 ? 'b9' : s >= 80 ? 'b8' : s >= 70 ? 'b7' : 'b6';
const byScore = (a: ReadingItem, b: ReadingItem) => (b.article.score ?? -1) - (a.article.score ?? -1) || a.article.title.localeCompare(b.article.title);
const dayOf = (d: string) => new Date(`${d.slice(0, 10)}T12:00:00Z`);
const fmtDay = (d: string) => dayOf(d).toLocaleDateString('en-GB', {timeZone: 'UTC', day: 'numeric', month: 'short'});
const fmtLong = (d: string) => dayOf(d).toLocaleDateString('en-US', {timeZone: 'UTC', weekday: 'long', month: 'long', day: 'numeric', year: 'numeric'});
const fmtShort = (s: string) => new Date(s).toLocaleDateString('en-GB', {timeZone: 'Asia/Yerevan', day: 'numeric', month: 'short'});
const fmtUtc = (s: string) => { const d = new Date(s); return `${d.toISOString().slice(0, 10)} ${d.toISOString().slice(11, 16)} UTC`; };

function Feedback({item, feedback, onSaved}: {item: ReadingItem; feedback: ReadingFeedback; onSaved: (f: ReadingFeedback) => void}) {
  const [note, setNote] = useState(feedback.note ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);
  const save = async (patch: ReadingFeedback) => {
    setBusy(true); setError(''); setSaved(false);
    try { onSaved(await api.readingFeedback(item.id, patch)); setSaved(true); }
    catch (e) { setError(String(e)); }
    finally { setBusy(false); }
  };
  const select = (label: string, field: keyof ReadingFeedback, options: [string, string][]) => <label className="mrn-field">{label}
    <select disabled={busy} value={feedback[field] ?? ''} onChange={e => void save({[field]: e.target.value})}>
      <option value="">Not rated</option>{options.map(([value, text]) => <option key={value} value={value}>{text}</option>)}
    </select></label>;
  return <div className="mrn-fb">
    <b className="mrn-dl">Your feedback</b>
    <div className="mrn-fb-grid">
      {select('Worth the time?', 'worthTime', [['no', 'No'], ['somewhat', 'Somewhat'], ['yes', 'Yes']])}
      {select('Added understanding?', 'understanding', [['known', 'Already knew'], ['facts', 'New facts'], ['explanation', 'New explanation'], ['changed', 'Changed my view']])}
      {item.article.section === 'exploration' && select('Worth discovering?', 'worthDiscovering', [['no', 'No'], ['somewhat', 'Somewhat'], ['yes', 'Yes']])}
      {select('Future reading', 'direction', [['deeper', 'Go deeper'], ['less-topic', 'Less of this topic'], ['less-source', 'Less from this source'], ['less-depth', 'Shorter / less depth']])}
    </div>
    <label className="mrn-field">What stayed with you, or felt empty?<textarea value={note} maxLength={4000} onChange={e => { setNote(e.target.value); setSaved(false); }} /></label>
    <div className="mrn-fb-row"><button className="btn btn-sm" disabled={busy} onClick={() => void save({note})}>{busy ? 'Saving…' : 'Save note'}</button>{saved && <span>Saved</span>}</div>
    {error && <p role="alert" className="mrn-fb-err">{error}</p>}
  </div>;
}

function Details({item, feedback, onSaved, onClose}: {item: ReadingItem; feedback: ReadingFeedback; onSaved: (f: ReadingFeedback) => void; onClose: () => void}) {
  const a = item.article;
  const close = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    close.current?.focus();
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', key);
    return () => window.removeEventListener('keydown', key);
  }, [onClose]);
  const generated = a.format === 'original' ? 'AI-written preview · the original is the read' : a.format === 'synthesis' ? 'AI synthesis · generated from the sources below' : 'AI summary · generated from the source below';
  const paras = (a.summary || '').split(/\n{2,}/).map(p => p.trim()).filter(Boolean);
  const scores = Object.entries(a.scores);
  return <div className="mrn-modal" role="dialog" aria-modal="true" aria-label={a.title}>
    <div className="mrn-mback" onClick={onClose} />
    <div className="mrn-mbox">
      <button ref={close} className="mrn-mclose" aria-label="Close" onClick={onClose}>&times;</button>
      <a className="mrn-mtitle" href={safeUrl(a.url)} target="_blank" rel="noopener noreferrer">{a.title}&nbsp;&rarr;</a>
      <p className="mrn-mlead">{a.whyRead}</p>
      {paras.length > 0 && <div className="mrn-dg"><b className="mrn-dl">{generated}</b>{paras.map((p, i) => <p key={i}>{p}</p>)}
        {a.format === 'original' && <a className="mrn-orig" href={safeUrl(a.url)} target="_blank" rel="noopener noreferrer">Read the original ↗</a>}</div>}
      <b className="mrn-dl">Key facts</b>
      <div className="mrn-s"><b>What a summary would lose</b><p>{a.originalValue}</p></div>
      {a.uncertainty && <div className="mrn-s sc"><b>Caveats</b><p>{a.uncertainty}</p></div>}
      {scores.length > 0 && <div className="mrn-s sn"><b>Why it scored {a.score ?? '—'}</b><ul>{scores.map(([k, s]) =>
        <li key={k}><span className="mrn-dim">{scoreLabels[k] ?? k} {s.value === null ? 'unknown' : `${s.value}/5`}</span> — {s.reason}</li>)}</ul></div>}
      {a.thinker && <div className="mrn-s"><b>Mind to revisit</b><p>{a.thinker}</p></div>}
      <div className="mrn-s"><b>Sources</b><ul>{a.sources.map((s, i) => <li key={i}><a href={safeUrl(s.url)} target="_blank" rel="noopener noreferrer">{s.title} ↗</a></li>)}</ul></div>
      <p className="mrn-note">The score and the nine dimensions are the editing agent's estimates, not verified ratings. Useful for {a.durability} · {a.topics.join(', ')} · rubric {a.rubricVersion}{a.model ? ` · ${a.model}` : ''}{a.access !== 'full' ? ' · evaluated from an excerpt only' : ''}.</p>
      <Feedback item={item} feedback={feedback} onSaved={onSaved} />
    </div>
  </div>;
}

function Card({n, item, rated, onOpen}: {n: number; item: ReadingItem; rated: boolean; onOpen: () => void}) {
  const a = item.article;
  const date = a.publishedDate ? fmtDay(a.publishedDate) : null;
  return <article className="mrn-entry" id={`mrn-i${n}`}>
    <div className="mrn-badge-col"><div className={`mrn-badge ${band(a.score)}`}>{a.score ?? '–'}</div></div>
    <div className="mrn-content">
      <a className="mrn-title" href={safeUrl(a.url)} target="_blank" rel="noopener noreferrer">{a.title}</a>
      <div className="mrn-pills">
        <span className={`mrn-pill p-${a.section}`}>{SECTION[a.section] ?? a.section}{a.explorationKind ? ` · ${a.explorationKind}` : ''}</span>
        <span className={`mrn-pill ${a.format === 'original' ? 'p-original' : 'p-gen'}`}>{FORMAT[a.format] ?? a.format}</span>
      </div>
      <span className="mrn-lbl">Summary</span><p className="mrn-summary-text">{a.whyRead}</p>
      <div className="mrn-learn"><span className="mrn-lbl">What you'll learn</span><p>{a.learningGain}</p></div>
      <a className="mrn-url" href={safeUrl(a.url)} target="_blank" rel="noopener noreferrer">{a.url}</a>
      <div className="mrn-mrow">
        <span className="mrn-meta">{a.publisher}{date && <> &middot; {date}</>} &middot; {a.readMinutes} min{rated && <> &middot; ✓ rated</>}</span>
        <button className="mrn-dbtn" onClick={onOpen}>Details</button>
      </div>
    </div>
  </article>;
}

function Edition({detail, busy, onToggleRead, budget}: {detail: ReadingDetail; busy: boolean; onToggleRead: () => void; budget?: number}) {
  const [filter, setFilter] = useState('all');
  const [open, setOpen] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Record<string, ReadingFeedback>>(() => Object.fromEntries(detail.feedback.map(f => [f.contentKey, f.response])));
  useEffect(() => { setFilter('all'); setOpen(null); setFeedback(Object.fromEntries(detail.feedback.map(f => [f.contentKey, f.response]))); }, [detail]);

  // Category-major, each category by score — the newsletter's order, numbered for the rows.
  const {shown, numbered, ordered} = useMemo(() => {
    const groups = CATEGORIES.map(([k]) => [k, detail.items.filter(i => catOf(i) === k).sort(byScore)] as const).filter(([, l]) => l.length);
    const numbered = new Map<string, number>(); let n = 0;
    for (const [, l] of groups) for (const i of l) numbered.set(i.id, ++n);
    return {shown: groups, numbered, ordered: groups.flatMap(([, l]) => l)};
  }, [detail]);
  const top = [...ordered].sort(byScore).slice(0, TOP_N);
  const exploration = ordered.filter(i => i.article.section === 'exploration').sort(byScore);
  const minutes = detail.items.reduce((s, i) => s + i.article.readMinutes, 0);
  const e = detail.edition;
  const jump = (id: string) => (ev: MouseEvent) => { ev.preventDefault(); document.getElementById(`mrn-i${numbered.get(id)}`)?.scrollIntoView({behavior: 'smooth', block: 'start'}); };
  const rows = (title: string, cls: string, list: ReadingItem[]) => <section className={`mrn-overview ${cls}`}><h2>{title}</h2><ol className="mrn-rows">
    {list.map(i => <li key={i.id}><a href={`#mrn-i${numbered.get(i.id)}`} onClick={jump(i.id)}><span className={`mrn-mb ${band(i.article.score)}`}>{i.article.score ?? '–'}</span>
      <span className="mrn-rt">{i.article.title}</span><span className="mrn-rc">{CAT[catOf(i)].chip}</span></a></li>)}</ol></section>;
  const openItem = detail.items.find(i => i.id === open);
  const close = useCallback(() => setOpen(null), []);

  return <div className="mrn"><div className="mrn-page">
    <header className="mrn-header"><div className="mrn-eyebrow">Morning Read</div><h1 className="mrn-date">{fmtLong(e.issueDate)}</h1>
      <div className="mrn-window">Covering {fmtShort(e.coveredFrom)} &rarr; {fmtUtc(e.coveredUntil)} &middot; {ordered.length} {ordered.length === 1 ? 'item' : 'items'} in {shown.length} {shown.length === 1 ? 'category' : 'categories'} &middot; {minutes} min</div></header>
    {ordered.length === 0 ? <p className="mrn-empty-cat">Nothing cleared the bar for this edition. {detail.editorNote}</p> : <>
      <nav className="mrn-chips" aria-label="Filter by category">
        <button aria-pressed={filter === 'all'} onClick={() => setFilter('all')}>All <span>{ordered.length}</span></button>
        {shown.map(([k, l]) => <button key={k} aria-pressed={filter === k} onClick={() => setFilter(k)}>{CAT[k].chip} <span>{l.length}</span></button>)}
      </nav>
      {filter === 'all' && <>
        {rows(ordered.length >= TOP_N ? `Top ${TOP_N} overall` : 'Overall ranking', 'mrn-top', top)}
        {exploration.length > 0 && rows('🧭 Exploration', 'mrn-exp', exploration)}
      </>}
      {shown.filter(([k]) => filter === 'all' || filter === k).map(([k, l]) => <section key={k} id={`mrn-c-${k}`}>
        <h2 className="mrn-ch">{CAT[k].heading} <span>{l.length}</span></h2>
        {l.map(i => <Card key={i.id} n={numbered.get(i.id)!} item={i} rated={!!feedback[i.contentKey] && Object.values(feedback[i.contentKey]).some(Boolean)} onOpen={() => setOpen(i.id)} />)}
      </section>)}
    </>}
    <footer className="mrn-footer">
      {detail.editorNote && ordered.length > 0 && <p><strong>Editor's note.</strong> {detail.editorNote}</p>}
      <p><strong>Scoring.</strong> Every item scored 0-100 by the editor's judgment on one shared scale (90+ do not miss, 80s high value, 70s worth reading, 60s context), then ranked within its category. The score is a judgment of value per minute, not an average; the nine dimensions behind it and their reasons are in each Details panel.</p>
      {shown.length > 0 && <p><strong>Categories.</strong> {shown.map(([k, l]) => `${CAT[k].heading} ${l.length}`).join(', ')}.</p>}
      <p><strong>Provenance.</strong> Original articles are recommended for reading in full. AI summaries and syntheses are generated from the linked sources and labelled as such.</p>
      <p><strong>Reading time.</strong> {minutes} min{budget ? ` of a ${budget}-minute daily budget` : ''}.</p>
      <p><strong>Window.</strong> {fmtShort(e.coveredFrom)} &rarr; {fmtUtc(e.coveredUntil)}. Marking this edition read sets where the next news scan begins. It does not rate its articles.</p>
      <div className="mrn-done"><span>{e.isRead ? '✓ You read this edition.' : 'You’re caught up with this edition.'}</span>
        <button className={`btn btn-sm ${e.isRead ? '' : 'btn-accent'}`} disabled={busy} onClick={onToggleRead}>{e.isRead ? 'Mark as unread' : '✓ Mark as read'}</button></div>
    </footer>
    {openItem && <Details item={openItem} feedback={feedback[openItem.contentKey] ?? {}} onClose={close}
      onSaved={f => setFeedback(prev => ({...prev, [openItem.contentKey]: f}))} />}
  </div></div>;
}

export function ReadingPage() {
  const [editions, setEditions] = useState<ReadingEdition[]>([]);
  const [selected, setSelected] = useState('');
  const [detail, setDetail] = useState<ReadingDetail | null>(null);
  const [settings, setSettings] = useState<ReadingSettings | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [settingsOpen, setSettingsOpen] = useState(false);
  const generation = useRef(0);
  const load = useCallback(async () => {
    const [rows, context] = await Promise.all([api.readingEditions(), api.readingContext()]);
    setEditions(rows); setSettings(context.settings);
    setSelected(prev => rows.some(r => r.id === prev) ? prev : rows[0]?.id ?? '');
  }, []);
  useEffect(() => { load().catch(e => setError(String(e))).finally(() => setLoading(false)); }, [load]);
  useEffect(() => {
    const gen = ++generation.current;
    setDetail(null);
    if (selected) api.readingEdition(selected).then(d => { if (gen === generation.current) setDetail(d); }).catch(e => { if (gen === generation.current) setError(String(e)); });
    return () => { generation.current++; };
  }, [selected]);
  const toggle = async () => {
    if (!detail) return;
    setBusy(true); setError('');
    try {
      await api.readingMark(detail.edition.id, !detail.edition.isRead, detail.edition.revision);
      setDetail(await api.readingEdition(detail.edition.id));
      await load();
    } catch (e) { setError(String(e)); } finally { setBusy(false); }
  };
  const saveSettings = async () => {
    if (!settings) return;
    setBusy(true); setError('');
    try { setSettings(await api.readingSettings(settings)); setSettingsOpen(false); }
    catch (e) { setError(String(e)); } finally { setBusy(false); }
  };
  const issue = detail?.edition ?? editions.find(e => e.id === selected);

  if (loading) return <div style={{padding: 24, color: 'var(--text-muted)'}}>Loading...</div>;
  return <div className="mr-page">
    <div className="mr-bar">
      <div className="mr-bar-main">
        <div className="mr-bar-title">{issue?.title ?? 'Morning Read'}</div>
        {issue && <div className="mr-bar-sub">
          <span>Covering news from <b>{fmtShort(issue.coveredFrom)}</b> → <b>{fmtUtc(issue.coveredUntil)}</b></span>
          {issue.itemCount > 0 && <><span>·</span><span>{issue.itemCount} items</span></>}
          {issue.isRead && <><span>·</span><span className="mr-read">✓ Read</span></>}
        </div>}
      </div>
      {editions.length > 1 && <select aria-label="Choose edition" value={selected} onChange={e => setSelected(e.target.value)}>
        {editions.map(e => <option key={e.id} value={e.id}>{fmtDay(e.issueDate)} · {e.isRead ? 'Read' : 'Unread'}</option>)}</select>}
      <button className="btn btn-sm" onClick={() => setSettingsOpen(!settingsOpen)}>Preferences</button>
      {detail && <button className={`btn btn-sm ${detail.edition.isRead ? '' : 'btn-accent'}`} disabled={busy} onClick={() => void toggle()}
        title={detail.edition.isRead ? 'Send the next edition back over this ground' : 'Ticking this is where the next edition starts from'}>
        {detail.edition.isRead ? 'Mark as unread' : '✓ Mark as read'}</button>}
    </div>
    {error && <div className="mr-error" role="alert"><span>{error}</span><button className="btn btn-sm" onClick={() => { setError(''); void load().then(async () => { if (selected) setDetail(await api.readingEdition(selected)); }).catch(e => setError(String(e))); }}>Reload</button></div>}
    {settingsOpen && settings && <section className="mr-settings"><h2>Your reading budget</h2>
      <div className="mr-settings-row">
        <label className="mrn-field">Minutes per day<input type="number" min={5} max={120} value={settings.dailyMinutes} onChange={e => setSettings({...settings, dailyMinutes: Number(e.target.value)})} /></label>
        <label className="mrn-field">Weekly exploration %<input type="number" min={20} max={40} value={settings.explorationPercent} onChange={e => setSettings({...settings, explorationPercent: Number(e.target.value)})} /></label>
      </div>
      <button className="btn btn-accent btn-sm" disabled={busy} onClick={() => void saveSettings()}>Save preferences</button>
      <p className="mr-muted">Exploration stays protected. Selection follows the agreed editorial criteria and your article feedback.</p></section>}
    {editions.length === 0 ? <div className="mr-empty"><b>☕ No edition yet</b>
      The Morning Read agent publishes one here: originals worth reading in full, short sourced briefings, and a protected share of exploration, grouped by category and scored like the newsletter. No filler, and no automatic read ticks.
      <p className="mr-muted">No automatic delivery is configured by this tab. Run the Morning Read workflow with your connected agent.</p></div>
      : detail ? <Edition detail={detail} busy={busy} onToggleRead={() => void toggle()} budget={settings?.dailyMinutes} />
      : <div style={{padding: 24, color: 'var(--text-muted)', fontSize: 14}}>Loading edition…</div>}
  </div>;
}
