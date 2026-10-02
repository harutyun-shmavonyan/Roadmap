import type { RoadmapSummary, RoadmapTree, NodeDto, CreateNodeRequest, ActionableItem,
  ActionItemStatus, SprintDto, WorkLogDto, ScheduleResponse, PerformanceSummary, StatusChangeDto,
  WeekPlan, WeekPlanGoal, WorkLogHistory, HabitDto, SprintHabitDto, ScheduleHabitDto,
  SingleTaskDto, ScheduleTaskDto, CustomLogDto, ScheduleBlockDef, ScheduleBlockMode, SprintGoalDto,
  NodeSubPointDto, ScheduleSubPointDto, NoteDto,
  FlashcardDto, FlashcardPromptDto, FlashcardStatsDto, FlashcardMemoryDto, FlashcardSessionDto, FlashcardGrade, FlashcardReviewResultDto, FlashcardPromptState,
  ArticleSummaryDto, ArticleDto, ArticleImageDto, ArticleFormat,
  NewsletterSummaryDto, NewsletterCursorDto,
  JobRunDto, JobRunSummaryDto,
  VocabEntryDto, VocabStatsDto,
  MealDto, MealSlot, SaveMealRequest,
  CourseSummaryDto, CourseDetailDto, LessonDetailDto, CourseEventDto, CourseResumeDto,
  CourseStatus, LessonStatus,
  ExperienceDto, ExperienceImageDto, ExperienceStatus, SaveExperienceRequest,
  SignalRunDto, SignalRunSummaryDto, SignalDto } from './types';

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
  createSprint: (r: string, name: string, s: string, e: string, scoringMode?: string) => req<SprintDto>(`${B}/${r}/sprints`, { method: 'POST', body: JSON.stringify({ name, startDate: s, endDate: e, scoringMode }) }),
  startSprint: (r: string, sid: string) => req<SprintDto>(`${B}/${r}/sprints/${sid}/start`, { method: 'POST' }),
  deleteSprint: (r: string, sid: string) => req<void>(`${B}/${r}/sprints/${sid}`, { method: 'DELETE' }),
  closeSprint: (r: string, sid: string) => req<SprintDto>(`${B}/${r}/sprints/${sid}/close`, { method: 'PATCH' }),
  updateSprint: (r: string, sid: string, name: string, s: string, e: string) => req<SprintDto>(`${B}/${r}/sprints/${sid}`, { method: 'PATCH', body: JSON.stringify({ name, startDate: s, endDate: e }) }),
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
  createBlock: (r: string, name: string, scheduleTemplate?: string, mode?: ScheduleBlockMode) =>
    req<ScheduleBlockDef>(`${B}/${r}/blocks`, { method: 'POST', body: JSON.stringify({ name, scheduleTemplate, mode }) }),
  updateBlock: (r: string, bid: string, name: string, scheduleTemplate?: string, mode?: ScheduleBlockMode) =>
    req<void>(`${B}/${r}/blocks/${bid}`, { method: 'PUT', body: JSON.stringify({ name, scheduleTemplate, mode }) }),
  // The whole active set at once — anything left out goes inactive.
  setBlockItemsActive: (r: string, bid: string, activeNodeIds: string[]) =>
    req<void>(`${B}/${r}/blocks/${bid}/items/active`, { method: 'PUT', body: JSON.stringify({ activeNodeIds }) }),
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
  // Notes v2 — flashcards (independent of the daily notes). Cards and prompts are mostly written by the
  // skills in chat; the tab reads them, fixes prompts, and runs the in-app flashcard review.
  getFlashcards: (f: { book?: 'red' | 'green'; date?: string; search?: string; withoutPrompts?: boolean } = {}) => {
    const q = new URLSearchParams({ limit: '5000' });
    if (f.book) q.set('book', f.book);
    if (f.date) q.set('date', f.date);
    if (f.search) q.set('search', f.search);
    if (f.withoutPrompts) q.set('withoutPrompts', 'true');
    return req<FlashcardDto[]>(`/api/flashcards/?${q}`);
  },
  getFlashcard: (id: string) => req<FlashcardDto>(`/api/flashcards/${id}`),
  getFlashcardStats: () => req<FlashcardStatsDto>('/api/flashcards/stats'),
  getFlashcardMemory: () => req<FlashcardMemoryDto>('/api/flashcards/memory'),
  createFlashcard: (book: 'red' | 'green', content: string, entryDate?: string, prompts?: { question: string; answer: string }[]) =>
    req<FlashcardDto>('/api/flashcards/', { method: 'POST', body: JSON.stringify({ book, content, entryDate, prompts }) }),
  updateFlashcard: (id: string, content: string) =>
    req<FlashcardDto>(`/api/flashcards/${id}`, { method: 'PUT', body: JSON.stringify({ content }) }),
  deleteFlashcard: (id: string) => req<void>(`/api/flashcards/${id}`, { method: 'DELETE' }),
  addFlashcardPrompts: (id: string, prompts: { question: string; answer: string }[]) =>
    req<FlashcardPromptDto[]>(`/api/flashcards/${id}/prompts`, { method: 'POST', body: JSON.stringify({ prompts }) }),
  // PATCH semantics: omit a field to keep it. reset restarts the schedule (for a rewritten leech).
  updateFlashcardPrompt: (id: string, patch: { question?: string; answer?: string; state?: FlashcardPromptState; reset?: boolean }) =>
    req<FlashcardPromptDto>(`/api/flashcards/prompts/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
  deleteFlashcardPrompt: (id: string) => req<void>(`/api/flashcards/prompts/${id}`, { method: 'DELETE' }),
  // The flashcard review: start (or resume) today's capped queue, then record each self-graded answer.
  startFlashcardReview: (book?: 'red' | 'green') =>
    req<FlashcardSessionDto>(`/api/flashcards/session${book ? `?book=${book}` : ''}`, { method: 'POST' }),
  recordFlashcardReview: (id: string, grade: FlashcardGrade, answer?: string, note?: string) =>
    req<FlashcardReviewResultDto>(`/api/flashcards/prompts/${id}/review`, { method: 'POST', body: JSON.stringify({ grade, answer, note }) }),

  // Articles (global reading library; Markdown or HTML body; marking read earns 3 pts/hour)
  getArticles: () => req<ArticleSummaryDto[]>('/api/articles'),
  getArticle: (id: string) => req<ArticleDto>(`/api/articles/${id}`),
  createArticle: (title: string, content: string, format: ArticleFormat, chatUrl: string, readMinutes?: number) =>
    req<ArticleDto>('/api/articles', { method: 'POST', body: JSON.stringify({ title, content, format, chatUrl, readMinutes }) }),
  updateArticle: (id: string, title: string, content: string, format: ArticleFormat, chatUrl: string, readMinutes?: number) =>
    req<ArticleDto>(`/api/articles/${id}`, { method: 'PUT', body: JSON.stringify({ title, content, format, chatUrl, readMinutes }) }),
  deleteArticle: (id: string) => req<void>(`/api/articles/${id}`, { method: 'DELETE' }),
  // Records the read date only — reading earns no points.
  markArticleRead: (id: string) =>
    req<ArticleDto>(`/api/articles/${id}/read`, { method: 'POST', body: JSON.stringify({}) }),
  markArticleUnread: (id: string) =>
    req<ArticleDto>(`/api/articles/${id}/unread`, { method: 'POST' }),

  // Self-contained HTML document (images inlined as data URIs) for an HTML-format article.
  // Fetched with the bearer token so it stays behind auth; used both for the reader iframe
  // (srcdoc) and the "open in new tab" blob.
  getArticleHtml: async (id: string): Promise<string> => {
    const token = getToken();
    const r = await fetch(`/api/articles/${id}/html`, { headers: token ? { Authorization: `Bearer ${token}` } : {} });
    if (!r.ok) throw new Error(`Article HTML fetch failed: ${r.status}`);
    return r.text();
  },
  // Upload one or more image files to an article (multipart). Returns the stored image list.
  uploadArticleImages: async (id: string, files: File[]): Promise<ArticleImageDto[]> => {
    const token = getToken();
    const fd = new FormData();
    for (const f of files) fd.append('file', f, f.name);
    const r = await fetch(`/api/articles/${id}/images`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: fd,
    });
    if (!r.ok) throw new Error(`Image upload failed: ${r.status} ${await r.text()}`);
    // Re-read the article to get the canonical, ordered image list.
    return (await req<ArticleDto>(`/api/articles/${id}`)).images;
  },
  deleteArticleImage: (id: string, name: string) =>
    req<void>(`/api/articles/${id}/images/${encodeURIComponent(name)}`, { method: 'DELETE' }),

  // Reading position: the fraction (0..1) of the article scrolled through plus the anchor naming
  // the exact spot ("index:offset" into the article's block elements, null when there is none).
  // Fire-and-forget: failures are ignored (losing a bookmark is not worth an error toast) and
  // `keepalive` lets the last save survive the tab being closed mid-article.
  saveArticleProgress: (id: string, progress: number, anchor: string | null): void => {
    const token = getToken();
    void fetch(`/api/articles/${id}/progress`, {
      method: 'PUT',
      keepalive: true,
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: JSON.stringify({ progress, anchor }),
    }).catch(() => {});
  },

  // Professional Newsletter (agent-published editions; the tab reads them and ticks them read)
  getNewsletters: () => req<NewsletterSummaryDto[]>('/api/newsletters'),
  getNewsletterCursor: () => req<NewsletterCursorDto>('/api/newsletters/cursor'),
  markNewsletterRead: (id: string) => req<NewsletterSummaryDto>(`/api/newsletters/${id}/read`, { method: 'POST' }),
  markNewsletterUnread: (id: string) => req<NewsletterSummaryDto>(`/api/newsletters/${id}/unread`, { method: 'POST' }),

  // The edition as a document, fetched with the bearer token so it can be framed or opened in a tab.
  getNewsletterHtml: async (id: string): Promise<string> => {
    const token = getToken();
    const r = await fetch(`/api/newsletters/${id}/html`, { headers: token ? { Authorization: `Bearer ${token}` } : {} });
    if (!r.ok) throw new Error(`Newsletter fetch failed: ${r.status}`);
    return r.text();
  },

  // Stock Signals (the screener publishes over MCP; the tab reads, ticks days read, records decisions)
  getSignalRuns: (days = 90) => req<SignalRunSummaryDto[]>(`/api/signals?days=${days}`),
  getLatestSignalRun: () => req<SignalRunDto>('/api/signals/latest'),
  getSignalRun: (date: string) => req<SignalRunDto>(`/api/signals/${date}`),
  markSignalRunRead: (id: string) => req<SignalRunSummaryDto>(`/api/signals/${id}/read`, { method: 'POST' }),
  markSignalRunUnread: (id: string) => req<SignalRunSummaryDto>(`/api/signals/${id}/unread`, { method: 'POST' }),
  updateSignal: (id: string, body: { status?: string; notes?: string }) =>
    req<SignalDto>(`/api/signals/items/${id}`, { method: 'PATCH', body: JSON.stringify(body) }),

  // English vocabulary (global — entries are added and reviewed over MCP; the tab reads and prunes)
  getVocab: () => req<VocabEntryDto[]>('/api/vocab'),
  getVocabStats: () => req<VocabStatsDto>('/api/vocab/stats'),
  deleteVocabEntry: (id: string) => req<void>(`/api/vocab/${id}`, { method: 'DELETE' }),

  // Nutrition (global meal book — read and written straight from the tab)
  getMeals: (slot?: MealSlot) => req<MealDto[]>(`/api/meals${slot ? `?slot=${slot}` : ''}`),
  createMeal: (body: SaveMealRequest) => req<MealDto>('/api/meals', { method: 'POST', body: JSON.stringify(body) }),
  updateMeal: (id: string, body: SaveMealRequest) => req<MealDto>(`/api/meals/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  toggleMealFavorite: (id: string) => req<MealDto>(`/api/meals/${id}/favorite`, { method: 'PATCH' }),
  deleteMeal: (id: string) => req<void>(`/api/meals/${id}`, { method: 'DELETE' }),

  // The meal photo (optional, one per meal). The tab only ever reads it — photos are set and
  // removed over MCP. The bytes sit behind the same bearer token as everything else, so an
  // <img src> cannot reach them directly: fetch them and render from an object URL.
  fetchMealImageUrl: async (id: string): Promise<string> => {
    const token = getToken();
    const r = await fetch(`/api/meals/${id}/image`, { headers: token ? { Authorization: `Bearer ${token}` } : {} });
    if (!r.ok) throw new Error(`Photo fetch failed: ${r.status}`);
    return URL.createObjectURL(await r.blob());
  },

  // Job scouting (global — one run per day, imported by the Finder pipeline over MCP)
  getJobRuns: () => req<JobRunSummaryDto[]>('/api/job-runs'),
  getLatestJobRun: () => req<JobRunDto>('/api/job-runs/latest'),
  getJobRun: (date: string) => req<JobRunDto>(`/api/job-runs/${date}`),

  // Record what happened to an application. PATCH semantics: only the fields
  // passed are written, so updating the status leaves the notes alone. Pass an
  // empty string to clear a field.
  updatePostingApplication: (id: string, patch: {
    applicationStatus?: string; appliedAt?: string; respondedAt?: string; applicationNotes?: string;
  }) => req<void>(`/api/job-runs/postings/${id}/application`, {
    method: 'PATCH', body: JSON.stringify(patch),
  }),

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

  // Courses (global — written by the authoring agent over MCP, read here). A course and a lesson
  // are addressed by slug and code rather than by id: they are what the URL shows, and they are
  // what the agent and the person both call things.
  getCourses: (status?: CourseStatus) =>
    req<CourseSummaryDto[]>(`/api/courses${status ? `?status=${status}` : ''}`),
  getCourse: (slug: string, depth: 'stages' | 'lessons' | 'full' = 'lessons') =>
    req<CourseDetailDto>(`/api/courses/${encodeURIComponent(slug)}?depth=${depth}`),
  getCourseLesson: (slug: string, code: string) =>
    req<LessonDetailDto>(`/api/courses/${encodeURIComponent(slug)}/lessons/${encodeURIComponent(code)}`),
  getCourseTimeline: (slug: string, limit = 50, cursor?: number) =>
    req<CourseEventDto[]>(`/api/courses/${encodeURIComponent(slug)}/timeline?limit=${limit}${cursor ? `&cursor=${cursor}` : ''}`),
  getCourseResume: (slug: string) =>
    req<CourseResumeDto>(`/api/courses/${encodeURIComponent(slug)}/resume`),

  setCourseStatus: (slug: string, status: CourseStatus) =>
    req<CourseDetailDto>(`/api/courses/${encodeURIComponent(slug)}/status`, {
      method: 'PATCH', body: JSON.stringify({ status }),
    }),
  // force completes a lesson whose required exercises are not all graded; the override is
  // recorded in the timeline, which is why the tab asks before sending it.
  setLessonStatus: (slug: string, code: string, status: LessonStatus, force?: boolean) =>
    req<LessonDetailDto>(`/api/courses/${encodeURIComponent(slug)}/lessons/${encodeURIComponent(code)}/status`, {
      method: 'PATCH', body: JSON.stringify({ status, force }),
    }),
  logCourseEvent: (slug: string, type: 'note', payload: unknown, lessonCode?: string) =>
    req<{ id: number; type: string; createdAt: string }>(`/api/courses/${encodeURIComponent(slug)}/events`, {
      method: 'POST', body: JSON.stringify({ type, payload, lessonCode }),
    }),

  // Experiences (global — written from the tab as readily as by an agent, so full CRUD here).
  getExperiences: (status?: ExperienceStatus) =>
    req<ExperienceDto[]>(`/api/experiences${status ? `?status=${status}` : ''}`),
  getExperienceCategories: () => req<{ category: string; count: number }[]>('/api/experiences/categories'),
  createExperience: (body: SaveExperienceRequest) =>
    req<ExperienceDto>('/api/experiences', { method: 'POST', body: JSON.stringify(body) }),
  // PUT replaces the whole experience — the form always sends every field.
  updateExperience: (id: string, body: SaveExperienceRequest) =>
    req<ExperienceDto>(`/api/experiences/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  setExperienceStatus: (id: string, status: ExperienceStatus) =>
    req<ExperienceDto>(`/api/experiences/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
  deleteExperience: (id: string) => req<void>(`/api/experiences/${id}`, { method: 'DELETE' }),

  // Pictures sit behind the bearer token, so they are fetched as blobs and shown from object URLs,
  // and uploaded as multipart — several at once, the way a phone's photo picker hands them over.
  uploadExperienceImages: async (id: string, files: File[]): Promise<ExperienceDto> => {
    const token = getToken();
    const fd = new FormData();
    for (const f of files) fd.append('file', f, f.name);
    const r = await fetch(`/api/experiences/${id}/images`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: fd,
    });
    if (!r.ok) throw new Error(`Upload failed: ${r.status} ${await r.text()}`);
    return r.json();
  },
  fetchExperienceImageUrl: async (imageId: string): Promise<string> => {
    const token = getToken();
    const r = await fetch(`/api/experiences/images/${imageId}`, { headers: token ? { Authorization: `Bearer ${token}` } : {} });
    if (!r.ok) throw new Error(`Picture fetch failed: ${r.status}`);
    return URL.createObjectURL(await r.blob());
  },
  updateExperienceImage: (imageId: string, patch: { caption?: string; sortOrder?: number }) =>
    req<ExperienceImageDto>(`/api/experiences/images/${imageId}`, { method: 'PATCH', body: JSON.stringify(patch) }),
  deleteExperienceImage: (imageId: string) =>
    req<void>(`/api/experiences/images/${imageId}`, { method: 'DELETE' }),
};
