import type { ReactNode } from 'react';

/**
 * The app's icons: a small flat set drawn here rather than pulled from a library or the platform's
 * emoji font. Mostly navigation, plus the odd action that sits in a row of them.
 *
 * Emoji were the previous answer and they look like whatever the reader's OS decides — three
 * different drawing styles in one column, and a different set again on the phone. These are one
 * family: the same 24-grid, a saturated body in the item's own colour with white cut-outs for the
 * detail, so they stay legible on both the light and the dark ground without restating any colour
 * per theme.
 */

const S = ({ children }: { children: ReactNode }) => (
  <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false">{children}</svg>
);

/**
 * The icons' paint servers: a top-left-to-bottom-right gradient per body. Flat fills go dull at
 * 18px; two stops of one hue give the shape a light side and keep it from reading as a sticker.
 *
 * They live in one node rendered once, not in each icon: an icon can be on screen twice (the rail
 * and the phone's bottom bar), and the rail is display:none on a phone — Blink will not resolve
 * url(#id) against a gradient inside an unrendered subtree, so the duplicate that happened to come
 * first left the bodies unpainted.
 */
const GRADIENTS: [id: string, from: string, to: string][] = [
  ['g-sched', '#60a5fa', '#2563eb'],
  ['g-week', '#818cf8', '#4f46e5'],
  ['g-tasks', '#fbbf24', '#d97706'],
  ['g-jobs', '#ca8a04', '#854d0e'],
  ['g-notes', '#a3e635', '#65a30d'],
  ['g-eng', '#22d3ee', '#0891b2'],
  ['g-nut', '#f87171', '#dc2626'],
  ['g-art', '#fdba74', '#f97316'],
  ['g-news', '#34d399', '#059669'],
];

/** Render once, high in the tree and never inside anything hidden. */
export const IconDefs = () => (
  <svg aria-hidden="true" focusable="false" style={{ position: 'absolute', width: 0, height: 0, overflow: 'hidden' }}>
    <defs>
      {GRADIENTS.map(([id, from, to]) => (
        <linearGradient key={id} id={id} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor={from} />
          <stop offset="1" stopColor={to} />
        </linearGradient>
      ))}
    </defs>
  </svg>
);

