import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from './api';
import './reading.css';

export interface ReadingFeedback { worthTime?: string; understanding?: string; worthDiscovering?: string; direction?: string; note?: string }
export interface ReadingItem {
  id: string; contentKey: string;
  article: { title: string; url: string; publisher: string; author: string; access: string; format: string; section: string;
    whyRead: string; learningGain: string; summary: string; originalValue: string; durability: string; uncertainty: string;
    readMinutes: number; imageUrl?: string; imageCredit?: string; publishedDate?: string; explorationKind?: string; thinker?: string;
    scores: Record<string, {value: number | null; reason: string}>; topics: string[]; sources: {title: string; url: string}[]; model?: string; rubricVersion: string };
}
export interface ReadingEdition { id: string; issueDate: string; title: string; revision: number; isRead: boolean; coveredFrom: string; coveredUntil: string; itemCount: number }
export interface ReadingDetail { edition: ReadingEdition; editorNote: string; items: ReadingItem[]; feedback: {contentKey: string; response: ReadingFeedback}[] }
export interface ReadingSettings { dailyMinutes: number; explorationPercent: number; preferences: string }
const sections = [
  {id: 'now', title: 'What matters now', description: 'Consequential changes, with the repetition removed.'},
  {id: 'depth', title: 'Understand something deeply', description: 'Ideas and explanations that stay useful.'},
  {id: 'exploration', title: 'Exploration', description: 'Different minds, unfamiliar fields, useful connections.'},
];
const scoreLabels: Record<string, string> = {substance: 'Substance', evidence: 'Evidence', depth: 'Explanatory depth', learning: 'New understanding', durability: 'Durability', originalValue: 'Original-reading value', consequence: 'Consequence', distance: 'Unfamiliarity', expansion: 'Perspective expansion'};
const safeUrl = (url?: string) => url && /^https?:\/\//i.test(url) ? url : undefined;
const fmt = (s: string) => new Date(s).toLocaleDateString('en-GB', {timeZone: 'Asia/Yerevan', day: 'numeric', month: 'short'});
function ArticleCard({item, initial}: {item: ReadingItem; initial?: ReadingFeedback}) {
  const a = item.article;
  const [feedback, setFeedback] = useState<ReadingFeedback>(initial ?? {});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);
  const [imageFailed, setImageFailed] = useState(false);
  const save = async (patch: ReadingFeedback) => {
    setBusy(true); setError(''); setSaved(false);
    try { setFeedback(await api.readingFeedback(item.id, patch)); setSaved(true); }
    catch(e) { setError(String(e)); }
    finally { setBusy(false); }
  };
  const select = (label: string, field: keyof ReadingFeedback, options: [string, string][]) => <label className="mr-field">{label}
    <select disabled={busy} value={feedback[field] ?? ''} onChange={e => void save({[field]: e.target.value})}>
      <option value="">Not rated</option>{options.map(([value, text]) => <option key={value} value={value}>{text}</option>)}
    </select></label>;
  return <article className="mr-card">
    {a.format === 'original' && safeUrl(a.imageUrl) && !imageFailed && <figure className="mr-image"><img src={a.imageUrl} alt="" loading="lazy" referrerPolicy="no-referrer" onError={() => setImageFailed(true)} /><figcaption>{a.imageCredit}</figcaption></figure>}
    <div className="mr-card-body">
      <div className="mr-meta"><span className={`mr-label mr-${a.format}`}>{a.format === 'original' ? 'Original article' : a.format === 'synthesis' ? 'AI synthesis' : 'AI summary'}</span><span>{a.readMinutes} min</span><span>Useful for {a.durability}</span>{a.explorationKind && <span>{a.explorationKind}</span>}</div>
      <h3>{a.title}</h3>
      <div className="mr-byline">{a.publisher}{a.author && ` · ${a.author}`}{a.publishedDate && ` · ${fmt(a.publishedDate)}`}{a.access !== 'full' && ' · Evaluated from excerpt only'}</div>
      <p className="mr-reason">{a.whyRead}</p>
      <div className="mr-learning"><strong>What you’ll understand</strong><p>{a.learningGain}</p></div>
      {a.summary && <div className="mr-summary"><small>{a.format === 'original' ? 'AI-written preview' : 'Generated from the sources below'}</small><p>{a.summary}</p></div>}
      {a.format === 'original' && <a className="mr-original-link" href={safeUrl(a.url)} target="_blank" rel="noopener noreferrer">Read the original ↗</a>}
      <div className="mr-tags">{a.topics.map(t => <span key={t}>{t}</span>)}</div>
      <details className="mr-details"><summary>Why it earned a place · scores & sources</summary>
        <p><strong>What a summary would lose:</strong> {a.originalValue}</p>
        <p className="mr-muted">Scores are agent estimates, not independently verified ratings. Rubric {a.rubricVersion}{a.model ? ` · ${a.model}` : ''}.</p>
        <dl className="mr-scores">{Object.entries(a.scores).map(([key, score]) => <div key={key}><dt>{scoreLabels[key] ?? key}<b>{score.value === null ? 'Unknown' : `${score.value}/5`}</b></dt><dd>{score.reason}</dd></div>)}</dl>
        {a.uncertainty && <p><strong>Uncertainty:</strong> {a.uncertainty}</p>}
        {a.thinker && <p><strong>Mind to revisit:</strong> {a.thinker}</p>}
        <ul>{a.sources.map((s, i) => <li key={i}><a href={safeUrl(s.url)} target="_blank" rel="noopener noreferrer">{s.title} ↗</a></li>)}</ul>
      </details>
      <details className="mr-details"><summary>Your feedback{saved ? ' · Saved' : ''}</summary>
        <div className="mr-feedback">
          {select('Worth the time?', 'worthTime', [['no','No'],['somewhat','Somewhat'],['yes','Yes']])}
          {select('Added understanding?', 'understanding', [['known','Already knew'],['facts','New facts'],['explanation','New explanation'],['changed','Changed my view']])}
          {a.section === 'exploration' && select('Worth discovering?', 'worthDiscovering', [['no','No'],['somewhat','Somewhat'],['yes','Yes']])}
          {select('Future reading', 'direction', [['deeper','Go deeper'],['less-topic','Less of this topic'],['less-source','Less from this source'],['less-depth','Shorter / less depth']])}
        </div>
        <label className="mr-field">What stayed with you—or felt empty?<textarea value={feedback.note ?? ''} maxLength={4000} onChange={e => {setFeedback({...feedback, note: e.target.value}); setSaved(false);}} /></label>
        <button className="btn btn-sm" disabled={busy} onClick={() => void save({note: feedback.note ?? ''})}>{busy ? 'Saving…' : 'Save note'}</button>
        {error && <p role="alert" className="mr-error">{error}</p>}
      </details>
    </div>
  </article>;
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
  useEffect(() => {load().catch(e => setError(String(e))).finally(() => setLoading(false));}, [load]);
  useEffect(() => {
    const gen = ++generation.current;
    setDetail(null);
    if (selected) api.readingEdition(selected).then(d => {if (gen === generation.current) setDetail(d);}).catch(e => {if (gen === generation.current) setError(String(e));});
    return () => { generation.current++; };
  }, [selected]);
  const toggle = async () => {
    if (!detail) return;
    setBusy(true); setError('');
    try {
      await api.readingMark(detail.edition.id, !detail.edition.isRead, detail.edition.revision);
      setDetail(await api.readingEdition(detail.edition.id));
      await load();
    } catch(e) {setError(String(e));} finally {setBusy(false);}
  };
  const saveSettings = async () => {
    if (!settings) return;
    setBusy(true); setError('');
    try {setSettings(await api.readingSettings(settings)); setSettingsOpen(false);}
    catch(e) {setError(String(e));} finally {setBusy(false);}
  };
  return <div className="mr-page">
    <header className="mr-header"><div><span className="mr-eyebrow">YOUR DAILY READING EDITION</span><h1>Morning Read</h1><p>Understand what matters. Discover what you’d otherwise miss.</p></div>
      <button className="btn btn-sm" onClick={() => setSettingsOpen(!settingsOpen)}>Reading preferences</button>
    </header>
    {error && <div className="mr-error" role="alert">{error} <button className="btn btn-sm" onClick={() => {setError(''); void load().then(async () => {if(selected) setDetail(await api.readingEdition(selected));}).catch(e => setError(String(e)));}}>Reload</button></div>}
    {settingsOpen && settings && <section className="mr-settings"><h2>Your reading budget</h2><div className="mr-feedback"><label className="mr-field">Minutes per day<input type="number" min={5} max={120} value={settings.dailyMinutes} onChange={e => setSettings({...settings, dailyMinutes: Number(e.target.value)})} /></label><label className="mr-field">Weekly exploration %<input type="number" min={20} max={40} value={settings.explorationPercent} onChange={e => setSettings({...settings, explorationPercent: Number(e.target.value)})}/></label></div><button className="btn btn-accent" disabled={busy} onClick={() => void saveSettings()}>Save preferences</button><p className="mr-muted">Exploration stays protected. Selection follows the agreed editorial criteria and your article feedback.</p></section>}
    {loading ? <p className="mr-empty">Loading your reading…</p> : editions.length === 0 ? <section className="mr-empty"><h2>Your first edition is ahead.</h2><p>Exceptional originals, useful briefings, and a protected space for exploration will appear here when your reading agent publishes.</p><p>No filler. No automatic “read” ticks. Your feedback shapes what comes next.</p><p className="mr-muted">No automatic delivery is configured by this tab. Run the Morning Read workflow with your connected agent.</p></section> : <>
      <div className="mr-toolbar"><label>Edition <select aria-label="Choose edition" value={selected} onChange={e => setSelected(e.target.value)}>{editions.map(e => <option key={e.id} value={e.id}>{fmt(e.issueDate)} · {e.isRead ? 'Read' : 'Unread'}</option>)}</select></label>{detail && <button className={`btn ${detail.edition.isRead ? '' : 'btn-accent'}`} disabled={busy} onClick={() => void toggle()}>{detail.edition.isRead ? 'Mark as unread' : 'Mark as read'}</button>}</div>
      {detail ? <main className="mr-edition"><div className="mr-edition-intro"><h2>{detail.edition.title}</h2><p className="mr-muted">News considered {fmt(detail.edition.coveredFrom)} – {fmt(detail.edition.coveredUntil)} · {detail.items.length} selections · {detail.items.reduce((s, i) => s + i.article.readMinutes, 0)} min{detail.edition.isRead ? ' · Read' : ''}</p>{detail.editorNote && <p>{detail.editorNote}</p>}<p className="mr-muted">Marking this edition read sets where the next news scan begins. It does not rate its articles.</p></div>
      {sections.map(section => {const items = detail.items.filter(i => i.article.section === section.id); return <section className={`mr-section mr-section-${section.id}`} key={section.id}><div className="mr-section-heading"><span>{String(sections.indexOf(section) + 1).padStart(2, '0')}</span><div><h2>{section.title}</h2><p>{section.description}</p></div></div>{items.length ? items.map(item => <ArticleCard key={item.id} item={item} initial={detail.feedback.find(f => f.contentKey === item.contentKey)?.response}/>) : <p className="mr-muted mr-no-items">No selection cleared the bar for this section today.</p>}</section>;})}
      <footer className="mr-footer">You’re caught up with this edition.<button className="btn btn-accent" disabled={busy || detail.edition.isRead} onClick={() => void toggle()}>{detail.edition.isRead ? 'Edition read' : 'Mark as read'}</button></footer></main> : <p className="mr-empty">Loading edition…</p>}
    </>}
  </div>;
}
