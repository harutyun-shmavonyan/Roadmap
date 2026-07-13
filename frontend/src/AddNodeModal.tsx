import { useState, useRef, useEffect, useCallback } from 'react';
import type { NodeDto, ScheduleTemplate, NodeSubPointDto } from './types';
import { api } from './api';

const DAYS = ['Mon','Tue','Wed','Thu','Fri','Sat','Sun'];
const DAY_VALUES = [1,2,3,4,5,6,0];
const UNITS = ['','page','hour','time','word','km'];
const UNIT_LABELS: Record<string,string> = { '': '(none)', 'page': 'Pages', 'hour': 'Hours', 'time': 'Times', 'word': 'Words', 'km': 'Kilometers' };
const fmt = (m: number) => { const h=Math.floor(m/60),mm=m%60,ap=h>=12?'PM':'AM',h12=h===0?12:h>12?h-12:h; return `${h12}:${mm.toString().padStart(2,'0')} ${ap}`; };
export const TIMES = Array.from({length:48},(_,i)=>i*30);
export const DURATIONS = [15,30,45,60,90,120,180,240];

interface PerDayEntry { startMinute: number; durationMinutes: number; }

interface AddProps { onSubmit: (t:string,a:boolean,u?:string,ts?:number,uph?:number,ppu?:number,s?:string,checklist?:boolean)=>void; onCancel:()=>void; }

export function AddNodeModal({ onSubmit, onCancel }: AddProps) {
  const [title,setTitle]=useState(''); const [isAct,setIsAct]=useState(false);
  const [isChecklist,setIsChecklist]=useState(false);
  const [unit,setUnit]=useState(''); const [totalSize,setTotalSize]=useState('');
  const [uph,setUph]=useState(''); const [ppu,setPpu]=useState('');
  const [sDays,setSDays]=useState<number[]>([]); const [sStart,setSStart]=useState(540); const [sDur,setSDur]=useState(60);
  const [perDay,setPerDay]=useState<Record<string,PerDayEntry>>({});
  const [showPerDay,setShowPerDay]=useState(false);
  const ref=useRef<HTMLInputElement>(null); useEffect(()=>{ref.current?.focus();},[]);
  const toggleDay=(v:number)=>setSDays(p=>p.includes(v)?p.filter(x=>x!==v):[...p,v].sort());
  const submit=()=>{ const t2=title.trim(); if(!t2)return;
    let s:string|undefined; if(isAct&&sDays.length>0) {
      const obj: any = {days:sDays,startMinute:sStart,durationMinutes:sDur};
      if(showPerDay && Object.keys(perDay).length>0) obj.perDay = perDay;
      s=JSON.stringify(obj);
    }
    onSubmit(t2,isAct,unit||undefined,totalSize?+totalSize:undefined,uph?+uph:undefined,ppu?+ppu:undefined,s,isAct&&isChecklist); };
  return (
    <div className="modal-overlay" onClick={onCancel}><div className="modal" onClick={e=>e.stopPropagation()}>
      <h2>Add Node</h2>
      <label>Title</label>
      <input ref={ref} type="text" value={title} onChange={e=>setTitle(e.target.value)}
        onKeyDown={e=>{if(e.key==='Enter')submit();if(e.key==='Escape')onCancel();}} placeholder="e.g. Read CLR via C#" />
      <label className="checkbox-row" onClick={()=>setIsAct(!isAct)}>
        <input type="checkbox" checked={isAct} onChange={e=>setIsAct(e.target.checked)} /><span>Actionable item</span></label>
      {isAct&&<label className="checkbox-row" onClick={()=>setIsChecklist(!isChecklist)}>
        <input type="checkbox" checked={isChecklist} onChange={e=>setIsChecklist(e.target.checked)} />
        <span>Use subpoints for logging (tick subpoints in schedule to log 1 unit)</span></label>}
      {isAct&&<>
        <div className="form-row"><div><label>Unit</label><select value={unit} onChange={e=>setUnit(e.target.value)}>
          {UNITS.map(u=><option key={u} value={u}>{UNIT_LABELS[u] || u}</option>)}</select></div>
          <div><label>Total size</label><input type="number" value={totalSize} onChange={e=>setTotalSize(e.target.value)} placeholder="e.g. 350" /></div></div>
        <div className="form-row"><div><label>Units/hour</label><input type="number" value={uph} onChange={e=>setUph(e.target.value)} placeholder="e.g. 30" /></div>
          <div><label>Pts/unit</label><input type="number" value={ppu} onChange={e=>setPpu(e.target.value)} placeholder="e.g. 0.5" /></div></div>
        <label>Weekly schedule</label>
        <div className="weekday-picker">{DAYS.map((d,i)=>(
          <button key={i} type="button" className={`weekday-btn ${sDays.includes(DAY_VALUES[i])?'active':''}`} onClick={()=>toggleDay(DAY_VALUES[i])}>{d}</button>))}</div>
        {sDays.length>0&&<>
          <div className="form-row"><div><label>Default start</label><select value={sStart} onChange={e=>setSStart(+e.target.value)}>
            {TIMES.map(m=><option key={m} value={m}>{fmt(m)}</option>)}</select></div>
            <div><label>Default duration (min)</label><input type="number" value={sDur} min={5} step={5} onChange={e=>setSDur(+e.target.value)}
              style={{width:'100%'}} /></div></div>
          <label className="checkbox-row" style={{marginTop:8}} onClick={()=>setShowPerDay(!showPerDay)}>
            <input type="checkbox" checked={showPerDay} onChange={e=>setShowPerDay(e.target.checked)} />
            <span>Different time per day</span></label>
          {showPerDay && <PerDayEditor days={sDays} perDay={perDay} onChange={setPerDay} defaultStart={sStart} defaultDur={sDur} />}
        </>}
        {isChecklist && <p style={{ fontSize: 11, color: 'var(--text-muted)', margin: '4px 0 0' }}>
          You can add subpoints after creating, by editing this node.</p>}
      </>}
      <div className="modal-actions"><button className="btn" onClick={onCancel}>Cancel</button>
        <button className="btn btn-accent" onClick={submit} disabled={!title.trim()}>Create</button></div>
    </div></div>);
}

