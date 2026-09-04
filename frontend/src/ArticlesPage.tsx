import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { marked } from 'marked';
import type { ArticleSummaryDto, ArticleDto, ArticleImageDto, ArticleFormat } from './types';
import { api } from './api';

marked.setOptions({ gfm: true, breaks: true });

// "2026-07-28" -> "28.07.2026"
function fmtDate(iso: string): string {
  const [y, m, d] = iso.split('-');
  return d && m && y ? `${d}.${m}.${y}` : iso;
}

// Injected into the article iframe alongside the height reporter. The height rules kill
// viewport-stretched layouts (html/body at 100%/100vh): with those in place the document's
// scrollHeight tracks the iframe's own height, and the auto-sizer ratchets the frame taller
// forever — the "endless empty space after the article" bug. The paragraph rule is the
// reader's house style: justified body text, browser-hyphenated so the column doesn't get
// rivers. It's appended last so it wins over an article's plain `p` rules but still loses
// to anything the article styles more specifically (classes, inline styles).
const READER_TWEAKS =
  `<style>html,body{height:auto !important;min-height:0 !important}` +
  `p{text-align:justify;hyphens:auto;-webkit-hyphens:auto;overflow-wrap:break-word}</style>`;

// A tiny script injected into the article iframe so it reports its content height back to the
// parent (the iframe is sandboxed to an opaque origin, so this is how we auto-size it).
const HEIGHT_REPORTER =
  `<script>(function(){function p(){try{parent.postMessage({__articleHeight:document.documentElement.scrollHeight},'*')}catch(e){}}` +
  `window.addEventListener('load',p);window.addEventListener('resize',p);document.addEventListener('load',p,true);` +
  `if(window.ResizeObserver){try{new ResizeObserver(p).observe(document.documentElement)}catch(e){}}` +
  `p();setTimeout(p,60);setTimeout(p,300);setTimeout(p,1200);})();<\/script>`;

function withHeightReporter(html: string): string {
  const inject = READER_TWEAKS + HEIGHT_REPORTER;
  return /<\/body>/i.test(html) ? html.replace(/<\/body>/i, inject + '</body>') : html + inject;
}

