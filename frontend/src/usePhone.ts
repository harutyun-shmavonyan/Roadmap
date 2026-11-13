import { useCallback, useEffect, useLayoutEffect, useRef, useState, type RefObject } from 'react';

// The phone breakpoint, the same 768px the stylesheet's mobile block uses. For layouts CSS
// alone cannot change — a list beside its detail becomes a list *then* its detail.
const PHONE = '(max-width: 768px)';

export function usePhone(): boolean {
  const [phone, setPhone] = useState(() => typeof window !== 'undefined' && window.matchMedia(PHONE).matches);
  useEffect(() => {
    const mq = window.matchMedia(PHONE);
    const on = () => setPhone(mq.matches);
    mq.addEventListener('change', on);
    return () => mq.removeEventListener('change', on);
  }, []);
  return phone;
}

/** A list beside its detail, on a phone: the list first, the detail on its own screen. Opening
 *  the detail starts it at the top of the tab's scroller; going back returns the list to where
 *  it was. `shell` is that scroller (the m-shell); on a desktop it does not scroll, so the
 *  scrollTop writes are no-ops and `open` is simply ignored by the caller. */
export function usePhoneDetail(shell: RefObject<HTMLElement | null>) {
  const [open, setOpen] = useState(false);
  const saved = useRef(0);
  const show = useCallback(() => { saved.current = shell.current?.scrollTop ?? 0; setOpen(true); }, [shell]);
  const back = useCallback(() => setOpen(false), []);
  useLayoutEffect(() => {
    const el = shell.current;
    if (el) el.scrollTop = open ? 0 : saved.current;
  }, [open, shell]);
  return { open, show, back };
}