// --- Edit modal ---

interface EditProps {
  node: NodeDto;
  roadmapId?: string;
  onSave:(title:string, u:string|null,ts:number|null,uph:number|null,ppu:number|null,s:string|null, checklist:boolean)=>void;
  onCancel:()=>void;
}

export function EditNodeModal({ node, roadmapId, onSave, onCancel }: EditProps) {
  const [title,setTitle]=useState(node.title);
  const [isChecklist,setIsChecklist]=useState(!!node.isChecklist);
  const [unit,setUnit]=useState(node.unit??'');
  const [totalSize,setTotalSize]=useState(node.totalSize?.toString()??'');
  const [uph,setUph]=useState(node.unitsPerHour?.toString()??'');
  const [ppu,setPpu]=useState(node.pointsPerUnit?.toString()??'');
  const parsed:ScheduleTemplate|null=node.scheduleTemplate?JSON.parse(node.scheduleTemplate):null;
  const [sDays,setSDays]=useState<number[]>(parsed?.days??[]);
  const [sStart,setSStart]=useState(parsed?.startMinute??540);
  const [sDur,setSDur]=useState(parsed?.durationMinutes??60);
  const [perDay,setPerDay]=useState<Record<string,PerDayEntry>>(()=>{
    if(!parsed?.perDay) return {};
    const pd: Record<string,PerDayEntry> = {};
    for(const [k,v] of Object.entries(parsed.perDay)) pd[k] = {startMinute:v.startMinute,durationMinutes:v.durationMinutes};
    return pd;
  });
  const [showPerDay,setShowPerDay]=useState(()=>!!parsed?.perDay && Object.keys(parsed.perDay).length>0);
  const [subPoints,setSubPoints]=useState<NodeSubPointDto[]>([]);
  const [newSubTitle,setNewSubTitle]=useState('');
  const toggleDay=(v:number)=>setSDays(p=>p.includes(v)?p.filter(x=>x!==v):[...p,v].sort());

  const loadSubPoints=useCallback(async()=>{
    if(!roadmapId) return;
    const list=await api.getNodeSubPoints(roadmapId,node.id);
    setSubPoints(list);
  },[roadmapId,node.id]);
  useEffect(()=>{ if(isChecklist && roadmapId) loadSubPoints(); },[isChecklist,roadmapId,loadSubPoints]);

  const addSp=async()=>{
    if(!roadmapId || !newSubTitle.trim()) return;
    await api.addNodeSubPoint(roadmapId,node.id,newSubTitle.trim());
    setNewSubTitle(''); await loadSubPoints();
  };
  const renameSp=async(spid:string,t:string)=>{
    if(!roadmapId || !t.trim()) return;
    await api.updateNodeSubPoint(roadmapId,node.id,spid,t.trim()); await loadSubPoints();
  };
  const deleteSp=async(spid:string)=>{
    if(!roadmapId) return;
    await api.deleteNodeSubPoint(roadmapId,node.id,spid); await loadSubPoints();
  };

  const save=()=>{
    let s:string|null=null;
    if(sDays.length>0) {
      const obj: any = {days:sDays,startMinute:sStart,durationMinutes:sDur};
      if(showPerDay && Object.keys(perDay).length>0) obj.perDay = perDay;
      s=JSON.stringify(obj);
    }
    onSave(title.trim()||node.title, unit||null,totalSize?+totalSize:null,uph?+uph:null,ppu?+ppu:null,s, node.isActionable && isChecklist);
  };
  return (
    <div className="modal-overlay" onClick={onCancel}><div className="modal" onClick={e=>e.stopPropagation()}>
      <h2>Edit Item</h2>
      <label>Title</label>
      <input type="text" value={title} onChange={e=>setTitle(e.target.value)} />
      {node.isActionable && <label className="checkbox-row" onClick={()=>setIsChecklist(!isChecklist)}>
        <input type="checkbox" checked={isChecklist} onChange={e=>setIsChecklist(e.target.checked)} />
        <span>Use subpoints for logging (tick subpoints in schedule to log 1 unit)</span></label>}
      {node.isActionable && <>
        <div className="form-row"><div><label>Unit</label><select value={unit} onChange={e=>setUnit(e.target.value)}>
          {UNITS.map(u=><option key={u} value={u}>{UNIT_LABELS[u] || u}</option>)}</select></div>
          <div><label>Total size</label><input type="number" value={totalSize} onChange={e=>setTotalSize(e.target.value)} /></div></div>
        <div className="form-row"><div><label>Units/hour</label><input type="number" value={uph} onChange={e=>setUph(e.target.value)} placeholder="e.g. 30" /></div>
          <div><label>Pts/unit</label><input type="number" value={ppu} onChange={e=>setPpu(e.target.value)} placeholder="e.g. 0.5" /></div></div>
      </>}
      <label>Weekly schedule</label>
      <div className="weekday-picker">{DAYS.map((d,i)=>(
        <button key={i} type="button" className={`weekday-btn ${sDays.includes(DAY_VALUES[i])?'active':''}`} onClick={()=>toggleDay(DAY_VALUES[i])}>{d}</button>))}</div>
      {sDays.length>0&&<>
        <div className="form-row"><div><label>Default start</label><select value={sStart} onChange={e=>setSStart(+e.target.value)}>
          {TIMES.map(m=><option key={m} value={m}>{fmt(m)}</option>)}</select></div>
          <div><label>Default duration (min)</label><input type="number" value={sDur} min={5} step={5} onChange={e=>setSDur(+e.target.value)}
            style={{width:'100%'}} /></div></div>
        <label className="checkbox-row" style={{marginTop:8}} onClick={()=>setShowPerDay(!showPerDay)}>
          <input type="checkbox" checked={showPerDay} onChange={e=>setShowPerDay(e.target.checked)} />
          <span>Different time per day</span></label>
        {showPerDay && <PerDayEditor days={sDays} perDay={perDay} onChange={setPerDay} defaultStart={sStart} defaultDur={sDur} />}
      </>}
      {isChecklist && roadmapId && <>
        <label style={{ marginTop: 12 }}>Subpoints</label>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {subPoints.map(sp => (
            <SubPointRow key={sp.id} sp={sp} onRename={t => renameSp(sp.id, t)} onDelete={() => deleteSp(sp.id)} />
          ))}
          <div style={{ display: 'flex', gap: 6 }}>
            <input type="text" value={newSubTitle} onChange={e=>setNewSubTitle(e.target.value)}
              placeholder="Add subpoint…" style={{ flex: 1, fontSize: 13, padding: '4px 8px' }}
              onKeyDown={e => { if (e.key === 'Enter') addSp(); }} />
            <button className="btn btn-sm" onClick={addSp} disabled={!newSubTitle.trim()}>Add</button>
          </div>
        </div>
      </>}
      <div className="modal-actions"><button className="btn" onClick={onCancel}>Cancel</button>
        <button className="btn btn-accent" onClick={save}>Save</button></div>
    </div></div>);
}