// Open the self-contained article HTML in a new tab via a blob URL. Stays private (the HTML was
// fetched with the bearer token) — no server route is publicly reachable. Carries the same
// reader style the iframe injects, so the tab reads identically (justified paragraphs).
function openHtmlInNewTab(html: string) {
  const styled = /<\/body>/i.test(html) ? html.replace(/<\/body>/i, READER_TWEAKS + '</body>') : html + READER_TWEAKS;
  const url = URL.createObjectURL(new Blob([styled], { type: 'text/html' }));
  window.open(url, '_blank', 'noopener');
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

// Renders a self-contained HTML article inside a sandboxed iframe that auto-sizes to its content,
// so the surrounding reader pane (not the iframe) scrolls.
function HtmlArticleFrame({ html }: { html: string }) {
  const [height, setHeight] = useState(480);
  const srcDoc = useMemo(() => withHeightReporter(html), [html]);
  useEffect(() => {
    const onMsg = (e: MessageEvent) => {
      const h = e.data && typeof e.data === 'object' ? (e.data as { __articleHeight?: unknown }).__articleHeight : undefined;
      if (typeof h === 'number' && isFinite(h) && h > 0) {
        // Ignore jitter under the slack we add, so the reported height and the height we
        // set can't chase each other upward.
        const nh = Math.ceil(h) + 4;
        setHeight(prev => Math.abs(nh - prev) > 4 ? nh : prev);
      }
    };
    window.addEventListener('message', onMsg);
    return () => window.removeEventListener('message', onMsg);
  }, []);
  return (
    <iframe
      title="Article"
      srcDoc={srcDoc}
      sandbox="allow-scripts allow-popups allow-popups-to-escape-sandbox allow-forms"
      style={{ width: '100%', height, border: 'none', display: 'block', background: 'transparent' }}
    />
  );
}

// Collapse the two-pane layout into a single column on phones.
function useNarrow(): boolean {
  const q = '(max-width: 720px)';
  const [narrow, setNarrow] = useState(() => typeof window !== 'undefined' && window.matchMedia(q).matches);
  useEffect(() => {
    const mq = window.matchMedia(q);
    const on = () => setNarrow(mq.matches);
    mq.addEventListener('change', on);
    return () => mq.removeEventListener('change', on);
  }, []);
  return narrow;
}

type Editor = { id: string | null; title: string; content: string; readMinutes: string; format: ArticleFormat; images: ArticleImageDto[]; chatUrl: string };

// Open a chat/conversation URL in a new tab (used by the "Chat about this" button).
function openChat(url: string) { window.open(url, '_blank', 'noopener'); }

export function ArticlesPage() {
  const narrow = useNarrow();
  const [list, setList] = useState<ArticleSummaryDto[]>([]);
  const [selId, setSelId] = useState<string | null>(null);
  const [detail, setDetail] = useState<ArticleDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [editor, setEditor] = useState<Editor | null>(null);
  // Fetched self-contained HTML document for the selected article (HTML format only).
  const [htmlDoc, setHtmlDoc] = useState<string | null>(null);
  const [htmlLoading, setHtmlLoading] = useState(false);
  // On mobile we show either the list OR the reader; this flag says the reader is open.
  const [mobileReaderOpen, setMobileReaderOpen] = useState(false);

  const loadList = useCallback(async (selectId?: string | null) => {
    const d = await api.getArticles();
    setList(d);
    setSelId(prev => {
      const next = selectId !== undefined ? selectId : (prev && d.some(a => a.id === prev) ? prev : (d[0]?.id ?? null));
      return next;
    });
  }, []);

  useEffect(() => { setLoading(true); loadList().finally(() => setLoading(false)); }, [loadList]);

  useEffect(() => {
    if (!selId) { setDetail(null); return; }
    let alive = true;
    api.getArticle(selId).then(d => { if (alive) setDetail(d); });
    return () => { alive = false; };
  }, [selId]);

  // For HTML articles, fetch the self-contained document (images inlined) for the iframe + new tab.
  // Re-fetch whenever the article changes or is edited (updatedAt).
  useEffect(() => {
    if (!detail || detail.format !== 'html') { setHtmlDoc(null); setHtmlLoading(false); return; }
    let alive = true;
    setHtmlLoading(true);
    api.getArticleHtml(detail.id)
      .then(h => { if (alive) setHtmlDoc(h); })
      .finally(() => { if (alive) setHtmlLoading(false); });
    return () => { alive = false; };
  }, [detail?.id, detail?.format, detail?.updatedAt]);

  const refresh = async (selectId?: string | null) => {
    await loadList(selectId);
    const id = selectId ?? selId;
    if (id) setDetail(await api.getArticle(id));
  };

  const openArticle = (id: string) => { setSelId(id); setMobileReaderOpen(true); };

  const toggleRead = async (a: ArticleSummaryDto | ArticleDto) => {
    setBusy(true);
    try {
      if (a.isRead) await api.markArticleUnread(a.id);
      else await api.markArticleRead(a.id);
      await refresh(a.id);
    } finally { setBusy(false); }
  };

  const saveEditor = async () => {
    if (!editor) return;
    const title = editor.title.trim();
    if (!title) return;
    const mins = editor.readMinutes.trim() === '' ? undefined : Math.max(1, parseInt(editor.readMinutes, 10) || 0) || undefined;
    setBusy(true);
    try {
      const saved = editor.id
        ? await api.updateArticle(editor.id, title, editor.content, editor.format, editor.chatUrl.trim(), mins)
        : await api.createArticle(title, editor.content, editor.format, editor.chatUrl.trim(), mins);
      // Keep the editor open (now in edit mode) so images can be uploaded to the saved article.
      setEditor({ id: saved.id, title: saved.title, content: saved.content, readMinutes: String(saved.readMinutes), format: saved.format, images: saved.images, chatUrl: saved.chatUrl ?? '' });
      await refresh(saved.id);
    } finally { setBusy(false); }
  };

  const closeEditor = (openReader?: boolean) => {
    setEditor(null);
    if (openReader) setMobileReaderOpen(true);
  };

  const del = async (a: ArticleSummaryDto | ArticleDto) => {
    if (!confirm(`Delete "${a.title}"?`)) return;
    setBusy(true);
    try {
      await api.deleteArticle(a.id);
      await loadList(selId === a.id ? null : selId);
      if (selId === a.id) { setDetail(null); setMobileReaderOpen(false); }
    } finally { setBusy(false); }
  };

  const startEdit = (d: ArticleDto) =>
    setEditor({ id: d.id, title: d.title, content: d.content, readMinutes: String(d.readMinutes), format: d.format, images: d.images, chatUrl: d.chatUrl ?? '' });

  const readCount = list.filter(a => a.isRead).length;
  const readerOpen = narrow && mobileReaderOpen && !!selId;

  // ---- List panel ----
  const listPanel = (mobile: boolean) => (
    <div style={{
      overflowY: 'auto', WebkitOverflowScrolling: 'touch', padding: 8,
      ...(mobile
        ? { flex: 1, minHeight: 0, width: '100%' }
        : { width: 280, flexShrink: 0, borderRight: '1px solid var(--border-subtle)' }),
    }}>
      {list.length === 0 ? (
        <div style={{ padding: 16, color: 'var(--text-muted)', fontSize: 13 }}>No articles yet. Create one, or add via MCP.</div>
      ) : list.map(a => {
        const active = a.id === selId && !mobile;
        return (
          <div key={a.id} onClick={() => openArticle(a.id)}
            style={{
              display: 'flex', alignItems: 'flex-start', gap: 10, padding: mobile ? '13px 12px' : '9px 10px', marginBottom: 4,
              borderRadius: 'var(--radius-sm)', cursor: 'pointer',
              borderLeft: `3px solid ${active ? 'var(--accent)' : 'transparent'}`,
              background: active ? 'var(--bg-secondary)' : 'transparent',
            }}>
            <button title={a.isRead ? 'Mark as pending' : 'Mark as read'} disabled={busy}
              onClick={e => { e.stopPropagation(); toggleRead(a); }}
              style={{
                flexShrink: 0, marginTop: 1, width: mobile ? 24 : 18, height: mobile ? 24 : 18, borderRadius: 5, cursor: 'pointer',
                border: `2px solid ${a.isRead ? 'var(--accent)' : 'var(--border)'}`,
                background: a.isRead ? 'var(--accent)' : 'transparent',
                color: '#fff', fontSize: mobile ? 15 : 12, lineHeight: mobile ? '20px' : '14px', padding: 0,
              }}>{a.isRead ? '✓' : ''}</button>
            <div style={{ minWidth: 0, flex: 1 }}>
              <div style={{ fontSize: mobile ? 15 : 13.5, fontWeight: 600, color: active ? 'var(--text-primary)' : 'var(--text-secondary)',
                overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{a.title}</div>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 3, display: 'flex', gap: 8, alignItems: 'center' }}>
                <span>⏱ {a.readMinutes} min</span>
                {a.format === 'html' && <span style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.04em', color: 'var(--accent)', border: '1px solid var(--border)', borderRadius: 4, padding: '0 4px' }}>HTML</span>}
                {a.imageCount > 0 && <span>🖼 {a.imageCount}</span>}
                {a.isRead && a.readOn && <span style={{ color: 'var(--accent)' }}>✓ {fmtDate(a.readOn)}</span>}
              </div>
            </div>
            {mobile && <span style={{ color: 'var(--text-muted)', fontSize: 18, alignSelf: 'center' }}>›</span>}
          </div>
        );
      })}
    </div>
  );

  // Metadata line shared by both reader layouts.
  const metaLine = (d: ArticleDto) => (
    <>
      <span>⏱ {d.readMinutes} min read</span>
      {d.isRead && d.readOn && <>
        <span>·</span>
        <span style={{ color: 'var(--accent)', fontWeight: 600 }}>✓ Read on {fmtDate(d.readOn)}</span>
      </>}
    </>
  );

  const footer = (d: ArticleDto) => (
    <div className="article-foot">
      {d.isRead ? (
        <>
          <span style={{ fontSize: 14, color: 'var(--accent)', fontWeight: 600 }}>✓ Read{d.readOn ? ` on ${fmtDate(d.readOn)}` : ''}</span>
          <button className="btn btn-sm btn-ghost" disabled={busy} onClick={() => toggleRead(d)}>Mark as pending</button>
        </>
      ) : (
        <button className="btn btn-accent" disabled={busy} onClick={() => toggleRead(d)}
          style={{ padding: '11px 22px', fontSize: 15 }}>
          ✓ Mark as Read
        </button>
      )}
    </div>
  );

  // ---- Reader panel ----
  const readerPanel = (mobile: boolean) => (
    <div style={{ flex: 1, minWidth: 0, minHeight: 0, overflowY: 'auto', WebkitOverflowScrolling: 'touch' }}>
      {mobile && (
        <div style={{ position: 'sticky', top: 0, zIndex: 5, display: 'flex', padding: '8px 10px',
          background: 'var(--bg-primary)', borderBottom: '1px solid var(--border-subtle)' }}>
          <button className="btn btn-ghost btn-sm" onClick={() => setMobileReaderOpen(false)}>← All articles</button>
        </div>
      )}
      {!detail ? (
        <div style={{ padding: 24, color: 'var(--text-muted)', fontSize: 14 }}>{mobile ? 'Loading…' : 'Select an article to read.'}</div>
      ) : detail.format === 'html' ? (
        // ---- HTML article: compact toolbar + sandboxed iframe + read footer ----
        <article className="article-reader">
          <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 8,
            padding: '12px 16px', borderBottom: '1px solid var(--border-subtle)' }}>
            <div style={{ minWidth: 0, flex: 1 }}>
              <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{detail.title}</div>
              <div className="article-meta" style={{ border: 'none', padding: 0, marginTop: 2, fontSize: 12.5 }}>{metaLine(detail)}</div>
            </div>
            {detail.chatUrl && <button className="btn btn-sm" onClick={() => openChat(detail.chatUrl!)}
              title="Open the chat this article came from">💬 Chat about this ↗</button>}
            <button className="btn btn-sm" disabled={!htmlDoc}
              onClick={() => htmlDoc && openHtmlInNewTab(htmlDoc)}
              title="Open this article's full HTML in a new browser tab">Open in new tab ↗</button>
            <button className="btn btn-sm" onClick={() => startEdit(detail)}>Edit</button>
            <button className="btn btn-sm btn-danger" onClick={() => del(detail)}>Delete</button>
          </div>
          {htmlLoading && !htmlDoc ? (
            <div style={{ padding: 24, color: 'var(--text-muted)', fontSize: 14 }}>Rendering…</div>
          ) : htmlDoc ? (
            <HtmlArticleFrame html={htmlDoc} />
          ) : (
            <div style={{ padding: 24, color: 'var(--text-muted)', fontSize: 14 }}>Could not load the article HTML.</div>
          )}
          {footer(detail)}
        </article>
      ) : (
        // ---- Markdown article: Medium-style reader ----
        <article className="article-reader">
          <div className="article-head">
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 6, marginBottom: 10 }}>
              {detail.chatUrl && <button className="btn btn-sm" onClick={() => openChat(detail.chatUrl!)}
                title="Open the chat this article came from">💬 Chat about this ↗</button>}
              <button className="btn btn-sm" onClick={() => startEdit(detail)}>Edit</button>
              <button className="btn btn-sm btn-danger" onClick={() => del(detail)}>Delete</button>
            </div>
            <h1 className="article-title">{detail.title}</h1>
            <div className="article-meta">{metaLine(detail)}</div>
          </div>

          <div className="article-body" dangerouslySetInnerHTML={{ __html: marked.parse(detail.content || '_No content._') as string }} />

          {footer(detail)}
        </article>
      )}
    </div>
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Top bar — hidden on mobile while the reader is open (it has its own back bar) */}
      {!readerOpen && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '12px 16px', borderBottom: '1px solid var(--border-subtle)', flexShrink: 0 }}>
          <span style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>📚 Articles</span>
          <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>{readCount}/{list.length} read</span>
          <div style={{ flex: 1 }} />
          <button className="btn btn-accent btn-sm" onClick={() => setEditor({ id: null, title: '', content: '', readMinutes: '', format: 'html', images: [], chatUrl: '' })}>+ New Article</button>
        </div>
      )}

      {loading ? (
        <div style={{ padding: 24, color: 'var(--text-muted)' }}>Loading...</div>
      ) : narrow ? (
        readerOpen ? readerPanel(true) : listPanel(true)
      ) : (
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {listPanel(false)}
          {readerPanel(false)}
        </div>
      )}

      {editor && <ArticleEditor editor={editor} setEditor={setEditor} busy={busy} onSave={saveEditor} onClose={closeEditor} />}
    </div>
  );
}

