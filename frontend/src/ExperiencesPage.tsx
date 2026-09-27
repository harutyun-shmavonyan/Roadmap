import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { marked } from 'marked';
import type { ExperienceDto, ExperienceImageDto, ExperienceStatus, SaveExperienceRequest } from './types';
import { api } from './api';

marked.setOptions({ gfm: true, breaks: true });

/**
 * The Experiences tab: what is planned, and what was had. The same list at two moments — an
 * experience moves from one half to the other rather than being copied, which is why the page is
 * one list split by status rather than two lists.
 *
 * Written here as readily as by the agent, so unlike Courses this tab is full CRUD: a plan often
 * starts as a line typed on a phone, and the photos come off that same phone afterwards.
 */

/* ─── pictures ───
   Behind the bearer token, so fetched as blobs and shown from object URLs. A picture's id never
   changes its bytes (a replacement is a new picture), so the cache can be keyed by id alone and
   never needs busting. */

const pictureUrls = new Map<string, Promise<string>>();

function pictureUrl(id: string): Promise<string> {
  let p = pictureUrls.get(id);
  if (!p) {
    p = api.fetchExperienceImageUrl(id);
    pictureUrls.set(id, p);
    p.catch(() => pictureUrls.delete(id)); // a failed fetch must not stick
  }
  return p;
}

function forgetPicture(id: string) {
  pictureUrls.get(id)?.then(URL.revokeObjectURL).catch(() => {});
  pictureUrls.delete(id);
}

function usePicture(id: string | undefined): string | null {
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!id) { setUrl(null); return; }
    let live = true;
    pictureUrl(id).then(u => { if (live) setUrl(u); }).catch(() => { if (live) setUrl(null); });
    return () => { live = false; };
  }, [id]);
  return url;
}

/* ─── dates ─── */

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

function parts(iso: string) {
  const [y, m, d] = iso.split('-').map(Number);
  return { y, m, d };
}

/** "12 Mar 2027", "12–18 Mar 2027", "28 Feb – 3 Mar 2027", "30 Dec 2026 – 2 Jan 2027". */
function when(start: string | null, end: string | null): string | null {
  if (!start) return null;
  const a = parts(start);
  if (!end || end === start) return `${a.d} ${MONTHS[a.m - 1]} ${a.y}`;
  const b = parts(end);
  if (a.y === b.y && a.m === b.m) return `${a.d}–${b.d} ${MONTHS[a.m - 1]} ${a.y}`;
  if (a.y === b.y) return `${a.d} ${MONTHS[a.m - 1]} – ${b.d} ${MONTHS[b.m - 1]} ${a.y}`;
  return `${a.d} ${MONTHS[a.m - 1]} ${a.y} – ${b.d} ${MONTHS[b.m - 1]} ${b.y}`;
}

/** How far off a planned thing is, in words a person uses. */
function howSoon(start: string | null): string | null {
  if (!start) return null;
  const { y, m, d } = parts(start);
  const days = Math.round((Date.UTC(y, m - 1, d) - Date.now()) / 86_400_000);
  if (days < 0) return 'date passed';
  if (days === 0) return 'today';
  if (days === 1) return 'tomorrow';
  if (days < 14) return `in ${days} days`;
  if (days < 60) return `in ${Math.round(days / 7)} weeks`;
  if (days < 730) return `in ${Math.round(days / 30.4)} months`;
  return `in ${Math.round(days / 365)} years`;
}

/* ─── bits ─── */

function StatusPill({ status }: { status: ExperienceStatus }) {
  const look = status === 'done' ? 'done' : 'ready';
  return (
    <span className="crs-pill" style={{ color: `var(--crs-${look}-ink)`, background: `var(--crs-${look}-bg)` }}>
      {status === 'done' ? '✓ done' : 'planned'}
    </span>
  );
}

function Pin() {
  return (
    <svg viewBox="0 0 24 24" width="13" height="13" aria-hidden="true" style={{ flex: '0 0 13px' }}
      fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 21s-6.5-5.6-6.5-11a6.5 6.5 0 0 1 13 0c0 5.4-6.5 11-6.5 11z" /><circle cx="12" cy="10" r="2.4" />
    </svg>
  );
}

