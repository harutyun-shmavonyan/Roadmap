/** Normalize stored unit codes to singular display form */
function normalizeUnit(unit: string): string {
  const map: Record<string, string> = {
    'u': 'time', 'h': 'hour', 'page': 'page', 'pages': 'page',
    'hour': 'hour', 'hours': 'hour', 'unit': 'time', 'units': 'time',
    'time': 'time', 'times': 'time',
    'word': 'word', 'words': 'word',
    'km': 'km', 'kms': 'km',
  };
  return map[unit.toLowerCase()] ?? unit;
}

/** Format a number with its unit, using plural form when amount !== 1 */
export function fmtUnit(amount: number, unit?: string | null): string {
  if (!unit) return String(Math.round(amount * 10) / 10);
  const rounded = Math.round(amount * 10) / 10;
  const norm = normalizeUnit(unit);
  const plural = rounded === 1 ? norm : `${norm}s`;
  return `${rounded} ${plural}`;
}

/** Just the unit label, pluralized */
export function unitLabel(amount: number, unit?: string | null): string {
  if (!unit) return '';
  const norm = normalizeUnit(unit);
  return amount === 1 ? norm : `${norm}s`;
}
