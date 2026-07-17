import type { RoadmapSummary, RoadmapTree, NodeDto, CreateNodeRequest, ActionableItem,
  ActionItemStatus, SprintDto, WorkLogDto, ScheduleResponse, PerformanceSummary, StatusChangeDto,
  WeekPlan, WeekPlanGoal, WorkLogHistory, HabitDto, SprintHabitDto, ScheduleHabitDto,
  SingleTaskDto, ScheduleTaskDto, CustomLogDto, ScheduleBlockDef, SprintGoalDto,
  NodeSubPointDto, ScheduleSubPointDto, NoteDto,
  JobRunDto, JobRunSummaryDto,
  VocabEntryDto, VocabStatsDto } from './types';

const B = '/api/roadmaps';

function getToken(): string | null {
  return localStorage.getItem('roadmap_token');
}

export function setToken(token: string) {
  localStorage.setItem('roadmap_token', token);
}

export function clearToken() {
  localStorage.removeItem('roadmap_token');
}

export function isLoggedIn(): boolean {
  return !!getToken();
}

async function req<T>(url: string, o?: RequestInit): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const r = await fetch(url, { headers, ...o });
  if (r.status === 401) {
    clearToken();
    window.location.reload();
    throw new Error('Unauthorized');
  }
  if (!r.ok) throw new Error(`API ${r.status}: ${await r.text()}`);
  if (r.status === 204) return undefined as T; return r.json();
}

export async function login(password: string): Promise<boolean> {
  try {
    const r = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ password }),
    });
    if (!r.ok) return false;
    const data = await r.json();
    setToken(data.token);
    return true;
  } catch { return false; }
}

export async function checkAuth(): Promise<boolean> {
  const token = getToken();
  if (!token) return false;
  try {
    const r = await fetch('/api/auth/check', { headers: { 'Authorization': `Bearer ${token}` } });
    return r.ok;
  } catch { return false; }
}