function Cal() {
  return (
    <svg viewBox="0 0 24 24" width="13" height="13" aria-hidden="true" style={{ flex: '0 0 13px' }}
      fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3.5" y="5" width="17" height="15.5" rx="2.5" /><path d="M3.5 9.5h17M8 3v4M16 3v4" />
    </svg>
  );
}

/* ─── a card ─── */

function Card({ x, onOpen }: { x: ExperienceDto; onOpen: () => void }) {
  const cover = usePicture(x.images[0]?.id);
  const date = when(x.startDate, x.endDate);
  const soon = x.status === 'planned' ? howSoon(x.startDate) : null;
  return (
    <div className="xp-card" role="button" tabIndex={0} onClick={onOpen}
      onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onOpen(); } }}>
      <div className="xp-cover">
        {cover
          ? <img src={cover} alt={x.images[0]?.caption ?? ''} />
          : <div className="xp-cover-empty" aria-hidden="true">{(x.category ?? x.title).slice(0, 1).toUpperCase()}</div>}
        {x.images.length > 1 && <span className="xp-count">{x.images.length} photos</span>}
      </div>
      <div className="xp-card-body">
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          {x.category && <span className="xp-cat">{x.category}</span>}
          <StatusPill status={x.status} />
        </div>
        <div className="xp-title">{x.title}</div>
        {x.location && <div className="xp-meta"><Pin />{x.location}</div>}
        <div className="xp-meta">
          <Cal />
          {date ?? (x.status === 'planned' ? 'Someday' : 'Undated')}
          {soon && <span className="xp-soon">· {soon}</span>}
        </div>
      </div>
    </div>
  );
}

/* ─── the detail view ─── */

function Gallery({ x, onChanged }: { x: ExperienceDto; onChanged: (next: ExperienceDto) => void }) {
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const upload = async (files: FileList | null) => {
    if (!files || files.length === 0) return;
    setBusy(true); setErr(null);
    try { onChanged(await api.uploadExperienceImages(x.id, [...files])); }
    catch (e) { setErr(String(e)); }
    finally { setBusy(false); if (fileRef.current) fileRef.current.value = ''; }
  };

  const remove = async (img: ExperienceImageDto) => {
    if (!window.confirm('Remove this photo?')) return;
    setBusy(true);
    try {
      await api.deleteExperienceImage(img.id);
      forgetPicture(img.id);
      onChanged({ ...x, images: x.images.filter(i => i.id !== img.id) });
    } finally { setBusy(false); }
  };

  const recaption = async (img: ExperienceImageDto) => {
    const next = window.prompt('Caption', img.caption ?? '');
    if (next === null) return;
    const saved = await api.updateExperienceImage(img.id, { caption: next });
    onChanged({ ...x, images: x.images.map(i => (i.id === img.id ? saved : i)) });
  };

  const makeCover = async (img: ExperienceImageDto) => {
    const low = Math.min(...x.images.map(i => i.sortOrder)) - 1;
    const saved = await api.updateExperienceImage(img.id, { sortOrder: low });
    onChanged({ ...x, images: [saved, ...x.images.filter(i => i.id !== img.id)] });
  };

  return (
    <div style={{ display: 'grid', gap: 10 }}>
      {x.images.length > 0 && (
        <div className="xp-gallery">
          {x.images.map((img, i) => (
            <GalleryItem key={img.id} img={img} cover={i === 0}
              onCaption={() => recaption(img)} onCover={() => makeCover(img)} onRemove={() => remove(img)} />
          ))}
        </div>
      )}
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <input ref={fileRef} type="file" accept="image/*" multiple hidden onChange={e => upload(e.target.files)} />
        <button className="btn btn-sm" disabled={busy} onClick={() => fileRef.current?.click()}>
          {busy ? 'Uploading…' : x.images.length ? '+ Add photos' : '+ Add photos'}
        </button>
        {err && <span style={{ color: 'var(--danger)', fontSize: 12.5 }}>{err}</span>}
      </div>
    </div>
  );
}