export const NAV_ICON: Record<string, ReactNode> = {
  // Calendar — the day you are living.
  schedule: (
    <S>
      <rect x="3" y="5" width="18" height="16" rx="3" fill="url(#g-sched)" />
      <rect x="3" y="5" width="18" height="4.5" rx="3" fill="#1d4ed8" />
      <rect x="6.5" y="2.5" width="2.5" height="4.5" rx="1.25" fill="#1d4ed8" />
      <rect x="15" y="2.5" width="2.5" height="4.5" rx="1.25" fill="#1d4ed8" />
      <rect x="6.5" y="12" width="4.5" height="4" rx="1.2" fill="#fff" />
    </S>
  ),
  // Week — the same calendar, read in columns.
  weekplan: (
    <S>
      <rect x="2.5" y="5.5" width="19" height="14" rx="3" fill="url(#g-week)" />
      <rect x="2.5" y="5.5" width="19" height="4" rx="3" fill="#4338ca" />
      <rect x="5.5" y="12" width="3" height="4.5" rx="1" fill="#fff" />
      <rect x="10.5" y="12" width="3" height="4.5" rx="1" fill="#fff" opacity=".75" />
      <rect x="15.5" y="12" width="3" height="4.5" rx="1" fill="#fff" opacity=".5" />
    </S>
  ),
  // Roadmap — nodes hanging off a spine.
  roadmap: (
    <S>
      <path d="M7 5v10a3 3 0 0 0 3 3h4" stroke="#15803d" strokeWidth="2" fill="none" strokeLinecap="round" />
      <path d="M7 10h4" stroke="#15803d" strokeWidth="2" fill="none" strokeLinecap="round" />
      <circle cx="7" cy="4.5" r="2.8" fill="#16a34a" />
      <circle cx="16.5" cy="10" r="2.6" fill="#16a34a" />
      <circle cx="16.5" cy="18" r="2.6" fill="#16a34a" />
    </S>
  ),
  // Tasks — a clipboard with one thing done.
  tasks: (
    <S>
      <rect x="4" y="4" width="16" height="17" rx="3" fill="url(#g-tasks)" />
      <rect x="8" y="2.5" width="8" height="4" rx="1.6" fill="#b45309" />
      <path d="m8 13.5 2.6 2.6L16 10.8" stroke="#fff" strokeWidth="2.2" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </S>
  ),
  // Performance — bars that got taller.
  performance: (
    <S>
      <rect x="3.5" y="13" width="4.5" height="8" rx="1.5" fill="#c4b5fd" />
      <rect x="9.75" y="8.5" width="4.5" height="12.5" rx="1.5" fill="#a78bfa" />
      <rect x="16" y="3.5" width="4.5" height="17.5" rx="1.5" fill="#7c3aed" />
    </S>
  ),
  // Habits — the loop that comes back round.
  habits: (
    <S>
      <path d="M20 12a8 8 0 1 1-2.4-5.7" stroke="#14b8a6" strokeWidth="2.6" fill="none" strokeLinecap="round" />
      <path d="M20 3v5h-5" stroke="#0f766e" strokeWidth="2.6" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </S>
  ),
  // Articles — an open book.
  articles: (
    <S>
      <path d="M3 5.5h6.2c1.6 0 2.8.9 2.8 2.1V20c0-1-1.2-1.8-2.8-1.8H3z" fill="url(#g-art)" />
      <path d="M21 5.5h-6.2c-1.6 0-2.8.9-2.8 2.1V20c0-1 1.2-1.8 2.8-1.8H21z" fill="#ea580c" />
      <rect x="11.2" y="5.5" width="1.6" height="14.5" rx=".8" fill="#9a3412" />
    </S>
  ),
  // Newsletter — a folded paper with a headline. Green: it leads the phone's quick row.
  newsletter: (
    <S>
      <rect x="2.5" y="4.5" width="19" height="15" rx="2.5" fill="url(#g-news)" />
      <rect x="5" y="7.5" width="7" height="5" rx="1.2" fill="#fff" />
      <rect x="13.5" y="7.5" width="6" height="1.8" rx=".9" fill="#fff" opacity=".9" />
      <rect x="13.5" y="10.7" width="6" height="1.8" rx=".9" fill="#fff" opacity=".6" />
      <rect x="5" y="14.5" width="14.5" height="2" rx="1" fill="#fff" opacity=".75" />
    </S>
  ),
  // English — a word in a speech bubble.
  english: (
    <S>
      <path d="M4 5.5h16a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-8.5L7 21v-4.5H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z" fill="url(#g-eng)" />
      <path d="M8.2 14 11 7h1.8l2.8 7h-2l-.5-1.4h-2.6L10 14z" fill="#fff" />
    </S>
  ),
  // Notes — a page being written on.
  notes: (
    <S>
      <rect x="4" y="3" width="14" height="18" rx="2.5" fill="url(#g-notes)" />
      <rect x="7" y="7" width="8" height="1.8" rx=".9" fill="#fff" />
      <rect x="7" y="11" width="8" height="1.8" rx=".9" fill="#fff" opacity=".8" />
      <rect x="7" y="15" width="5" height="1.8" rx=".9" fill="#fff" opacity=".6" />
      <path d="m15.5 17.5 5-5 2.2 2.2-5 5-2.8.6z" fill="#4d7c0f" />
    </S>
  ),
  // Nutrition — an apple.
  nutrition: (
    <S>
      <path d="M12 7c-1-.8-2.2-1.2-3.4-1.2C5.9 5.8 4 8.2 4 11.6c0 3.8 2.6 9.4 5.3 9.4 1 0 1.7-.5 2.7-.5s1.7.5 2.7.5c2.7 0 5.3-5.6 5.3-9.4 0-3.4-1.9-5.8-4.6-5.8-1.2 0-2.4.4-3.4 1.2z" fill="url(#g-nut)" />
      <path d="M12.4 6.4c0-2 1.4-3.6 3.4-3.9.3 2.2-1.2 3.9-3.4 3.9z" fill="#16a34a" />
    </S>
  ),
  // Jobs — a briefcase.
  jobs: (
    <S>
      <rect x="2.5" y="7" width="19" height="13" rx="2.5" fill="url(#g-jobs)" />
      <path d="M9 7V5.5A1.5 1.5 0 0 1 10.5 4h3A1.5 1.5 0 0 1 15 5.5V7" stroke="#713f12" strokeWidth="2" fill="none" strokeLinecap="round" />
      <rect x="2.5" y="11.5" width="19" height="2.4" fill="#78350f" opacity=".55" />
      <rect x="10.4" y="10.8" width="3.2" height="3.8" rx="1" fill="#fde68a" />
    </S>
  ),
  // A moon to go dark, a sun to come back — the only two that state an action rather than a place,
  // so they carry the one warm colour in the set and nothing else.
  moon: (
    <S>
      <path d="M20.5 14.3A8.8 8.8 0 0 1 9.7 3.5 8.8 8.8 0 1 0 20.5 14.3z" fill="#fbbf24" />
    </S>
  ),
  sun: (
    <S>
      <circle cx="12" cy="12" r="4.6" fill="#fbbf24" />
      <g stroke="#f59e0b" strokeWidth="2.1" strokeLinecap="round">
        <path d="M12 2.5v2.4M12 19.1v2.4M2.5 12h2.4M19.1 12h2.4M5.2 5.2l1.7 1.7M17.1 17.1l1.7 1.7M18.8 5.2l-1.7 1.7M6.9 17.1l-1.7 1.7" />
      </g>
    </S>
  ),
  // The way out.
  logout: (
    <S>
      <path d="M13.5 3.5h-6A2.5 2.5 0 0 0 5 6v12a2.5 2.5 0 0 0 2.5 2.5h6" stroke="#94a3b8" strokeWidth="2.2" fill="none" strokeLinecap="round" />
      <path d="M16.5 8.2 20.3 12l-3.8 3.8M19.8 12h-9" stroke="#64748b" strokeWidth="2.2" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </S>
  ),

  // Delay — a clock, badged with the nudge forward. Two marks rather than one, because a clock
  // alone reads as "when" and an arrow alone as "next"; the pair is the only way the glyph says
  // "later" at 18px.
  delay: (
    <S>
      <circle cx="10.5" cy="10.5" r="8.5" fill="#64748b" />
      <path d="M10.5 5.6v5h3.8" stroke="#fff" strokeWidth="2.1" fill="none" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="18" cy="18" r="5.6" fill="#f59e0b" />
      <path d="M15.6 18h4.2M18.1 16.2 19.9 18l-1.8 1.8" stroke="#fff" strokeWidth="1.8" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </S>
  ),

  // "More" — the phone's drawer handle. Monochrome on purpose: it opens a menu, it isn't a place.
  more: (
    <S>
      <rect x="3.5" y="6" width="17" height="2.4" rx="1.2" fill="currentColor" />
      <rect x="3.5" y="10.8" width="17" height="2.4" rx="1.2" fill="currentColor" />
      <rect x="3.5" y="15.6" width="17" height="2.4" rx="1.2" fill="currentColor" />
    </S>
  ),
};
