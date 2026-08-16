import { useState, useEffect, useCallback } from 'react';
import type { MealDto, MealSlot, SaveMealRequest } from './types';
import { api } from './api';

const SLOTS: MealSlot[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

// Split a textarea into a clean list (one item per line) and back again.
const toLines = (text: string): string[] => text.split('\n').map(s => s.trim()).filter(Boolean);
const toCsv = (text: string): string[] => text.split(',').map(s => s.trim()).filter(Boolean);

// "" -> null so an empty macro field stays unknown rather than becoming 0.
const toNum = (text: string): number | null => {
  const t = text.trim();
  if (!t) return null;
  const n = Number(t);
  return Number.isFinite(n) ? Math.max(0, Math.round(n)) : null;
};

const avg = (xs: number[]): number | null =>
  xs.length === 0 ? null : Math.round(xs.reduce((a, b) => a + b, 0) / xs.length);

export function NutritionPage() {
  const [slot, setSlot] = useState<MealSlot>('Breakfast');
  const [meals, setMeals] = useState<MealDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [detail, setDetail] = useState<MealDto | null>(null);
  // 'new' opens an empty form; a meal opens it prefilled.
  const [editing, setEditing] = useState<MealDto | 'new' | null>(null);

  const load = useCallback(async (s: MealSlot) => {
    setLoading(true); setError(null);
    try { setMeals(await api.getMeals(s)); }
    catch { setError('Could not load meals.'); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { load(slot); }, [slot, load]);

  // Esc closes whichever layer is on top.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return;
      if (editing) setEditing(null);
      else if (detail) setDetail(null);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [editing, detail]);

  const save = async (body: SaveMealRequest) => {
    const saved = editing && editing !== 'new'
      ? await api.updateMeal(editing.id, body)
      : await api.createMeal(body);
    setEditing(null);
    setDetail(d => (d && d.id === saved.id ? saved : d));
    // A slot change moves the meal out of the current list; reload whatever is shown.
    await load(slot);
  };

  const remove = async (id: string) => {
    await api.deleteMeal(id);
    setDetail(null); setEditing(null);
    await load(slot);
  };

  const toggleFavorite = async (id: string) => {
    const updated = await api.toggleMealFavorite(id);
    setDetail(d => (d && d.id === id ? updated : d));
    await load(slot);
  };

  const kcals = meals.map(m => m.calories).filter((n): n is number => n != null);
  const proteins = meals.map(m => m.proteinG).filter((n): n is number => n != null);
  const avgKcal = avg(kcals), avgProtein = avg(proteins);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Slot switcher + the line that says how this slot is doing */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '12px 20px',
        borderBottom: '1px solid var(--border-subtle)', flexShrink: 0, flexWrap: 'wrap' }}>
        <div className="nav-tabs">
          {SLOTS.map(s => (
            <button key={s} className={`nav-tab ${slot === s ? 'active' : ''}`} onClick={() => setSlot(s)}>{s}</button>
          ))}
        </div>
        <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>
          {meals.length} meal{meals.length === 1 ? '' : 's'}
          {avgKcal != null && <> · avg {avgKcal} kcal</>}
          {avgProtein != null && <> · {avgProtein} g protein</>}
        </span>
        <button className="btn btn-accent btn-sm" style={{ marginLeft: 'auto' }}
          onClick={() => setEditing('new')}>+ Add meal</button>
      </div>

      <div style={{ flex: 1, overflowY: 'auto', padding: 20 }}>
        {loading ? (
          <div style={{ color: 'var(--text-muted)' }}>Loading...</div>
        ) : error ? (
          <div style={{ color: 'var(--danger)' }}>{error}</div>
        ) : meals.length === 0 ? (
          <div style={{ textAlign: 'center', color: 'var(--text-muted)', padding: '60px 20px' }}>
            <div style={{ fontSize: 32, marginBottom: 12 }}>🥣</div>
            <div style={{ fontSize: 15, marginBottom: 16, color: 'var(--text-secondary)' }}>
              Nothing saved for {slot.toLowerCase()} yet.
            </div>
            <button className="btn btn-accent btn-sm" onClick={() => setEditing('new')}>+ Add the first one</button>
          </div>
        ) : (
          <div style={{ display: 'grid', gap: 14, gridTemplateColumns: 'repeat(auto-fill, minmax(290px, 1fr))' }}>
            {meals.map(m => (
              <MealCard key={m.id} meal={m} onOpen={() => setDetail(m)} onStar={() => toggleFavorite(m.id)} />
            ))}
          </div>
        )}
      </div>

      {detail && !editing && (
        <MealModal meal={detail} onClose={() => setDetail(null)}
          onEdit={() => setEditing(detail)} onDelete={() => remove(detail.id)}
          onStar={() => toggleFavorite(detail.id)} />
      )}

      {editing && (
        <MealForm meal={editing === 'new' ? null : editing} defaultSlot={slot}
          onCancel={() => setEditing(null)} onSave={save} />
      )}
    </div>
  );
}