function SubPointRow({ sp, onRename, onDelete }: { sp: NodeSubPointDto; onRename: (t: string) => void; onDelete: () => void; }) {
  const [editing, setEditing] = useState(false);
  const [val, setVal] = useState(sp.title);
  if (editing) {
    return (
      <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
        <input type="text" value={val} onChange={e=>setVal(e.target.value)} autoFocus
          style={{ flex: 1, fontSize: 13, padding: '4px 8px' }}
          onKeyDown={e => { if (e.key === 'Enter') { onRename(val); setEditing(false); } if (e.key === 'Escape') { setVal(sp.title); setEditing(false); } }} />
        <button className="btn btn-sm" onClick={() => { onRename(val); setEditing(false); }}>Save</button>
        <button className="btn btn-sm btn-ghost" onClick={() => { setVal(sp.title); setEditing(false); }}>✕</button>
      </div>
    );
  }
  return (
    <div style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 13 }}>
      <span style={{ flex: 1 }}>• {sp.title}</span>
      <button className="btn btn-sm btn-ghost" onClick={() => setEditing(true)}>✎</button>
      <button className="btn btn-sm btn-ghost" onClick={onDelete}>✕</button>
    </div>
  );
}

// --- Per-day schedule editor ---
const DAY_NAMES: Record<number,string> = {0:'Sun',1:'Mon',2:'Tue',3:'Wed',4:'Thu',5:'Fri',6:'Sat'};

