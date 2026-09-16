import type { ReactNode } from 'react';

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
