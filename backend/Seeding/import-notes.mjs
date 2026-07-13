#!/usr/bin/env node
// Generate idempotent SQL to import a folder of daily-note .md files into the `notes` table.
//
// Filename format expected:  001 — 20.08.2018.md   (DayNumber — DD.MM.YYYY.md)
// The dash may be em (—), en (–) or hyphen (-); surrounding spaces are flexible.
//
// Usage:
//   node import-notes.mjs --dir "C:/path/to/green-notes" [--book green] [--out notes.sql]
//
// Then apply:  psql "$DATABASE_URL" -f notes.sql
//
// Semantics: the folder is the authoritative source for the book. The script deletes
// the book's existing rows, then inserts one row per file. This is fully idempotent
// (re-runnable) and sidesteps both unique constraints — (Book, EntryDate) AND
// (Book, DayNumber) — which a single ON CONFLICT clause cannot both satisfy.
// NOTE: this wipes any rows for that book first, so don't use it to merge app-added notes.

import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

function arg(name, def) {
  const i = process.argv.indexOf(`--${name}`);
  return i !== -1 && process.argv[i + 1] ? process.argv[i + 1] : def;
}

const dir = arg('dir');
const book = (arg('book', 'green')).toLowerCase();
const out = arg('out', 'notes.sql');

if (!dir) { console.error('Error: --dir is required'); process.exit(1); }
if (book !== 'red' && book !== 'green') { console.error("Error: --book must be 'red' or 'green'"); process.exit(1); }

const NAME_RE = /^(\d+)\s*[—–-]\s*(\d{2})\.(\d{2})\.(\d{4})\.md$/i;
const sqlStr = (s) => "'" + s.replace(/'/g, "''") + "'";

const files = readdirSync(dir).filter(f => f.toLowerCase().endsWith('.md'));
const rows = [];
const skipped = [];

for (const f of files) {
  const m = f.match(NAME_RE);
  if (!m) { skipped.push(f); continue; }
  const dayNumber = parseInt(m[1], 10);
  const entryDate = `${m[4]}-${m[3]}-${m[2]}`; // DD.MM.YYYY -> YYYY-MM-DD
  const content = readFileSync(join(dir, f), 'utf8').replace(/\r\n/g, '\n');
  rows.push({ dayNumber, entryDate, content });
}

rows.sort((a, b) => a.dayNumber - b.dayNumber);

// Merge entries that share a calendar date (the schema allows one row per (book, date)).
// Keep the lowest day_number; join the contents in day_number order with a markdown rule.
const byDate = new Map();
const merges = [];
for (const r of rows) {
  const ex = byDate.get(r.entryDate);
  if (!ex) { byDate.set(r.entryDate, r); continue; }
  ex.content = ex.content.replace(/\n+$/, '') + '\n\n---\n\n' + r.content;
  merges.push(`${r.entryDate}: day ${r.dayNumber} merged into day ${ex.dayNumber}`);
}
const merged = [...byDate.values()].sort((a, b) => a.dayNumber - b.dayNumber);

const lines = [
  `-- Daily notes import for book='${book}' — ${merged.length} row(s) from ${rows.length} file(s)`,
  `-- Generated from: ${dir}`,
  `-- Replaces the entire '${book}' book with the folder's contents (re-runnable).`,
  `BEGIN;`,
  `DELETE FROM notes WHERE "Book" = '${book}';`,
  ``,
];

for (const r of merged) {
  lines.push(
    `INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")`,
    `VALUES (gen_random_uuid(), '${book}', ${r.dayNumber}, DATE '${r.entryDate}', ${sqlStr(r.content)}, now(), now());`,
    ``,
  );
}

lines.push(`COMMIT;`, ``);
writeFileSync(out, lines.join('\n'), 'utf8');

console.log(`Wrote ${merged.length} row(s) from ${rows.length} file(s) to ${out}`);
if (merges.length) console.log(`Merged ${merges.length} same-date file(s):\n  ` + merges.join('\n  '));
if (skipped.length) console.log(`Skipped ${skipped.length} file(s) not matching "NNN — DD.MM.YYYY.md":\n  ` + skipped.join('\n  '));
