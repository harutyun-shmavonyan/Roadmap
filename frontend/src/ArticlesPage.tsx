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
  const inject = READER_TWEAKS + HEIGHT_REPORTER + ANCHOR_RESPONDER;
  return /<\/body>/i.test(html) ? html.replace(/<\/body>/i, inject + '</body>') : html + inject;
}

// ---- Reading position ----------------------------------------------------------------------
// Where you stopped is kept on the article itself, in two forms. The anchor — "index:offset": the
// block element that sat at the reader's top edge and how far into it you were — is the exact
// spot, and it survives a reflow, so reopening in a narrower window, at a bigger font size or on
// the phone lands on the same sentence rather than merely the same percentage. The fraction of the
// scrollable length is the fallback: it places the reader roughly before the anchor resolves (an
// HTML article's iframe has to render first), covers an anchor that no longer resolves at all, and
// draws the progress bar in the list. The in-app reader and the "open in new tab" view record and
// restore both, so the two always agree on where you are.

// Scroll settles for this long before the position is sent.
const POS_SAVE_MS = 700;
// A position this close to either end isn't worth jumping to — just start at the top.
const POS_EPS = 0.02;
// Restoring re-applies the position on a tick until the content stops growing (an HTML article
// lives in an auto-sizing iframe whose height only arrives after the document loads and settles),
// then stops — or gives up after POS_RETRIES ticks, whichever comes first.
const POS_RETRY_MS = 120;
const POS_RETRIES = 40;
// Ticks with an unchanged scrollable length that mean the layout has settled.
const POS_STABLE_TICKS = 5;
// Input that means "I'm driving now" — it cancels an in-flight restore.
const TAKEOVER_EVENTS = ['wheel', 'touchstart', 'keydown', 'mousedown'] as const;

// The block elements a position can be anchored to. An anchor names an index into this list, so
// the list has to come out identical everywhere one article is rendered — it does: the reader
// iframe and the new tab are handed the very same document.
const ANCHOR_SEL = 'p,h1,h2,h3,h4,h5,h6,li,blockquote,pre,figure,img,table,hr';

type Bookmark = { pos: number; anchor: string | null };

// How far through a scrollable element we are, 0..1 (0 when there's nothing to scroll).
function scrollFraction(el: HTMLElement): number {
  const max = el.scrollHeight - el.clientHeight;
  return max <= 0 ? 0 : Math.min(1, Math.max(0, el.scrollTop / max));
}

function worthRestoring(pos: number): boolean {
  return Number.isFinite(pos) && pos > POS_EPS && pos < 1 - POS_EPS;
}

// Viewport y of the pane's scroll origin, i.e. what to subtract from an element's rect to get its
// top in the pane's own scroll coordinates.
function paneOrigin(pane: HTMLElement): number {
  return pane.getBoundingClientRect().top - pane.scrollTop;
}

// The anchor for wherever the pane is scrolled to: the last block element starting at or above the
// reading edge, plus how far past its start we are. The edge is `inset` below the pane's top — the
// height of the sticky bar — so the anchor names the first line you can actually read, which is
// also what the new tab (no bar, inset 0) names. '' when we're above the first element: near the
// top there is nothing to anchor to and nothing worth anchoring.
function anchorInPane(pane: HTMLElement, inset: number): string {
  const origin = paneOrigin(pane), top = pane.scrollTop + inset;
  const els = pane.querySelectorAll<HTMLElement>(ANCHOR_SEL);
  let best = -1, bestTop = 0;
  for (let i = 0; i < els.length; i++) {
    const t = els[i].getBoundingClientRect().top - origin;
    if (t > top + 1) break;
    best = i; bestTop = t;
  }
  return best < 0 ? '' : `${best}:${Math.round(top - bestTop)}`;
}

// The scrollTop that puts an anchor back at the reading edge, or -1 when it doesn't resolve (the
// article was edited since, or the anchor belongs to a document this pane isn't showing).
function offsetInPane(pane: HTMLElement, anchor: string, inset: number): number {
  const [i, d] = anchor.split(':').map(Number);
  const els = pane.querySelectorAll<HTMLElement>(ANCHOR_SEL);
  if (!Number.isInteger(i) || i < 0 || i >= els.length) return -1;
  return els[i].getBoundingClientRect().top - paneOrigin(pane) + (Number.isFinite(d) ? d : 0) - inset;
}

