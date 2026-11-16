import { useId, type ReactNode } from 'react';

/**
 * The app's icons: one flat set, drawn here rather than pulled from a library or the platform's
 * emoji font. Mostly navigation, plus the odd action that sits in a row of them.
 *
 * **Colour is not chosen here.** Each body takes `var(--ic-<id>)`, and those tokens are a
 * validated categorical palette — see the block in styles.css for where the hues come from and
 * what was checked. Details are white at a few opacities: white on a saturated body is the one
 * pairing that needs no contrast argument, and it keeps every icon to a single hue, which is what
 * lets the palette be validated as a palette rather than as two dozen unrelated colours.
 *
 * Shape carries identity and colour only reinforces it — twelve places share eight hues, so two
 * icons can repeat a colour, never near each other, and every icon has its label beside it.
 */

const S = ({ children }: { children: ReactNode }) => (
  <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false">{children}</svg>
);

export const NAV_ICON: Record<string, ReactNode> = {
  // Pulse — the market's heartbeat, read once a day.
  signals: (
    <S>
      <rect x="2.5" y="3" width="19" height="18" rx="4" fill="var(--ic-signals)" />
      <polyline points="5,13 8.3,13 10.4,7.5 13.6,17 15.7,11 19,11" fill="none" stroke="#fff" strokeWidth="1.9"
        strokeLinejoin="round" strokeLinecap="round" />
    </S>
  ),
  // Calendar — the day you are living.
  schedule: (
    <S>
      <rect x="3" y="5" width="18" height="16" rx="3" fill="var(--ic-schedule)" />
      <rect x="6.5" y="2.5" width="2.5" height="4.5" rx="1.25" fill="var(--ic-schedule)" />
      <rect x="15" y="2.5" width="2.5" height="4.5" rx="1.25" fill="var(--ic-schedule)" />
      <rect x="3" y="8.6" width="18" height="1.5" fill="#fff" opacity=".55" />
      <rect x="6.5" y="12.5" width="4.5" height="4" rx="1.2" fill="#fff" />
    </S>
  ),
  // Week — the same calendar, read in columns.
  weekplan: (
    <S>
      <rect x="2.5" y="5.5" width="19" height="14" rx="3" fill="var(--ic-weekplan)" />
      <rect x="2.5" y="8.6" width="19" height="1.3" fill="#fff" opacity=".45" />
      <rect x="5.5" y="12" width="3" height="4.6" rx="1" fill="#fff" />
      <rect x="10.5" y="12" width="3" height="4.6" rx="1" fill="#fff" opacity=".75" />
      <rect x="15.5" y="12" width="3" height="4.6" rx="1" fill="#fff" opacity=".5" />
    </S>
  ),
  // Roadmap — nodes hanging off a spine.
  roadmap: (
    <S>
      <path d="M7 5v10a3 3 0 0 0 3 3h4" stroke="var(--ic-roadmap)" strokeWidth="2.2" fill="none" strokeLinecap="round" />
      <path d="M7 10h4" stroke="var(--ic-roadmap)" strokeWidth="2.2" fill="none" strokeLinecap="round" />
      <circle cx="7" cy="4.5" r="2.9" fill="var(--ic-roadmap)" />
      <circle cx="16.5" cy="10" r="2.7" fill="var(--ic-roadmap)" />
      <circle cx="16.5" cy="18" r="2.7" fill="var(--ic-roadmap)" />
    </S>
  ),
  // Tasks — a clipboard with one thing done.
  tasks: (
    <S>
      <rect x="4" y="4" width="16" height="17" rx="3" fill="var(--ic-tasks)" />
      <rect x="8" y="2.5" width="8" height="4" rx="1.6" fill="var(--ic-tasks)" />
      <rect x="8" y="2.5" width="8" height="4" rx="1.6" fill="#fff" opacity=".4" />
      <path d="m8 13.5 2.6 2.6L16 10.8" stroke="#fff" strokeWidth="2.3" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </S>
  ),
  // Performance — bars that got taller.
  performance: (
    <S>
      <rect x="3.5" y="13" width="4.5" height="8" rx="1.5" fill="var(--ic-performance)" opacity=".45" />
      <rect x="9.75" y="8.5" width="4.5" height="12.5" rx="1.5" fill="var(--ic-performance)" opacity=".72" />
      <rect x="16" y="3.5" width="4.5" height="17.5" rx="1.5" fill="var(--ic-performance)" />
    </S>
  ),
  // Habits — the loop that comes back round.
  habits: (
    <S>
      <path d="M20 12a8 8 0 1 1-2.4-5.7" stroke="var(--ic-habits)" strokeWidth="2.8" fill="none" strokeLinecap="round" />
      <path d="M20 3v5h-5" stroke="var(--ic-habits)" strokeWidth="2.8" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </S>
  ),
  // Articles — an open book.
  articles: (
    <S>
      <path d="M3 5.5h6.2c1.6 0 2.8.9 2.8 2.1V20c0-1-1.2-1.8-2.8-1.8H3z" fill="var(--ic-articles)" opacity=".7" />
      <path d="M21 5.5h-6.2c-1.6 0-2.8.9-2.8 2.1V20c0-1 1.2-1.8 2.8-1.8H21z" fill="var(--ic-articles)" />
      <rect x="11.2" y="5.5" width="1.6" height="14.5" rx=".8" fill="#fff" opacity=".55" />
    </S>
  ),
  // Courses — a stack of lessons with the one you are on lifted out of it.
  courses: (
    <S>
      <rect x="3" y="4" width="18" height="4.2" rx="1.6" fill="var(--ic-courses)" />
      <rect x="3" y="9.9" width="18" height="4.2" rx="1.6" fill="var(--ic-courses)" opacity=".78" />
      <rect x="3" y="15.8" width="18" height="4.2" rx="1.6" fill="var(--ic-courses)" opacity=".56" />
      <path d="M8.2 11.2l1.6 1.7 3.4-3.6" fill="none" stroke="#fff" strokeWidth="1.9"
        strokeLinecap="round" strokeLinejoin="round" />
      <rect x="6" y="5.4" width="7" height="1.4" rx=".7" fill="#fff" opacity=".8" />
    </S>
  ),
  // Newsletter — a folded paper with a headline.
  newsletter: (
    <S>
      <rect x="2.5" y="4.5" width="19" height="15" rx="2.5" fill="var(--ic-newsletter)" />
      <rect x="5" y="7.5" width="7" height="5" rx="1.2" fill="#fff" />
      <rect x="13.5" y="7.5" width="6" height="1.8" rx=".9" fill="#fff" opacity=".9" />
      <rect x="13.5" y="10.7" width="6" height="1.8" rx=".9" fill="#fff" opacity=".6" />
      <rect x="5" y="14.5" width="14.5" height="2" rx="1" fill="#fff" opacity=".75" />
    </S>
  ),
  // English — a word in a speech bubble.
  english: (
    <S>
      <path d="M4 5.5h16a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-8.5L7 21v-4.5H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z" fill="var(--ic-english)" />
      <path d="M8.2 14 11 7h1.8l2.8 7h-2l-.5-1.4h-2.6L10 14z" fill="#fff" />
    </S>
  ),
  // Notes — a page being written on.
  // Notes v2 — a stack of flashcards, the front one turned to its question mark.
  notesv2: (
    <S>
      <rect x="5" y="7" width="15" height="12" rx="2.5" fill="var(--ic-notesv2)" opacity=".45" />
      <rect x="3" y="4.5" width="15" height="12" rx="2.5" fill="var(--ic-notesv2)" />
      <path d="M8.6 9.2a2 2 0 0 1 3.9.6c0 1.3-1.9 1.4-1.9 2.6" stroke="#fff" strokeWidth="1.7" fill="none" strokeLinecap="round" />
      <circle cx="10.6" cy="14.6" r="1" fill="#fff" />
    </S>
  ),
  notes: (
    <S>
      <rect x="4" y="3" width="14" height="18" rx="2.5" fill="var(--ic-notes)" />
      <rect x="7" y="7" width="8" height="1.8" rx=".9" fill="#fff" />
      <rect x="7" y="11" width="8" height="1.8" rx=".9" fill="#fff" opacity=".8" />
      <rect x="7" y="15" width="5" height="1.8" rx=".9" fill="#fff" opacity=".6" />
      <path d="m15.5 17.5 5-5 2.2 2.2-5 5-2.8.6z" fill="var(--ic-notes)" />
    </S>
  ),
  // Nutrition — an apple, with the roadmap's green for the leaf: the one place two hues meet, and
  // a leaf in the body's own red would not read as a leaf.
  nutrition: (
    <S>
      <path d="M12 7c-1-.8-2.2-1.2-3.4-1.2C5.9 5.8 4 8.2 4 11.6c0 3.8 2.6 9.4 5.3 9.4 1 0 1.7-.5 2.7-.5s1.7.5 2.7.5c2.7 0 5.3-5.6 5.3-9.4 0-3.4-1.9-5.8-4.6-5.8-1.2 0-2.4.4-3.4 1.2z" fill="var(--ic-nutrition)" />
      <path d="M12.4 6.4c0-2 1.4-3.6 3.4-3.9.3 2.2-1.2 3.9-3.4 3.9z" fill="var(--ic-roadmap)" />
    </S>
  ),
  // Jobs — a briefcase.
  jobs: (
    <S>
      <rect x="2.5" y="7" width="19" height="13" rx="2.5" fill="var(--ic-jobs)" />
      <path d="M9 7V5.5A1.5 1.5 0 0 1 10.5 4h3A1.5 1.5 0 0 1 15 5.5V7" stroke="var(--ic-jobs)" strokeWidth="2" fill="none" strokeLinecap="round" />
      <rect x="2.5" y="11.6" width="19" height="1.5" fill="#fff" opacity=".45" />
      <rect x="10.4" y="10.6" width="3.2" height="3.6" rx="1" fill="#fff" />
    </S>
  ),
  // Experiences — a peak with the sun over it: somewhere to go.
  experiences: (
    <S>
      <circle cx="17" cy="7" r="3" fill="var(--ic-experiences)" opacity=".6" />
      <path d="M2.5 20 9.5 8.5l4.2 6.4 2.3-3.1L21.5 20z" fill="var(--ic-experiences)" />
      <path d="M9.5 8.5 11.6 11.9 10.2 11.4 8.7 12.7 7.9 11.1z" fill="#fff" opacity=".85" />
    </S>
  ),
  // Delay — a clock badged with the nudge forward. A clock alone reads as "when" and an arrow
  // alone as "next"; the pair is the only way the glyph says "later" at 18px.
  delay: (
    <S>
      <circle cx="10.5" cy="10.5" r="8.5" fill="var(--ic-delay)" />
      <path d="M10.5 5.6v5h3.8" stroke="#fff" strokeWidth="2.1" fill="none" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="18" cy="18" r="5.6" fill="var(--ic-delay-badge)" />
      <path d="M15.6 18h4.2M18.1 16.2 19.9 18l-1.8 1.8" stroke="#fff" strokeWidth="1.8" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </S>
  ),
  // Theme and the way out — actions rather than places, so they wear ink, not a categorical hue.
  moon: (
    <S><path d="M20.5 14.3A8.8 8.8 0 0 1 9.7 3.5 8.8 8.8 0 1 0 20.5 14.3z" fill="var(--ic-action)" /></S>
  ),
  sun: (
    <S>
      <circle cx="12" cy="12" r="4.6" fill="var(--ic-action)" />
      <g stroke="var(--ic-action)" strokeWidth="2.1" strokeLinecap="round">
        <path d="M12 2.5v2.4M12 19.1v2.4M2.5 12h2.4M19.1 12h2.4M5.2 5.2l1.7 1.7M17.1 17.1l1.7 1.7M18.8 5.2l-1.7 1.7M6.9 17.1l-1.7 1.7" />
      </g>
    </S>
  ),
  logout: (
    <S>
      <path d="M13.5 3.5h-6A2.5 2.5 0 0 0 5 6v12a2.5 2.5 0 0 0 2.5 2.5h6" stroke="var(--ic-action)" strokeWidth="2.2" fill="none" strokeLinecap="round" />
      <path d="M16.5 8.2 20.3 12l-3.8 3.8M19.8 12h-9" stroke="var(--ic-action)" strokeWidth="2.2" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </S>
  ),
  // "More" — the phone's drawer handle. It opens a menu, it isn't a place, so it takes the ink
  // colour of the row it sits in.
  more: (
    <S>
      <rect x="3.5" y="6" width="17" height="2.4" rx="1.2" fill="currentColor" />
      <rect x="3.5" y="10.8" width="17" height="2.4" rx="1.2" fill="currentColor" />
      <rect x="3.5" y="15.6" width="17" height="2.4" rx="1.2" fill="currentColor" />
    </S>
  ),
};