export function PerDayEditor({ days, perDay, onChange, defaultStart, defaultDur }: {
  days: number[];
  perDay: Record<string, PerDayEntry>;
  onChange: (pd: Record<string, PerDayEntry>) => void;
  defaultStart: number; defaultDur: number;
}) {
  const setDayField = (day: number, field: 'startMinute' | 'durationMinutes', val: number) => {
    const key = day.toString();
    const existing = perDay[key] ?? { startMinute: defaultStart, durationMinutes: defaultDur };
    onChange({ ...perDay, [key]: { ...existing, [field]: val } });
  };
  const clearDay = (day: number) => {
    const next = { ...perDay };
    delete next[day.toString()];
    onChange(next);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 8, padding: '10px 0' }}>
      {days.map(day => {
        const key = day.toString();
        const hasOverride = key in perDay;
        const entry = perDay[key] ?? { startMinute: defaultStart, durationMinutes: defaultDur };
        return (
          <div key={day} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13 }}>
            <span style={{ width: 36, fontWeight: 600, color: hasOverride ? 'var(--accent)' : 'var(--text-muted)' }}>{DAY_NAMES[day]}</span>
            <select value={entry.startMinute} onChange={e => setDayField(day, 'startMinute', +e.target.value)}
              style={{ fontSize: 12, padding: '3px 6px' }}>
              {TIMES.map(m => <option key={m} value={m}>{fmt(m)}</option>)}
            </select>
            <input type="number" value={entry.durationMinutes} min={5} step={5}
              onChange={e => setDayField(day, 'durationMinutes', +e.target.value)}
              style={{ fontSize: 12, padding: '3px 6px', width: 60 }} />
            {hasOverride && (
              <button className="btn btn-ghost btn-sm" onClick={() => clearDay(day)}
                style={{ fontSize: 11, padding: '2px 6px' }} title="Reset to default">✕</button>
            )}
          </div>
        );
      })}
      <p style={{ fontSize: 11, color: 'var(--text-muted)', margin: '4px 0 0' }}>
        Only days with changes are stored. Others use the default.
      </p>
    </div>
  );
}