export const api = {
  listRoadmaps: () => req<RoadmapSummary[]>(B),
  getTree: (r: string) => req<RoadmapTree>(`${B}/${r}/tree`),
  createRoadmap: (name: string, desc?: string) => req<RoadmapSummary>(B, { method: 'POST', body: JSON.stringify({ name, description: desc }) }),

  createNode: (r: string, body: CreateNodeRequest) => req<NodeDto>(`${B}/${r}/nodes`, { method: 'POST', body: JSON.stringify(body) }),
  updateNode: (r: string, n: string, body: Record<string, unknown>) => req<void>(`${B}/${r}/nodes/${n}`, { method: 'PUT', body: JSON.stringify(body) }),
  updateNodeStatus: (r: string, n: string, status: ActionItemStatus) => req<void>(`${B}/${r}/nodes/${n}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
  deleteNode: (r: string, n: string) => req<void>(`${B}/${r}/nodes/${n}`, { method: 'DELETE' }),
  reorderNode: (r: string, nid: string, direction: 'up' | 'down') =>
    req<void>(`${B}/${r}/nodes/${nid}/reorder`, { method: 'PATCH', body: JSON.stringify({ direction }) }),
  moveNode: (r: string, nid: string, newParentId: string | null, sortOrder: number) =>
    req<void>(`${B}/${r}/nodes/${nid}/move`, { method: 'PATCH', body: JSON.stringify({ newParentId, sortOrder }) }),
  addCategoryLink: (r: string, nid: string, categoryId: string) =>
    req<{linkId: string; categoryId: string; categoryTitle: string}>(`${B}/${r}/nodes/${nid}/categories`, { method: 'POST', body: JSON.stringify({ categoryId }) }),
  removeCategoryLink: (r: string, nid: string, linkId: string) =>
    req<void>(`${B}/${r}/nodes/${nid}/categories/${linkId}`, { method: 'DELETE' }),

  getActionables: (r: string, status?: ActionItemStatus) => req<ActionableItem[]>(`${B}/${r}/actionables${status ? `?status=${status}` : ''}`),
  getSchedule: (r: string, date: string) => req<ScheduleResponse>(`${B}/${r}/schedule/${date}`),

  getSprints: (r: string) => req<SprintDto[]>(`${B}/${r}/sprints`),
  createSprint: (r: string, name: string, s: string, e: string) => req<SprintDto>(`${B}/${r}/sprints`, { method: 'POST', body: JSON.stringify({ name, startDate: s, endDate: e }) }),
  startSprint: (r: string, sid: string) => req<SprintDto>(`${B}/${r}/sprints/${sid}/start`, { method: 'POST' }),
  deleteSprint: (r: string, sid: string) => req<void>(`${B}/${r}/sprints/${sid}`, { method: 'DELETE' }),
  closeSprint: (r: string, sid: string) => req<SprintDto>(`${B}/${r}/sprints/${sid}/close`, { method: 'PATCH' }),
  toggleRelaxDay: (r: string, sid: string, date: string) => req<SprintDto>(`${B}/${r}/sprints/${sid}/relax/${date}`, { method: 'PATCH' }),
  getPerformance: (r: string, sid: string) => req<PerformanceSummary>(`${B}/${r}/sprints/${sid}/performance`),

  getWorkLogs: (r: string, date: string) => req<WorkLogDto[]>(`${B}/${r}/worklogs/${date}`),
  logWork: (r: string, nodeId: string, date: string, amount: number, note?: string) =>
    req<void>(`${B}/${r}/worklogs`, { method: 'POST', body: JSON.stringify({ nodeId, date, amount, note }) }),
  updateWorkLog: (r: string, logId: string, amount: number, note?: string | null) =>
    req<void>(`${B}/${r}/worklogs/${logId}`, { method: 'PUT', body: JSON.stringify({ amount, note }) }),
  deleteWorkLog: (r: string, lid: string) => req<void>(`${B}/${r}/worklogs/${lid}`, { method: 'DELETE' }),

  getNodeHistory: (r: string, nid: string) => req<StatusChangeDto[]>(`${B}/${r}/nodes/${nid}/history`),
  getNodeLogs: (r: string, nid: string) => req<WorkLogHistory>(`${B}/${r}/nodes/${nid}/logs`),
  getRoadmapHistory: (r: string, limit?: number) => req<StatusChangeDto[]>(`${B}/${r}/history${limit ? `?limit=${limit}` : ''}`),

  getWeekPlan: (r: string, date: string) => req<WeekPlan | { noSprint: true }>(`${B}/${r}/weekplan/${date}`),
  addWeekGoal: (r: string, date: string, title: string, targetDesc?: string, targetAmt?: number, sprintGoalId?: string) =>
    req<WeekPlanGoal>(`${B}/${r}/weekplan/${date}/goals`, { method: 'POST', body: JSON.stringify({ title, targetDescription: targetDesc, targetAmount: targetAmt, sprintGoalId }) }),
  updateWeekGoal: (r: string, date: string, gid: string, body: Record<string, unknown>) =>
    req<void>(`${B}/${r}/weekplan/${date}/goals/${gid}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteWeekGoal: (r: string, date: string, gid: string) =>
    req<void>(`${B}/${r}/weekplan/${date}/goals/${gid}`, { method: 'DELETE' }),
  toggleWeekClose: (r: string, date: string, notes?: string) =>
    req<void>(`${B}/${r}/weekplan/${date}/close`, { method: 'PATCH', body: JSON.stringify({ notes }) }),

  // Habits
  getHabits: (r: string) => req<HabitDto[]>(`${B}/${r}/habits`),
  createHabit: (r: string, name: string) => req<HabitDto>(`${B}/${r}/habits`, { method: 'POST', body: JSON.stringify({ name }) }),
  deleteHabit: (r: string, hid: string) => req<void>(`${B}/${r}/habits/${hid}`, { method: 'DELETE' }),
  getSprintHabits: (r: string, sid: string) => req<SprintHabitDto[]>(`${B}/${r}/sprints/${sid}/habits`),
  addSprintHabit: (r: string, sid: string, habitId: string) => req<string>(`${B}/${r}/sprints/${sid}/habits`, { method: 'POST', body: JSON.stringify({ habitId }) }),
  removeSprintHabit: (r: string, sid: string, shid: string) => req<void>(`${B}/${r}/sprints/${sid}/habits/${shid}`, { method: 'DELETE' }),
  pauseSprintHabit: (r: string, sid: string, shid: string) => req<void>(`${B}/${r}/sprints/${sid}/habits/${shid}/pause`, { method: 'PATCH' }),
  resumeSprintHabit: (r: string, sid: string, shid: string) => req<void>(`${B}/${r}/sprints/${sid}/habits/${shid}/resume`, { method: 'PATCH' }),
  toggleHabitCheck: (r: string, sid: string, shid: string, date: string, isChecked: boolean) =>
    req<void>(`${B}/${r}/sprints/${sid}/habits/${shid}/check`, { method: 'PUT', body: JSON.stringify({ date, isChecked }) }),
  getScheduleHabits: (r: string, date: string) => req<ScheduleHabitDto[]>(`${B}/${r}/schedule/${date}/habits`),

  // Node subpoints (checklist templates)
  getNodeSubPoints: (r: string, nid: string) => req<NodeSubPointDto[]>(`${B}/${r}/nodes/${nid}/subpoints`),
  addNodeSubPoint: (r: string, nid: string, title: string) => req<NodeSubPointDto>(`${B}/${r}/nodes/${nid}/subpoints`, { method: 'POST', body: JSON.stringify({ title }) }),
  updateNodeSubPoint: (r: string, nid: string, spid: string, title: string) => req<void>(`${B}/${r}/nodes/${nid}/subpoints/${spid}`, { method: 'PATCH', body: JSON.stringify({ title }) }),
  deleteNodeSubPoint: (r: string, nid: string, spid: string) => req<void>(`${B}/${r}/nodes/${nid}/subpoints/${spid}`, { method: 'DELETE' }),
  getScheduleSubPoints: (r: string, date: string, nid: string) => req<ScheduleSubPointDto[]>(`${B}/${r}/schedule/${date}/subpoints/${nid}`),
  toggleScheduleSubPoint: (r: string, date: string, nid: string, spid: string, isChecked: boolean) => req<void>(`${B}/${r}/schedule/${date}/subpoints/${nid}/${spid}`, { method: 'PATCH', body: JSON.stringify({ isChecked }) }),

  // Tasks
  getTasks: (r: string) => req<SingleTaskDto[]>(`${B}/${r}/tasks`),
  createTask: (r: string, body: Record<string, unknown>) => req<SingleTaskDto>(`${B}/${r}/tasks`, { method: 'POST', body: JSON.stringify(body) }),
  updateTask: (r: string, tid: string, body: Record<string, unknown>) => req<void>(`${B}/${r}/tasks/${tid}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteTask: (r: string, tid: string) => req<void>(`${B}/${r}/tasks/${tid}`, { method: 'DELETE' }),
  completeTask: (r: string, tid: string, date: string) => req<void>(`${B}/${r}/tasks/${tid}/complete`, { method: 'PATCH', body: JSON.stringify({ date }) }),
  uncompleteTask: (r: string, tid: string) => req<void>(`${B}/${r}/tasks/${tid}/uncomplete`, { method: 'PATCH' }),
  delayTask: (r: string, tid: string) => req<void>(`${B}/${r}/tasks/${tid}/delay`, { method: 'PATCH' }),
  getScheduleTasks: (r: string, date: string) => req<ScheduleTaskDto[]>(`${B}/${r}/schedule/${date}/tasks`),

  // Custom Logs
  getCustomLogs: (r: string) => req<CustomLogDto[]>(`${B}/${r}/customlogs`),
  createCustomLog: (r: string, title: string, points: number, date: string, note?: string) =>
    req<CustomLogDto>(`${B}/${r}/customlogs`, { method: 'POST', body: JSON.stringify({ title, points, date, note }) }),
  deleteCustomLog: (r: string, id: string) => req<void>(`${B}/${r}/customlogs/${id}`, { method: 'DELETE' }),
  getScheduleCustomLogs: (r: string, date: string) => req<CustomLogDto[]>(`${B}/${r}/schedule/${date}/customlogs`),

  // Schedule Blocks
  getBlocks: (r: string) => req<ScheduleBlockDef[]>(`${B}/${r}/blocks`),
  createBlock: (r: string, name: string, scheduleTemplate?: string) =>
    req<ScheduleBlockDef>(`${B}/${r}/blocks`, { method: 'POST', body: JSON.stringify({ name, scheduleTemplate }) }),
  updateBlock: (r: string, bid: string, name: string, scheduleTemplate?: string) =>
    req<void>(`${B}/${r}/blocks/${bid}`, { method: 'PUT', body: JSON.stringify({ name, scheduleTemplate }) }),
  deleteBlock: (r: string, bid: string) => req<void>(`${B}/${r}/blocks/${bid}`, { method: 'DELETE' }),
  assignToBlock: (r: string, bid: string, nodeId: string) =>
    req<void>(`${B}/${r}/blocks/${bid}/items`, { method: 'POST', body: JSON.stringify({ nodeId, blockSortOrder: 0 }) }),
  removeFromBlock: (r: string, bid: string, nodeId: string) =>
    req<void>(`${B}/${r}/blocks/${bid}/items/${nodeId}`, { method: 'DELETE' }),
  reorderBlockItem: (r: string, bid: string, nodeId: string, direction: 'up' | 'down') =>
    req<void>(`${B}/${r}/blocks/${bid}/items/${nodeId}/reorder`, { method: 'PATCH', body: JSON.stringify({ direction }) }),
  batchReorderBlockItems: (r: string, bid: string, nodeIds: string[]) =>
    req<void>(`${B}/${r}/blocks/${bid}/items/reorder`, { method: 'PUT', body: JSON.stringify({ nodeIds }) }),

  // Sprint Goals
  getSprintGoals: (r: string, sid: string) => req<SprintGoalDto[]>(`${B}/${r}/sprints/${sid}/goals`),
  createSprintGoal: (r: string, sid: string, title: string, targetAmount: number, unit?: string, description?: string) =>
    req<SprintGoalDto>(`${B}/${r}/sprints/${sid}/goals`, { method: 'POST', body: JSON.stringify({ title, unit, targetAmount, description }) }),
  updateSprintGoal: (r: string, sid: string, gid: string, title: string, targetAmount: number, unit?: string, description?: string) =>
    req<void>(`${B}/${r}/sprints/${sid}/goals/${gid}`, { method: 'PUT', body: JSON.stringify({ title, unit, targetAmount, description }) }),
  deleteSprintGoal: (r: string, sid: string, gid: string) =>
    req<void>(`${B}/${r}/sprints/${sid}/goals/${gid}`, { method: 'DELETE' }),
  logSprintGoal: (r: string, sid: string, gid: string, date: string, amount: number) =>
    req<string>(`${B}/${r}/sprints/${sid}/goals/${gid}/log`, { method: 'POST', body: JSON.stringify({ date, amount }) }),
  deleteSprintGoalLog: (r: string, sid: string, gid: string, logId: string) =>
    req<void>(`${B}/${r}/sprints/${sid}/goals/${gid}/log/${logId}`, { method: 'DELETE' }),

  // Daily Notes (global 'red' / 'green' books)
  getNotes: (book: 'red' | 'green') => req<NoteDto[]>(`/api/notes/${book}`),

  // English vocabulary (global — entries are added and reviewed over MCP; the tab reads and prunes)
  getVocab: () => req<VocabEntryDto[]>('/api/vocab'),
  getVocabStats: () => req<VocabStatsDto>('/api/vocab/stats'),
  deleteVocabEntry: (id: string) => req<void>(`/api/vocab/${id}`, { method: 'DELETE' }),

  // Job scouting (global — one run per day, imported by the Finder pipeline over MCP)
  getJobRuns: () => req<JobRunSummaryDto[]>('/api/job-runs'),
  getLatestJobRun: () => req<JobRunDto>('/api/job-runs/latest'),
  getJobRun: (date: string) => req<JobRunDto>(`/api/job-runs/${date}`),

  // Tailored CV is binary (bytea), so fetch with the bearer token and trigger a
  // blob download rather than a plain <a href> (which wouldn't carry auth).
  downloadPostingCv: async (id: string, filename: string) => {
    const token = getToken();
    const r = await fetch(`/api/job-runs/postings/${id}/cv`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });
    if (!r.ok) throw new Error(`CV download failed: ${r.status}`);
    const blob = await r.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = filename;
    document.body.appendChild(a); a.click(); a.remove();
    URL.revokeObjectURL(url);
  },
};