function GalleryItem({ img, cover, onCaption, onCover, onRemove }: {
  img: ExperienceImageDto; cover: boolean; onCaption: () => void; onCover: () => void; onRemove: () => void;
}) {
  const url = usePicture(img.id);
  return (
    <figure className="xp-figure">
      <div className="xp-figure-img">
        {url ? <img src={url} alt={img.caption ?? ''} /> : <div className="crs-skel" style={{ height: '100%' }} />}
        {cover && <span className="xp-count" style={{ left: 8, right: 'auto' }}>cover</span>}
      </div>
      {img.caption && <figcaption>{img.caption}</figcaption>}
      <div className="xp-figure-actions">
        <button className="btn-ghost" onClick={onCaption}>{img.caption ? 'Edit caption' : 'Caption'}</button>
        {!cover && <button className="btn-ghost" onClick={onCover}>Make cover</button>}
        <button className="btn-ghost" style={{ color: 'var(--danger)' }} onClick={onRemove}>Remove</button>
      </div>
    </figure>
  );
}

function Detail({ x, onClose, onEdit, onChanged, onDeleted }: {
  x: ExperienceDto; onClose: () => void; onEdit: () => void;
  onChanged: (next: ExperienceDto) => void; onDeleted: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const date = when(x.startDate, x.endDate);
  const soon = x.status === 'planned' ? howSoon(x.startDate) : null;

  const flip = async () => {
    setBusy(true);
    try { onChanged(await api.setExperienceStatus(x.id, x.status === 'done' ? 'planned' : 'done')); }
    finally { setBusy(false); }
  };

  const remove = async () => {
    if (!window.confirm(`Delete "${x.title}" and its ${x.images.length} photo(s)? There is no undo.`)) return;
    setBusy(true);
    try { await api.deleteExperience(x.id); x.images.forEach(i => forgetPicture(i.id)); onDeleted(); }
    finally { setBusy(false); }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal xp-modal" onClick={e => e.stopPropagation()} role="dialog" aria-label={x.title}>
        <div style={{ display: 'grid', gap: 8 }}>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
            {x.category && <span className="xp-cat">{x.category}</span>}
            <StatusPill status={x.status} />
          </div>
          <h2 style={{ margin: 0, fontSize: 22, letterSpacing: '-.015em', color: 'var(--text-primary)' }}>{x.title}</h2>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            {x.location && <span className="xp-meta"><Pin />{x.location}</span>}
            <span className="xp-meta"><Cal />{date ?? (x.status === 'planned' ? 'Someday' : 'Undated')}
              {soon && <span className="xp-soon">· {soon}</span>}</span>
          </div>
        </div>

        <Gallery x={x} onChanged={onChanged} />

        {x.descriptionMd
          ? <div className="crs-md xp-md" dangerouslySetInnerHTML={{ __html: marked.parse(x.descriptionMd) as string }} />
          : <p style={{ color: 'var(--text-secondary)', fontSize: 14, margin: 0 }}>No description yet.</p>}

        <div className="modal-actions" style={{ justifyContent: 'space-between' }}>
          <button className="btn btn-sm" style={{ color: 'var(--danger)' }} disabled={busy} onClick={remove}>Delete</button>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-sm" onClick={onClose}>Close</button>
            <button className="btn btn-sm" onClick={onEdit}>Edit</button>
            <button className={`btn btn-sm ${x.status === 'planned' ? 'btn-accent' : ''}`} disabled={busy} onClick={flip}>
              {x.status === 'planned' ? '✓ Mark done' : 'Back to planned'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ─── the form ─── */

function Form({ x, categories, onCancel, onSaved }: {
  x: ExperienceDto | null; categories: string[];
  onCancel: () => void; onSaved: (saved: ExperienceDto) => void;
}) {
  const [title, setTitle] = useState(x?.title ?? '');
  const [category, setCategory] = useState(x?.category ?? '');
  const [status, setStatus] = useState<ExperienceStatus>(x?.status ?? 'planned');
  const [location, setLocation] = useState(x?.location ?? '');
  const [start, setStart] = useState(x?.startDate ?? '');
  const [end, setEnd] = useState(x?.endDate ?? '');
  const [description, setDescription] = useState(x?.descriptionMd ?? '');
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const save = async () => {
    if (!title.trim()) { setErr('A title is the one thing it needs.'); return; }
    if (end && !start) { setErr('An end date needs a start date.'); return; }
    if (start && end && end < start) { setErr('The end date is before the start date.'); return; }
    setBusy(true); setErr(null);
    const body: SaveExperienceRequest = {
      title: title.trim(), category: category.trim() || null, status,
      location: location.trim() || null, startDate: start || null, endDate: end || null,
      descriptionMd: description.trim() ? description : null,
    };
    try {
      let saved = x ? await api.updateExperience(x.id, body) : await api.createExperience(body);
      // A new experience has no id until it is saved, so its first photos go up straight after.
      if (files.length) saved = await api.uploadExperienceImages(saved.id, files);
      onSaved(saved);
    } catch (e) { setErr(String(e)); setBusy(false); }
  };

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div className="modal xp-modal" onClick={e => e.stopPropagation()} role="dialog"
        aria-label={x ? 'Edit experience' : 'New experience'}>
        <h2 style={{ margin: 0, fontSize: 19 }}>{x ? 'Edit experience' : 'New experience'}</h2>

        <label className="xp-field">
          <span>Title</span>
          <input className="inline-input" autoFocus value={title} onChange={e => setTitle(e.target.value)}
            placeholder="See the northern lights in Tromsø" />
        </label>

        <div className="xp-row">
          <label className="xp-field">
            <span>Category</span>
            <input className="inline-input" list="xp-categories" value={category}
              onChange={e => setCategory(e.target.value)} placeholder="Travel" />
            <datalist id="xp-categories">{categories.map(c => <option key={c} value={c} />)}</datalist>
          </label>
          <div className="xp-field">
            <span>Status</span>
            <div className="xp-seg" role="radiogroup" aria-label="Status">
              {(['planned', 'done'] as ExperienceStatus[]).map(s => (
                <button key={s} type="button" role="radio" aria-checked={status === s}
                  className={status === s ? 'on' : ''} onClick={() => setStatus(s)}>
                  {s === 'done' ? '✓ Done' : 'Planned'}
                </button>
              ))}
            </div>
          </div>
        </div>

        <label className="xp-field">
          <span>Location</span>
          <input className="inline-input" value={location} onChange={e => setLocation(e.target.value)}
            placeholder="Tromsø, Norway" />
        </label>

        <div className="xp-row">
          <label className="xp-field">
            <span>From</span>
            <input className="inline-input" type="date" value={start} onChange={e => setStart(e.target.value)} />
          </label>
          <label className="xp-field">
            <span>To <em style={{ fontWeight: 400, textTransform: 'none', letterSpacing: 0 }}>(optional)</em></span>
            <input className="inline-input" type="date" value={end} min={start || undefined}
              onChange={e => setEnd(e.target.value)} />
          </label>
        </div>

        <label className="xp-field">
          <span>Description <em style={{ fontWeight: 400, textTransform: 'none', letterSpacing: 0 }}>(Markdown)</em></span>
          <textarea className="inline-input" rows={7} value={description} onChange={e => setDescription(e.target.value)}
            placeholder="What it is, why you want it — or what it was like." style={{ resize: 'vertical' }} />
        </label>

        {!x && (
          <label className="xp-field">
            <span>Photos <em style={{ fontWeight: 400, textTransform: 'none', letterSpacing: 0 }}>(optional)</em></span>
            <input type="file" accept="image/*" multiple onChange={e => setFiles([...(e.target.files ?? [])])} />
          </label>
        )}

        {err && <div role="alert" style={{ color: 'var(--danger)', fontSize: 13 }}>{err}</div>}

        <div className="modal-actions">
          <button className="btn" onClick={onCancel} disabled={busy}>Cancel</button>
          <button className="btn btn-accent" onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Save'}</button>
        </div>
      </div>
    </div>
  );
}

/* ─── the page ─── */

type Filter = 'all' | ExperienceStatus;

export function ExperiencesPage() {
  const [items, setItems] = useState<ExperienceDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<Filter>('all');
  const [category, setCategory] = useState<string | null>(null);
  const [open, setOpen] = useState<string | null>(null);
  const [editing, setEditing] = useState<ExperienceDto | 'new' | null>(null);

  const load = useCallback(() => {
    api.getExperiences().then(setItems).catch(e => setError(String(e)));
  }, []);
  useEffect(load, [load]);

  // Categories come from the list itself, most used first, so the chips and the form's
  // suggestions are always exactly what is in use.
  const categories = useMemo(() => {
    const counts = new Map<string, number>();
    (items ?? []).forEach(x => { if (x.category) counts.set(x.category, (counts.get(x.category) ?? 0) + 1); });
    return [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).map(([c]) => c);
  }, [items]);

  const shown = (items ?? []).filter(x =>
    (filter === 'all' || x.status === filter) && (!category || x.category === category));
  const planned = shown.filter(x => x.status === 'planned');
  const done = shown.filter(x => x.status === 'done');
  const opened = items?.find(x => x.id === open) ?? null;

  const replace = (next: ExperienceDto) =>
    setItems(prev => {
      const list = prev ?? [];
      return list.some(x => x.id === next.id) ? list.map(x => (x.id === next.id ? next : x)) : [next, ...list];
    });

  if (error) return <p style={{ padding: 24, color: 'var(--danger)' }}>{error}</p>;

  const total = items?.length ?? 0;
  const plannedCount = (items ?? []).filter(x => x.status === 'planned').length;

  return (
    <div style={{ height: '100%', overflowY: 'auto', WebkitOverflowScrolling: 'touch' }}>
      <div style={{ padding: 24, display: 'grid', gap: 18, maxWidth: 1180 }}>
        <div style={{ display: 'flex', gap: 14, alignItems: 'center', flexWrap: 'wrap' }}>
          <div style={{ flex: 1, minWidth: 200 }}>
            <h2 style={{ margin: 0, fontSize: 22, letterSpacing: '-.015em', color: 'var(--text-primary)' }}>Experiences</h2>
            {items && (
              <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 3 }}>
                {plannedCount} planned · {total - plannedCount} done
              </div>
            )}
          </div>
          <button className="btn btn-accent" onClick={() => setEditing('new')}>+ New experience</button>
        </div>

        {items && total > 0 && (
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
            <div className="xp-seg" role="radiogroup" aria-label="Show">
              {(['all', 'planned', 'done'] as Filter[]).map(f => (
                <button key={f} role="radio" aria-checked={filter === f}
                  className={filter === f ? 'on' : ''} onClick={() => setFilter(f)}>
                  {f === 'all' ? 'All' : f === 'planned' ? 'Planned' : 'Done'}
                </button>
              ))}
            </div>
            {categories.length > 0 && <span style={{ width: 1, height: 22, background: 'var(--border-subtle)', margin: '0 4px' }} />}
            {categories.map(c => (
              <button key={c} className={`xp-chip ${category === c ? 'on' : ''}`}
                aria-pressed={category === c} onClick={() => setCategory(category === c ? null : c)}>
                {c}
              </button>
            ))}
          </div>
        )}

        {!items && (
          <div className="xp-grid">{[0, 1, 2, 3].map(i => <div key={i} className="crs-skel" style={{ height: 292, borderRadius: 14 }} />)}</div>
        )}

        {items && total === 0 && (
          <div style={{ padding: '28px 4px', maxWidth: 520, color: 'var(--text-secondary)', fontSize: 14, lineHeight: 1.7 }}>
            <div style={{ fontSize: 16, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 6 }}>Nothing here yet</div>
            Add something you want to do — a trip, a concert, a climb — even if it is just a line for now.
            The place, the dates and the photos can come later, and when it has happened, mark it done and
            it moves to the record.
          </div>
        )}

        {items && total > 0 && shown.length === 0 && (
          <p style={{ color: 'var(--text-secondary)', fontSize: 14 }}>Nothing matches that filter.</p>
        )}

        {planned.length > 0 && (
          <section style={{ display: 'grid', gap: 12 }}>
            {filter === 'all' && <h3 className="xp-h">Planned <span>{planned.length}</span></h3>}
            <div className="xp-grid">{planned.map(x => <Card key={x.id} x={x} onOpen={() => setOpen(x.id)} />)}</div>
          </section>
        )}

        {done.length > 0 && (
          <section style={{ display: 'grid', gap: 12 }}>
            {filter === 'all' && <h3 className="xp-h">Done <span>{done.length}</span></h3>}
            <div className="xp-grid">{done.map(x => <Card key={x.id} x={x} onOpen={() => setOpen(x.id)} />)}</div>
          </section>
        )}
      </div>

      {opened && editing === null && (
        <Detail x={opened} onClose={() => setOpen(null)} onEdit={() => setEditing(opened)}
          onChanged={replace}
          onDeleted={() => { setItems(prev => (prev ?? []).filter(x => x.id !== opened.id)); setOpen(null); }} />
      )}

      {editing !== null && (
        <Form x={editing === 'new' ? null : editing} categories={categories}
          onCancel={() => setEditing(null)}
          onSaved={saved => { replace(saved); setEditing(null); setOpen(saved.id); load(); }} />
      )}
    </div>
  );
}