// The same anchor arithmetic, as source injected into article documents (the reader iframe and the
// new tab), where positions are measured against the document rather than a pane.
const ANCHOR_JS =
  `var AS=${JSON.stringify(ANCHOR_SEL)};function AE(){return document.querySelectorAll(AS)}` +
  `function AT(e){return e.getBoundingClientRect().top+(window.pageYOffset||0)}` +
  `function AA(top){var l=AE(),b=-1,bt=0;for(var i=0;i<l.length;i++){var t=AT(l[i]);if(t>top+1)break;b=i;bt=t}` +
  `return b<0?'':b+':'+Math.round(top-bt)}` +
  `function AO(a){var p=String(a||'').split(':'),i=parseInt(p[0],10),d=parseInt(p[1],10)||0,l=AE();` +
  `return (i>=0&&i<l.length)?AT(l[i])+d:-1}`;

// The reader's iframe is sandboxed to an opaque origin, so the parent can neither read its DOM nor
// measure a paragraph inside it. This answers the parent's two questions over postMessage: which
// anchor sits at a given offset (asked while you read), and where an anchor lands (while restoring).
const ANCHOR_RESPONDER =
  `<script>(function(){${ANCHOR_JS}` +
  `addEventListener('message',function(ev){var d=ev.data;if(!d||typeof d!=='object')return;` +
  `if(typeof d.__anchorAsk==='number')parent.postMessage({__articleAnchor:AA(d.__anchorAsk)},'*');` +
  `else if(typeof d.__offsetAsk==='string')parent.postMessage({__articleOffset:AO(d.__offsetAsk)},'*');});` +
  `})();<\/script>`;

// The article's top edge in the pane's scroll coordinates — the iframe sits below the toolbar, so
// an offset inside the document is this much further down the pane.
function frameTop(pane: HTMLElement, frame: HTMLIFrameElement): number {
  return frame.getBoundingClientRect().top - paneOrigin(pane);
}

// Saving and restoring for the standalone document opened in a new tab, as a self-contained
// script. That document is a blob: URL created by this page, so it inherits the app's origin and
// can read the stored token and call the API itself.
function positionScript(id: string, bm: Bookmark): string {
  const cfg = JSON.stringify({
    id, start: worthRestoring(bm.pos) ? bm.pos : 0, anchor: bm.anchor || '',
    origin: window.location.origin, save: POS_SAVE_MS, step: POS_RETRY_MS, tries: POS_RETRIES, stable: POS_STABLE_TICKS,
  });
  return `<script>(function(){var C=${cfg},live=true,t;${ANCHOR_JS}` +
    `function frac(){var m=document.documentElement.scrollHeight-innerHeight;` +
    `return m<=0?0:Math.min(1,Math.max(0,pageYOffset/m));}` +
    `function save(){try{var k=localStorage.getItem('roadmap_token');if(!k)return;` +
    `fetch(C.origin+'/api/articles/'+C.id+'/progress',{method:'PUT',keepalive:true,` +
    `headers:{'Content-Type':'application/json',Authorization:'Bearer '+k},` +
    `body:JSON.stringify({progress:frac(),anchor:AA(pageYOffset)})}).catch(function(){});}catch(e){}}` +
    `addEventListener('scroll',function(){clearTimeout(t);t=setTimeout(save,C.save)},{passive:true});` +
    `addEventListener('pagehide',save);` +
    `document.addEventListener('visibilitychange',function(){if(document.visibilityState==='hidden')save()});` +
    `if(C.start>0||C.anchor){var n=0,same=0,last=-1,exact=false,iv=setInterval(function(){` +
    `if(!live||++n>C.tries){clearInterval(iv);return;}` +
    `var m=document.documentElement.scrollHeight-innerHeight;if(m<=0)return;` +
    `var y=C.anchor?AO(C.anchor):-1;if(y>=0){exact=true;scrollTo(0,y)}else if(!exact&&C.start>0)scrollTo(0,C.start*m);` +
    `same=m===last?same+1:0;last=m;if(same>=C.stable)clearInterval(iv);},C.step);` +
    `${JSON.stringify(TAKEOVER_EVENTS)}.forEach(function(e){addEventListener(e,function(){live=false},{passive:true,once:true})});}` +
    `})();<\/script>`;
}