// kcal in front, then the macro split — plain numbers, no chrome.
function Macros({ meal, size = 'sm' }: { meal: MealDto; size?: 'sm' | 'lg' }) {
  const parts: string[] = [];
  if (meal.proteinG != null) parts.push(`${meal.proteinG}P`);
  if (meal.carbsG != null) parts.push(`${meal.carbsG}C`);
  if (meal.fatG != null) parts.push(`${meal.fatG}F`);
  if (meal.calories == null && parts.length === 0) return null;
  return (
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, flexWrap: 'wrap' }}>
      {meal.calories != null && (
        <span style={{ fontSize: size === 'lg' ? 22 : 18, fontWeight: 700, color: 'var(--text-primary)', lineHeight: 1 }}>
          {meal.calories}
          <span style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-muted)', marginLeft: 3 }}>kcal</span>
        </span>
      )}
      {parts.length > 0 && (
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: size === 'lg' ? 13 : 12, color: 'var(--text-muted)' }}>
          {parts.join(' · ')}
        </span>
      )}
    </div>
  );
}

function Star({ on, onClick }: { on: boolean; onClick: () => void }) {
  return (
    <button type="button" className="btn btn-ghost btn-sm" title={on ? 'Unstar' : 'Star'}
      onClick={e => { e.stopPropagation(); onClick(); }}
      style={{ padding: '2px 6px', fontSize: 15, color: on ? 'var(--warning)' : 'var(--text-muted)' }}>
      {on ? '★' : '☆'}
    </button>
  );
}

function Chip({ text }: { text: string }) {
  return (
    <span style={{ fontSize: 11, fontWeight: 600, padding: '3px 9px', borderRadius: 999,
      background: 'var(--bg-secondary)', color: 'var(--text-muted)', border: '1px solid var(--border-subtle)' }}>
      {text}
    </span>
  );
}

function MealCard({ meal, onOpen, onStar }: { meal: MealDto; onOpen: () => void; onStar: () => void }) {
  return (
    <div className="meal-card" onClick={onOpen} title="Open the recipe"
      style={{ display: 'flex', flexDirection: 'column', gap: 10, padding: '16px 18px', cursor: 'pointer',
        background: 'var(--bg-primary)', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)' }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 8 }}>
        <div style={{ flex: 1, minWidth: 0, fontSize: 16, fontWeight: 600, lineHeight: 1.3 }}>{meal.name}</div>
        <Star on={meal.isFavorite} onClick={onStar} />
      </div>

      {meal.summary && (
        <div style={{ fontSize: 13, lineHeight: 1.5, color: 'var(--text-secondary)' }}>{meal.summary}</div>
      )}

      <Macros meal={meal} />

      {(meal.prepMinutes != null || meal.tags.length > 0) && (
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {meal.prepMinutes != null && <Chip text={`⏱ ${meal.prepMinutes} min`} />}
          {meal.tags.map(t => <Chip key={t} text={t} />)}
        </div>
      )}
    </div>
  );
}

function MealModal({ meal, onClose, onEdit, onDelete, onStar }: {
  meal: MealDto; onClose: () => void; onEdit: () => void; onDelete: () => void; onStar: () => void;
}) {
  const [confirm, setConfirm] = useState(false);
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 520 }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 8, marginBottom: 6 }}>
          <h2 style={{ flex: 1, marginBottom: 0 }}>{meal.name}</h2>
          <Star on={meal.isFavorite} onClick={onStar} />
        </div>

        {meal.summary && (
          <div style={{ fontSize: 14, lineHeight: 1.6, color: 'var(--text-secondary)', marginBottom: 14 }}>{meal.summary}</div>
        )}

        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap', marginBottom: 18 }}>
          <Macros meal={meal} size="lg" />
          {meal.prepMinutes != null && <Chip text={`⏱ ${meal.prepMinutes} min`} />}
          {meal.tags.map(t => <Chip key={t} text={t} />)}
        </div>

        {meal.ingredients.length > 0 && (
          <Section title="Ingredients">
            <ul style={{ margin: 0, paddingLeft: 18, fontSize: 14, lineHeight: 1.7, color: 'var(--text-secondary)' }}>
              {meal.ingredients.map((i, n) => <li key={n}>{i}</li>)}
            </ul>
          </Section>
        )}

        {meal.steps.length > 0 && (
          <Section title="Method">
            <ol style={{ margin: 0, paddingLeft: 18, fontSize: 14, lineHeight: 1.7, color: 'var(--text-secondary)' }}>
              {meal.steps.map((s, n) => <li key={n} style={{ marginBottom: 4 }}>{s}</li>)}
            </ol>
          </Section>
        )}

        <div className="modal-actions" style={{ justifyContent: 'space-between' }}>
          <button type="button" className={`btn btn-sm ${confirm ? 'btn-danger' : 'btn-ghost'}`}
            onClick={() => (confirm ? onDelete() : setConfirm(true))}>
            {confirm ? 'Delete for good?' : 'Delete'}
          </button>
          <div style={{ display: 'flex', gap: 8 }}>
            <button type="button" className="btn btn-sm" onClick={onClose}>Close</button>
            <button type="button" className="btn btn-sm btn-accent" onClick={onEdit}>Edit</button>
          </div>
        </div>
      </div>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{ marginBottom: 18 }}>
      <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.06em', textTransform: 'uppercase',
        color: 'var(--text-muted)', marginBottom: 8 }}>{title}</div>
      {children}
    </div>
  );
}