/* ─── Inline badge icons ───
   The app-icon look at text size: a rounded tile with a soft top-to-bottom gradient, a glossy top
   edge, and the glyph knocked out in `ink` (white by default). The tile takes `currentColor`, so a
   badge keeps the meaning of the text it sits in (accent, a price's good/warning colour); on a
   filled button pass `ink` as the button's colour and the tile turns white. */

function Tile({ size, title, ink, tone, children }: {
  size: string | number; title?: string; ink: string; tone?: string; children: (ink: string) => ReactNode;
}) {
  const id = useId().replace(/:/g, '');
  const fill = tone ?? 'currentColor';
  return (
    <svg viewBox="0 0 24 24" width={size} height={size} role={title ? 'img' : undefined}
      aria-hidden={title ? undefined : true} aria-label={title} focusable="false"
      style={{ display: 'inline-block', verticalAlign: '-0.24em', flexShrink: 0, color: tone ? undefined : 'inherit' }}>
      <defs>
        <linearGradient id={`g${id}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor={fill} stopOpacity="1" />
          <stop offset="1" stopColor={fill} stopOpacity=".78" />
        </linearGradient>
        <linearGradient id={`h${id}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#fff" stopOpacity=".35" />
          <stop offset=".5" stopColor="#fff" stopOpacity="0" />
        </linearGradient>
      </defs>
      <rect x="1" y="1" width="22" height="22" rx="6.5" fill={`url(#g${id})`} />
      <rect x="1" y="1" width="22" height="22" rx="6.5" fill={`url(#h${id})`} />
      {children(ink)}
    </svg>
  );
}

/** Balance scales on a tile — the mark for Weighted scoring (no ⚖ emoji anywhere). The user's pick
 *  (2026-11-16, option A of six): a gently curved beam, finial on top, deep pans, a domed foot. */
export function ScalesIcon({ size = '1.3em', title, ink = '#fff', tone }: { size?: string | number; title?: string; ink?: string; tone?: string }) {
  return (
    <Tile size={size} title={title} ink={ink} tone={tone}>{k => (
      <>
        <path d="M12 6v11.2" stroke={k} strokeWidth="1.6" strokeLinecap="round" />
        <path d="M8.4 18.6c.6-1 2-1.6 3.6-1.6s3 .6 3.6 1.6z" fill={k} />
        <path d="M5.2 8.4c2.2-.9 4.5-1.3 6.8-1.3s4.6.4 6.8 1.3" fill="none" stroke={k} strokeWidth="1.5" strokeLinecap="round" />
        <circle cx="12" cy="5.4" r="1.3" fill={k} />
        <path d="M5.2 8.4 3.5 13M5.2 8.4 6.9 13M18.8 8.4 17.1 13M18.8 8.4l1.7 4.6" stroke={k} strokeWidth=".9" strokeLinecap="round" opacity=".85" />
        <path d="M2.9 13h4.6c-.2 1.5-1.2 2.4-2.3 2.4S3.1 14.5 2.9 13z" fill={k} />
        <path d="M16.5 13h4.6c-.2 1.5-1.2 2.4-2.3 2.4s-2.1-.9-2.3-2.4z" fill={k} />
      </>
    )}</Tile>
  );
}

/** A calendar page on a tile — scheduling. */
export function CalendarIcon({ size = '1.3em', title, ink = '#fff', tone }: { size?: string | number; title?: string; ink?: string; tone?: string }) {
  return (
    <Tile size={size} title={title} ink={ink} tone={tone}>{k => (
      <>
        <rect x="5.2" y="6.6" width="13.6" height="12" rx="2.2" fill="none" stroke={k} strokeWidth="1.6" />
        <path d="M5.2 10.2h13.6" stroke={k} strokeWidth="1.6" />
        <path d="M8.8 5v3M15.2 5v3" stroke={k} strokeWidth="1.7" strokeLinecap="round" />
        <circle cx="9" cy="13.4" r="1.05" fill={k} /><circle cx="12" cy="13.4" r="1.05" fill={k} /><circle cx="15" cy="13.4" r="1.05" fill={k} />
        <circle cx="9" cy="16.1" r="1.05" fill={k} /><circle cx="12" cy="16.1" r="1.05" fill={k} opacity=".55" />
      </>
    )}</Tile>
  );
}

/** An open book on a tile — the meal book. */
export function BookIcon({ size = '1.3em', title, ink = '#fff', tone }: { size?: string | number; title?: string; ink?: string; tone?: string }) {
  return (
    <Tile size={size} title={title} ink={ink} tone={tone}>{k => (
      <>
        <path d="M12 8.2c-1.6-1.3-3.8-1.8-6.6-1.6v10.6c2.8-.2 5 .3 6.6 1.6 1.6-1.3 3.8-1.8 6.6-1.6V6.6c-2.8-.2-5 .3-6.6 1.6Z" fill={k} fillOpacity=".22" stroke={k} strokeWidth="1.5" strokeLinejoin="round" />
        <path d="M12 8.2v10.6" stroke={k} strokeWidth="1.5" />
        <path d="M7.6 10.2c1.1 0 2 .2 2.8.6M7.6 12.8c1.1 0 2 .2 2.8.6M13.6 10.8c.8-.4 1.7-.6 2.8-.6" stroke={k} strokeWidth="1.1" strokeLinecap="round" opacity=".9" />
      </>
    )}</Tile>
  );
}

/** A plate with fork and knife on a tile — the food log. */
export function PlateIcon({ size = '1.3em', title, ink = '#fff', tone }: { size?: string | number; title?: string; ink?: string; tone?: string }) {
  return (
    <Tile size={size} title={title} ink={ink} tone={tone}>{k => (
      <>
        <circle cx="12" cy="12.4" r="4.6" fill="none" stroke={k} strokeWidth="1.5" />
        <circle cx="12" cy="12.4" r="2.4" fill={k} fillOpacity=".35" />
        <path d="M5 6.2v4.2a1.3 1.3 0 0 0 1.3 1.3M7.6 6.2v4.2a1.3 1.3 0 0 1-1.3 1.3M6.3 6.2v12" stroke={k} strokeWidth="1.2" strokeLinecap="round" fill="none" />
        <path d="M18.6 18.2V6.4c-1.3.7-1.9 2.6-1.9 4.6 0 1 .6 1.6 1.9 1.6" stroke={k} strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round" fill="none" />
      </>
    )}</Tile>
  );
}