function ArticleEditor({ editor, setEditor, busy, onSave, onClose }: {
  editor: Editor; setEditor: (e: Editor | null) => void; busy: boolean; onSave: () => void; onClose: (openReader?: boolean) => void;
}) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [imgErr, setImgErr] = useState<string | null>(null);
  const isHtml = editor.format === 'html';

  const uploadFiles = async (files: FileList | null) => {
    if (!files || files.length === 0 || !editor.id) return;
    setImgErr(null); setUploading(true);
    try {
      const images = await api.uploadArticleImages(editor.id, Array.from(files));
      setEditor({ ...editor, images });
    } catch (e) {
      setImgErr(e instanceof Error ? e.message : 'Upload failed');
    } finally {
      setUploading(false);
      if (fileRef.current) fileRef.current.value = '';
    }
  };

  const deleteImage = async (name: string) => {
    if (!editor.id) return;
    setImgErr(null);
    try {
      await api.deleteArticleImage(editor.id, name);
      setEditor({ ...editor, images: editor.images.filter(i => i.name !== name) });
    } catch (e) {
      setImgErr(e instanceof Error ? e.message : 'Delete failed');
    }
  };

  const refOf = (name: string) => `{{img:${name}}}`;
  const insertImage = (name: string) => {
    const snippet = `<img src="${refOf(name)}" alt="${name}">`;
    const sep = editor.content && !editor.content.endsWith('\n') ? '\n' : '';
    setEditor({ ...editor, content: editor.content + sep + snippet + '\n' });
  };
  const copyRef = (name: string) => { void navigator.clipboard?.writeText(refOf(name)); };

  const inputStyle: React.CSSProperties = {
    fontSize: 15, padding: '10px 12px', background: 'var(--bg-secondary)',
    border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)', color: 'var(--text-primary)', outline: 'none',
  };

  return (
    <div onClick={() => onClose()}
      style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.45)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100, padding: 20 }}>
      <div onClick={e => e.stopPropagation()}
        style={{ width: 'min(820px, 100%)', maxHeight: '92vh', display: 'flex', flexDirection: 'column',
          background: 'var(--bg-primary)', border: '1px solid var(--border)', borderRadius: 'var(--radius-lg)', padding: 20 }}>
        <div style={{ display: 'flex', alignItems: 'center', marginBottom: 14 }}>
          <h2 style={{ margin: 0, fontSize: 18, color: 'var(--text-primary)', flex: 1 }}>{editor.id ? 'Edit article' : 'New article'}</h2>
          <button className="btn btn-ghost btn-sm" onClick={() => onClose()}>✕</button>
        </div>

        <div style={{ display: 'flex', gap: 10, marginBottom: 10, flexWrap: 'wrap', alignItems: 'center' }}>
          <input value={editor.title} autoFocus placeholder="Title"
            onChange={e => setEditor({ ...editor, title: e.target.value })}
            style={{ ...inputStyle, flex: 1, minWidth: 200 }} />
          <input value={editor.readMinutes} type="number" min={1} placeholder="min (auto)"
            title="Approximate reading time in minutes — leave blank to auto-estimate from length"
            onChange={e => setEditor({ ...editor, readMinutes: e.target.value })}
            style={{ ...inputStyle, width: 120, fontSize: 14 }} />
          {/* Format toggle */}
          <div style={{ display: 'flex', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)', overflow: 'hidden' }}>
            {(['markdown', 'html'] as ArticleFormat[]).map(f => (
              <button key={f} onClick={() => setEditor({ ...editor, format: f })}
                style={{
                  padding: '9px 14px', fontSize: 13, fontWeight: 600, cursor: 'pointer', border: 'none',
                  background: editor.format === f ? 'var(--accent)' : 'transparent',
                  color: editor.format === f ? '#fff' : 'var(--text-secondary)',
                }}>{f === 'markdown' ? 'Markdown' : 'HTML'}</button>
            ))}
          </div>
        </div>

        <input value={editor.chatUrl} type="url" placeholder="Chat URL (optional) — e.g. the ChatGPT conversation this came from"
          onChange={e => setEditor({ ...editor, chatUrl: e.target.value })}
          style={{ ...inputStyle, width: '100%', fontSize: 13, marginBottom: 10, boxSizing: 'border-box' }} />

        {isHtml && (
          <div style={{ fontSize: 12.5, color: 'var(--text-muted)', marginBottom: 8, lineHeight: 1.5 }}>
            Paste a full HTML document (with its own <code>&lt;style&gt;</code>) or a fragment. Reference uploaded images
            as <code>{'{{img:NAME}}'}</code> in an <code>&lt;img&gt;</code> src. It renders here and opens full-screen via <b>Open in new tab</b>.
          </div>
        )}

        <textarea value={editor.content}
          placeholder={isHtml ? 'Write the article as HTML — <h1>, <p>, <style>, <img src="{{img:cover.png}}"> …' : 'Write the article in Markdown...'}
          onChange={e => setEditor({ ...editor, content: e.target.value })}
          style={{ flex: 1, minHeight: 280, fontSize: 14, lineHeight: 1.6, padding: '12px 14px', resize: 'vertical',
            fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
            background: 'var(--bg-secondary)', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)', color: 'var(--text-primary)', outline: 'none' }} />

        {/* Image manager — available once the article is saved (needs an id) */}
        {isHtml && (
          <div style={{ marginTop: 12, border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: editor.images.length ? 10 : 0, flexWrap: 'wrap' }}>
              <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>🖼 Images</span>
              <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{editor.images.length} uploaded</span>
              <div style={{ flex: 1 }} />
              {editor.id ? (
                <>
                  <input ref={fileRef} type="file" accept="image/*" multiple style={{ display: 'none' }}
                    onChange={e => uploadFiles(e.target.files)} />
                  <button className="btn btn-sm" disabled={uploading} onClick={() => fileRef.current?.click()}>
                    {uploading ? 'Uploading…' : '+ Upload images'}
                  </button>
                </>
              ) : (
                <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>Save first to upload images</span>
              )}
            </div>
            {imgErr && <div style={{ fontSize: 12, color: 'var(--danger, #d33)', marginBottom: 8 }}>{imgErr}</div>}
            {editor.images.length > 0 && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {editor.images.map(img => (
                  <div key={img.name} style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap',
                    fontSize: 12.5, background: 'var(--bg-secondary)', borderRadius: 'var(--radius-sm)', padding: '6px 10px' }}>
                    <code style={{ color: 'var(--text-primary)' }}>{refOf(img.name)}</code>
                    <span style={{ color: 'var(--text-muted)' }}>{img.contentType}</span>
                    <div style={{ flex: 1 }} />
                    <button className="btn btn-ghost btn-sm" onClick={() => insertImage(img.name)} title="Append an <img> tag to the body">Insert</button>
                    <button className="btn btn-ghost btn-sm" onClick={() => copyRef(img.name)} title="Copy the {{img:…}} reference">Copy</button>
                    <button className="btn btn-ghost btn-sm btn-danger" onClick={() => deleteImage(img.name)}>Delete</button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 14, alignItems: 'center' }}>
          {editor.id && <span style={{ fontSize: 12, color: 'var(--text-muted)', marginRight: 'auto' }}>Saved · you can keep editing or add images</span>}
          <button className="btn" onClick={() => onClose(!!editor.id)}>{editor.id ? 'Close' : 'Cancel'}</button>
          <button className="btn btn-accent" disabled={busy || !editor.title.trim()} onClick={onSave}>{busy ? 'Saving...' : 'Save'}</button>
        </div>
      </div>
    </div>
  );
}