function MealForm({ meal, defaultSlot, onCancel, onSave }: {
  meal: MealDto | null; defaultSlot: MealSlot; onCancel: () => void; onSave: (body: SaveMealRequest) => Promise<void>;
}) {
  const [slot, setSlot] = useState<MealSlot>(meal?.slot ?? defaultSlot);
  const [name, setName] = useState(meal?.name ?? '');
  const [summary, setSummary] = useState(meal?.summary ?? '');
  const [ingredients, setIngredients] = useState((meal?.ingredients ?? []).join('\n'));
  const [steps, setSteps] = useState((meal?.steps ?? []).join('\n'));
  const [calories, setCalories] = useState(meal?.calories?.toString() ?? '');
  const [protein, setProtein] = useState(meal?.proteinG?.toString() ?? '');
  const [carbs, setCarbs] = useState(meal?.carbsG?.toString() ?? '');
  const [fat, setFat] = useState(meal?.fatG?.toString() ?? '');
  const [prep, setPrep] = useState(meal?.prepMinutes?.toString() ?? '');
  const [tags, setTags] = useState((meal?.tags ?? []).join(', '));
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const submit = async () => {
    if (!name.trim() || busy) return;
    setBusy(true); setErr(null);
    try {
      await onSave({
        slot, name: name.trim(), summary: summary.trim() || null,
        ingredients: toLines(ingredients), steps: toLines(steps),
        calories: toNum(calories), proteinG: toNum(protein), carbsG: toNum(carbs), fatG: toNum(fat),
        prepMinutes: toNum(prep), tags: toCsv(tags),
      });
    } catch {
      setErr('Could not save. Try again.');
      setBusy(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 520 }}>
        <h2>{meal ? 'Edit meal' : 'New meal'}</h2>

        <label>Name</label>
        <input type="text" value={name} autoFocus onChange={e => setName(e.target.value)}
          placeholder="Greek yogurt, berries & walnuts" />

        <label>Why it's good</label>
        <input type="text" value={summary} onChange={e => setSummary(e.target.value)}
          placeholder="One line — what this meal does for you" />

        <div className="form-row">
          <div>
            <label>Slot</label>
            <select value={slot} onChange={e => setSlot(e.target.value as MealSlot)}>
              {SLOTS.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
          </div>
          <div>
            <label>Prep (min)</label>
            <input type="number" min={0} value={prep} onChange={e => setPrep(e.target.value)} placeholder="10" />
          </div>
        </div>

        <div className="form-row">
          <div><label>Kcal</label><input type="number" min={0} value={calories} onChange={e => setCalories(e.target.value)} placeholder="420" /></div>
          <div><label>Protein</label><input type="number" min={0} value={protein} onChange={e => setProtein(e.target.value)} placeholder="30" /></div>
          <div><label>Carbs</label><input type="number" min={0} value={carbs} onChange={e => setCarbs(e.target.value)} placeholder="35" /></div>
          <div><label>Fat</label><input type="number" min={0} value={fat} onChange={e => setFat(e.target.value)} placeholder="20" /></div>
        </div>

        <label>Ingredients — one per line</label>
        <textarea value={ingredients} onChange={e => setIngredients(e.target.value)} rows={5}
          placeholder={'250 g Greek yogurt\n100 g berries\n20 g walnuts'} />

        <label>Method — one step per line</label>
        <textarea value={steps} onChange={e => setSteps(e.target.value)} rows={4}
          placeholder={'Spoon the yogurt into a bowl.\nTop with the berries and walnuts.'} />

        <label>Tags — comma separated</label>
        <input type="text" value={tags} onChange={e => setTags(e.target.value)} placeholder="high-protein, no-cook" />

        {err && <div style={{ color: 'var(--danger)', fontSize: 13, marginBottom: 8 }}>{err}</div>}

        <div className="modal-actions">
          <button type="button" className="btn btn-sm" onClick={onCancel}>Cancel</button>
          <button type="button" className="btn btn-sm btn-accent" onClick={submit} disabled={!name.trim() || busy}>
            {busy ? 'Saving...' : meal ? 'Save' : 'Add meal'}
          </button>
        </div>
      </div>
    </div>
  );
}