// Open the self-contained article HTML in a new tab via a blob URL. Stays private (the HTML was
// fetched with the bearer token) — no server route is publicly reachable. Carries the same
// reader style the iframe injects, so the tab reads identically (justified paragraphs), plus the
// position script so the tab resumes — and records — the same reading position as the app.
function openHtmlInNewTab(html: string, id: string, bm: Bookmark) {
  const inject = READER_TWEAKS + positionScript(id, bm);
  const styled = /<\/body>/i.test(html) ? html.replace(/<\/body>/i, inject + '</body>') : html + inject;
  const url = URL.createObjectURL(new Blob([styled], { type: 'text/html' }));
  window.open(url, '_blank', 'noopener');
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/**
 * Keeps the reader pane's scroll position and the article's stored reading position in sync.
 *
 * Saving: scrolling is debounced into a PUT carrying both the fraction and the anchor, and
 * anything still pending is flushed when the article changes, the tab is hidden or the reader
 * unmounts — so closing the tab mid-article still records the spot. Nothing is saved while the
 * article isn't fully rendered, otherwise the browser clamping scrollTop during a content swap
 * would overwrite the bookmark with 0.
 *
 * Restoring: the fraction places the reader roughly, the anchor then puts it exactly on the spot,
 * and both are re-applied for a few seconds while an HTML article's iframe grows into its real
 * height. Any scroll input from the reader cancels the restore on the spot.
 */
function useReadingPosition(
  // The element, not a ref: on phones the reader pane mounts only when you tap into an article,
  // long after it was selected, and the restore has to wait for it rather than miss it.
  pane: HTMLDivElement | null,
  // The sticky bar covering the top of the pane, if any — positions are measured from below it.
  head: HTMLElement | null,
  frameRef: React.RefObject<HTMLIFrameElement | null>,
  articleId: string | null,
  saved: Bookmark,
  isHtml: boolean,
  ready: boolean,
  onSaved: (id: string, bm: Bookmark) => void,
  onRestored: (pos: number | null) => void,
) {
  // Mirror of `pane` for the callbacks and listeners below, which run outside render.
  const paneRef = useRef<HTMLDivElement | null>(null); paneRef.current = pane;
  const headRef = useRef<HTMLElement | null>(null); headRef.current = head;
  const inset = () => headRef.current?.offsetHeight ?? 0;
  const pending = useRef<{ id: string; pos: number; anchor: string | null } | null>(null);
  const timer = useRef<number | undefined>(undefined);
  const restoring = useRef(false);
  // Set once an anchor has actually placed the reader, so the coarse fraction stops fighting it.
  const exact = useRef(false);
  // The iframe's last answer to "which anchor is at the top edge?", used by the next save.
  const frameAnchor = useRef<string | null>(null);
  // Read inside effects that must not re-run when these change (a re-fetched article, a new
  // render of the parent) — only a new article or newly rendered content starts a restore.
  const savedRef = useRef(saved); savedRef.current = saved;
  const cbs = useRef({ onSaved, onRestored }); cbs.current = { onSaved, onRestored };

  const flush = useCallback(() => {
    window.clearTimeout(timer.current);
    const p = pending.current;
    pending.current = null;
    if (!p) return;
    const bm: Bookmark = { pos: p.pos, anchor: p.anchor ?? frameAnchor.current };
    api.saveArticleProgress(p.id, bm.pos, bm.anchor);
    cbs.current.onSaved(p.id, bm);
  }, []);

  const onScroll = useCallback(() => {
    const pane = paneRef.current;
    if (!pane || !articleId || !ready || restoring.current) return;
    const pos = scrollFraction(pane);
    if (isHtml) {
      // Only the sandboxed document can name the paragraph at its top edge; the answer arrives by
      // message well before this save's debounce is up.
      const frame = frameRef.current;
      frame?.contentWindow?.postMessage({ __anchorAsk: pane.scrollTop + inset() - frameTop(pane, frame) }, '*');
      pending.current = { id: articleId, pos, anchor: null };
    } else {
      pending.current = { id: articleId, pos, anchor: anchorInPane(pane, inset()) || null };
    }
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(flush, POS_SAVE_MS);
  }, [articleId, ready, isHtml, flush, frameRef]);

  // Replies from the article's document: an anchor to save, or the offset to restore to.
  useEffect(() => {
    const onMsg = (e: MessageEvent) => {
      const d = e.data as { __articleAnchor?: unknown; __articleOffset?: unknown } | null;
      if (!d || typeof d !== 'object') return;
      if (typeof d.__articleAnchor === 'string') frameAnchor.current = d.__articleAnchor || null;
      if (typeof d.__articleOffset === 'number' && restoring.current && d.__articleOffset >= 0) {
        const pane = paneRef.current, frame = frameRef.current;
        if (pane && frame) { pane.scrollTop = frameTop(pane, frame) + d.__articleOffset - inset(); exact.current = true; }
      }
    };
    window.addEventListener('message', onMsg);
    return () => window.removeEventListener('message', onMsg);
  }, [frameRef]);

  // Flush on article switch and on unmount.
  useEffect(() => flush, [articleId, flush]);
  // A new article starts with no anchor of its own — never carry the previous one over.
  useEffect(() => { frameAnchor.current = null; }, [articleId]);

  // Flush when the tab goes away — 'visibilitychange' covers mobile backgrounding, 'pagehide' a
  // real close/navigation (the save uses keepalive so it survives both).
  useEffect(() => {
    const onHide = () => { if (document.visibilityState === 'hidden') flush(); };
    document.addEventListener('visibilitychange', onHide);
    window.addEventListener('pagehide', flush);
    return () => {
      document.removeEventListener('visibilitychange', onHide);
      window.removeEventListener('pagehide', flush);
    };
  }, [flush]);

  useEffect(() => {
    cbs.current.onRestored(null);
    exact.current = false;
    if (!pane || !articleId || !ready) return;
    const { pos: target, anchor } = savedRef.current;
    if (!worthRestoring(target) && !anchor) { pane.scrollTop = 0; return; }

    restoring.current = true;
    let tries = 0, stable = 0, lastMax = -1, iv = 0;
    const stop = () => { window.clearInterval(iv); restoring.current = false; };
    const apply = () => {
      const max = pane.scrollHeight - pane.clientHeight;
      if (max > 0) {
        // Rough placement from the fraction until the anchor has landed the exact spot.
        if (!exact.current && target > 0) pane.scrollTop = target * max;
        if (anchor) {
          if (isHtml) {
            const frame = frameRef.current;
            frame?.contentWindow?.postMessage({ __offsetAsk: anchor }, '*');
          } else {
            const y = offsetInPane(pane, anchor, inset());
            if (y >= 0) { pane.scrollTop = y; exact.current = true; }
          }
        }
        stable = max === lastMax ? stable + 1 : 0;
        lastMax = max;
      }
      // Done once the content has stopped growing under us — or once we've waited long enough.
      if (stable >= POS_STABLE_TICKS || ++tries >= POS_RETRIES) stop();
    };
    apply();
    iv = window.setInterval(apply, POS_RETRY_MS);
    const opts: AddEventListenerOptions = { passive: true, once: true };
    TAKEOVER_EVENTS.forEach(e => pane.addEventListener(e, stop, opts));
    if (worthRestoring(target)) cbs.current.onRestored(target);
    return () => {
      stop();
      TAKEOVER_EVENTS.forEach(e => pane.removeEventListener(e, stop));
    };
  }, [pane, articleId, ready, isHtml, frameRef]);

  return onScroll;
}

// Renders a self-contained HTML article inside a sandboxed iframe that auto-sizes to its content,
// so the surrounding reader pane (not the iframe) scrolls.
function HtmlArticleFrame({ html, frameRef }: { html: string; frameRef: React.RefObject<HTMLIFrameElement> }) {
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
      ref={frameRef}
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
  // Fetched self-contained HTML document for the selected article (HTML format only). Kept with
  // the id it belongs to so a stale document is never treated as the new article's content.
  const [htmlDoc, setHtmlDoc] = useState<{ id: string; html: string } | null>(null);
  const [htmlLoading, setHtmlLoading] = useState(false);
  // On mobile we show either the list OR the reader; this flag says the reader is open.
  const [mobileReaderOpen, setMobileReaderOpen] = useState(false);
  // The scrolling reader pane (state, not a ref: on phones it mounts only once you tap into an
  // article) and the position we resumed it to, shown as a brief hint.
  const [paneEl, setPaneEl] = useState<HTMLDivElement | null>(null);
  // The sticky bar at the top of the pane: the reading position is measured from below it.
  const [headEl, setHeadEl] = useState<HTMLDivElement | null>(null);
  const frameRef = useRef<HTMLIFrameElement>(null);
  const [resumedAt, setResumedAt] = useState<number | null>(null);

  const loadList = useCallback(async (selectId?: string | null) => {
    const d = await api.getArticles();
    // What is still to read comes first; anything ticked read sinks to the bottom, where it stays
    // reachable without being in the way. Within each half the server's own order is untouched —
    // sort order, then newest first — because Array.sort is stable.
    const ordered = [...d].sort((a, b) => Number(a.isRead) - Number(b.isRead));
    setList(ordered);
    setSelId(prev => {
      const next = selectId !== undefined ? selectId
        : (prev && ordered.some(a => a.id === prev) ? prev : (ordered[0]?.id ?? null));
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
    const id = detail.id;
    api.getArticleHtml(id)
      .then(h => { if (alive) setHtmlDoc({ id, html: h }); })
      .finally(() => { if (alive) setHtmlLoading(false); });
    return () => { alive = false; };
  }, [detail?.id, detail?.format, detail?.updatedAt]);

  // The HTML of the article currently on screen (null while it's still being fetched).
  const shownHtml = detail && htmlDoc?.id === detail.id ? htmlDoc.html : null;
  // The article is fully on screen — and so safe to scroll and to record a position for — once
  // its detail matches the selection and, for HTML articles, its document has arrived.
  const readerReady = !!detail && detail.id === selId && (detail.format !== 'html' || !!shownHtml);

  // Every recorded position updates the list row and the article's live bookmark, so the list,
  // the reader's "% in" and the bookmark handed to a new tab all track the current read.
  const [livePos, setLivePos] = useState<{ id: string; bm: Bookmark } | null>(null);
  const onListProgress = useCallback((id: string, bm: Bookmark) => {
    setList(prev => prev.map(a => (a.id === id ? { ...a, readProgress: bm.pos } : a)));
    setLivePos({ id, bm });
  }, []);
  const bookmarkOf = (d: ArticleDto): Bookmark =>
    livePos?.id === d.id ? livePos.bm : { pos: d.readProgress, anchor: d.readAnchor };

  const onReaderScroll = useReadingPosition(
    paneEl, headEl, frameRef, detail?.id ?? null,
    detail ? { pos: detail.readProgress, anchor: detail.readAnchor } : { pos: 0, anchor: null },
    detail?.format === 'html', readerReady, onListProgress, setResumedAt);

  // The "picked up where you left off" hint is a nudge, not a status line — let it go after a bit.
  useEffect(() => {
    if (resumedAt === null) return;
    const t = window.setTimeout(() => setResumedAt(null), 5000);
    return () => window.clearTimeout(t);
  }, [resumedAt]);

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
                {!a.isRead && worthRestoring(a.readProgress) && (
                  <span title="Where you stopped reading" style={{ display: 'inline-flex', alignItems: 'center', gap: 5, color: 'var(--accent)' }}>
                    <span style={{ display: 'inline-block', width: 30, height: 4, borderRadius: 2, background: 'var(--border)', overflow: 'hidden' }}>
                      <span style={{ display: 'block', height: '100%', width: `${Math.round(a.readProgress * 100)}%`, background: 'var(--accent)' }} />
                    </span>
                    {Math.round(a.readProgress * 100)}%
                  </span>
                )}
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
      {!d.isRead && worthRestoring(bookmarkOf(d).pos) && <>
        <span>·</span>
        <span style={{ color: 'var(--accent)', fontWeight: 600 }}>{Math.round(bookmarkOf(d).pos * 100)}% in</span>
      </>}
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
  // Everything that has to stay put while the article scrolls lives in one sticky stack: the
  // phone's back bar and, for HTML articles, the toolbar carrying "Open in new tab". Stacking them
  // in a single sticky box is what keeps the rows from landing on top of each other, and the
  // reading position is measured from the bar's lower edge (see headEl) so the paragraph you
  // resume on sits just below it instead of behind it.
  const stickyHeader = (mobile: boolean) => (
    <div ref={setHeadEl} style={{ position: 'sticky', top: 0, zIndex: 5, background: 'var(--bg-primary)' }}>
      {mobile && (
        <div style={{ display: 'flex', padding: '8px 10px', borderBottom: '1px solid var(--border-subtle)' }}>
          <button className="btn btn-ghost btn-sm" onClick={() => setMobileReaderOpen(false)}>← All articles</button>
        </div>
      )}
      {detail && detail.format === 'html' && (
        <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 8,
          padding: '12px 16px', borderBottom: '1px solid var(--border-subtle)' }}>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{detail.title}</div>
            <div className="article-meta" style={{ border: 'none', padding: 0, marginTop: 2, fontSize: 12.5 }}>{metaLine(detail)}</div>
          </div>
          {detail.chatUrl && <button className="btn btn-sm" onClick={() => openChat(detail.chatUrl!)}
            title="Open the chat this article came from">💬 Chat about this ↗</button>}
          <button className="btn btn-sm" disabled={!shownHtml}
            onClick={() => shownHtml && openHtmlInNewTab(shownHtml, detail.id, bookmarkOf(detail))}
            title="Open this article's full HTML in a new browser tab">Open in new tab ↗</button>
          <button className="btn btn-sm" onClick={() => startEdit(detail)}>Edit</button>
          <button className="btn btn-sm btn-danger" onClick={() => del(detail)}>Delete</button>
        </div>
      )}
      {resumedAt !== null && (
        // Height 0 so the hint floats over the article instead of nudging it — it goes on a timer.
        <div style={{ height: 0, overflow: 'visible', display: 'flex', alignItems: 'flex-start', justifyContent: 'center', pointerEvents: 'none' }}>
          <span style={{ marginTop: 8, padding: '4px 10px', borderRadius: 999, fontSize: 12, fontWeight: 600,
            background: 'var(--bg-primary)', border: '1px solid var(--border)', color: 'var(--text-secondary)',
            boxShadow: '0 2px 8px rgba(0,0,0,0.18)' }}>
            ↩ Picked up at {Math.round(resumedAt * 100)}%
          </span>
        </div>
      )}
    </div>
  );

  const readerPanel = (mobile: boolean) => (
    <div ref={setPaneEl} onScroll={onReaderScroll}
      style={{ flex: 1, minWidth: 0, minHeight: 0, overflowY: 'auto', WebkitOverflowScrolling: 'touch', position: 'relative' }}>
      {stickyHeader(mobile)}
      {!detail ? (
        <div style={{ padding: 24, color: 'var(--text-muted)', fontSize: 14 }}>{mobile ? 'Loading…' : 'Select an article to read.'}</div>
      ) : detail.format === 'html' ? (
        // ---- HTML article: sandboxed iframe under the sticky toolbar + read footer ----
        <article className="article-reader">
          {htmlLoading && !shownHtml ? (
            <div style={{ padding: 24, color: 'var(--text-muted)', fontSize: 14 }}>Rendering…</div>
          ) : shownHtml ? (
            <HtmlArticleFrame html={shownHtml} frameRef={frameRef} />
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
