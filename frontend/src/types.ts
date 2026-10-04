export type ActionItemStatus = 'NotStarted' | 'Active' | 'Paused' | 'Stopped' | 'Completed';

export interface StatusChangeDto { id: string; nodeId: string; nodeTitle: string; oldStatus: string; newStatus: string; trigger: string; changedAt: string; }

export interface RoadmapSummary { id: string; name: string; description: string | null; createdAt: string; }
export interface RoadmapTree { id: string; name: string; description: string | null; roots: NodeDto[]; }

export interface NodeDto {
  id: string; parentId: string | null; title: string; isActionable: boolean;
  status: ActionItemStatus; unit: string | null; totalSize: number | null;
  unitsPerHour: number | null; pointsPerUnit: number | null;
  scheduleTemplate: string | null; sortOrder: number;
  scheduleBlockId: string | null; blockSortOrder: number;
  categoryLinks: CategoryLinkDto[]; children: NodeDto[];
  isChecklist: boolean;
}
export interface CategoryLinkDto { linkId: string; categoryId: string; categoryTitle: string; }
export interface ScheduleTemplate { days: number[]; startMinute: number; durationMinutes: number; perDay?: Record<string, { startMinute: number; durationMinutes: number }>; }

/**
 * One thing on the day's calendar. Normally an item (`nodeId` set). For a pool block it is the
 * block itself: `nodeId` is null, `blockId` and `poolItems` are set, the figures are the pool's
 * (average rate, summed progress), and the exact item is picked from `poolItems` when logging.
 */
export interface ScheduleBlock {
  nodeId: string | null; nodeTitle: string; nodePath: string; unit: string | null;
  unitsPerHour: number | null; plannedUnits: number;
  startMinute: number; durationMinutes: number;
  totalLogged: number; totalSize: number | null; completionPercent: number;
  pointsPerUnit: number | null;
  isChecklist: boolean;
  blockId: string | null;
  poolItems: ScheduleBlockOption[] | null;
  /** Weighted sprints only: what one unit (one hour for a pool) is worth today, after the
   *  day's budget is re-split by commitment progress. Null on Fixed sprints. */
  effectivePointsPerUnit: number | null;
  /** Weighted sprints only: today's price as a percent of the item's nominal rate —
   *  112 means a unit is worth 12% more than usual today, 92 less, 100 neutral. */
  pricePercent: number | null;
}
export interface ScheduleBlockOption {
  nodeId: string; title: string; path: string; unit: string | null;
  totalSize: number | null; totalLogged: number;
  unitsPerHour: number | null; pointsPerUnit: number | null; isChecklist: boolean;
}
export interface ScheduleResponse {
  blocks: ScheduleBlock[]; activeSprint: SprintDto | null; isRelaxDay: boolean;
  /** Weighted sprints only: the day's earned points priced server-side. Null on Fixed sprints. */
  dayEarnedPoints?: number | null;
  /** Weighted sprints only: every committed item's coefficient today, scheduled today or not. */
  itemPrices?: Record<string, ItemPrice> | null;
}
export interface ItemPrice { pricePercent: number; effectivePointsPerUnit: number; }

export interface ActionableItem {
  id: string; title: string; path: string; status: ActionItemStatus;
  unit: string | null; totalSize: number | null; unitsPerHour: number | null;
  pointsPerUnit: number | null; totalLogged: number; scheduleTemplate: string | null;
}

export type SprintScoringMode = 'Fixed' | 'Weighted';
export interface SprintDto { id: string; name: string; startDate: string; endDate: string; isOpen: boolean; isStarted: boolean; relaxDays: string | null; scoringMode: SprintScoringMode; }
export interface WorkLogDto { id: string; nodeId: string; nodeTitle: string; date: string; amount: number; unit: string | null; note: string | null; }

export interface PerformanceSummary {
  items: PerformanceItem[]; totalPlannedPoints: number; totalEarnedPoints: number;
  dailyPoints: { date: string; points: number }[];
  /**
   * Earned and due to date, per day, each as a percentage of the sprint's whole plan. The four
   * `earned*` figures split `cumulativeEarned` by where it came from and add back up to it.
   */
  dailyProgress: {
    date: string; cumulativeEarned: number; cumulativePlanned: number;
    earnedPercent: number; plannedPercent: number;
    earnedSchedule: number; earnedGoals: number; earnedHabits: number; earnedOther: number;
    isFuture: boolean;
  }[];
  completedTasks: { id: string; title: string; priority: string; estimatedHours: number; points: number; completedDate: string }[];
  customLogs: { id: string; title: string; points: number; date: string; note: string | null }[];
  categoryBreakdown: CategoryTimeNode[];
  sprintGoals: SprintGoalDto[];
  /** Total flat bonus earned for sprint goals reached. Earned, never planned. */
  goalBonusPoints: number;
  /** "Fixed" or "Weighted" — how this sprint's earned figures were priced. */
  scoringMode: SprintScoringMode;
}
export interface CategoryTimeNode { categoryName: string; totalMinutes: number; totalPoints: number; depth: number; children: CategoryTimeNode[]; }
export interface PerformanceItem {
  nodeId: string; title: string; unit: string | null;
  totalSize: number | null; unitsPerHour: number | null; pointsPerUnit: number | null;
  scheduledSessions: number; plannedUnits: number; doneUnits: number;
  plannedPoints: number; earnedPoints: number;
  totalMinutes: number;
  willComplete: boolean; projectedCompletionDate: string | null;
  dailyCumulative: { date: string; cumulativePercent: number; idealPercent: number }[];
  isNodeCompleted: boolean;
  /** Worked on but never committed to — the queue only reached it because you got ahead. */
  isBonus: boolean;
  /** This row is a pool block: `nodeId` is the block id and `poolItems` holds the breakdown. */
  isPool: boolean;
  poolItems: PerformanceItem[] | null;
}

export interface CreateNodeRequest {
  parentId: string | null; title: string; isActionable: boolean; sortOrder: number;
  unit?: string | null; totalSize?: number | null; unitsPerHour?: number | null;
  pointsPerUnit?: number | null; scheduleTemplate?: string | null;
  isChecklist?: boolean;
}

export interface NodeSubPointDto { id: string; title: string; sortOrder: number; }
export interface ScheduleSubPointDto { id: string; title: string; sortOrder: number; isChecked: boolean; }

export interface WeekPlan {
  id: string; roadmapId: string; weekStart: string; isClosed: boolean; notes: string | null;
  scheduledItems: WeekScheduledItem[]; customGoals: WeekPlanGoal[];
  completedTasks: { id: string; title: string; priority: string; estimatedHours: number; points: number; completedDate: string }[];
  customLogs: { id: string; title: string; points: number; date: string; note: string | null }[];
  activeSprint: SprintDto | null;
  sprintGoals: SprintGoalDto[];
}

export interface WeekScheduledItem {
  nodeId: string; title: string; unit: string | null; unitsPerHour: number | null;
  sessionsThisWeek: number; plannedUnits: number; loggedUnits: number;
  totalSize: number | null; totalLogged: number; willCompleteThisSprint: boolean; projectedCompletionDate: string | null;
  isNodeCompleted: boolean;
}

export interface WeekPlanGoal {
  id: string; title: string; targetDescription: string | null;
  targetAmount: number | null; resultAmount: number | null; resultNote: string | null;
  isCompleted: boolean; sortOrder: number; sprintGoalId: string | null;
  sprintGoalTitle: string | null; sprintGoalTarget: number | null; sprintGoalLogged: number | null; sprintGoalUnit: string | null;
}

export interface WorkLogHistory {
  nodeId: string; nodeTitle: string; unit: string | null;
  entries: WorkLogHistoryEntry[];
}
export interface WorkLogHistoryEntry {
  id: string; date: string; amount: number; note: string | null; sprintName: string | null;
}

// Daily Notes
export interface NoteDto { book: string; dayNumber: number; entryDate: string; content: string; createdAt: string; updatedAt: string; }

// Notes v2 — flashcards, a system of its own beside the daily notes. A card is one note (its date is a
// property and a filter); what is scheduled is its prompts (FSRS). stability is in days (the interval at 90% predicted
// recall); retrievability is the predicted recall today.
export type FlashcardPromptState = 'Active' | 'Parked' | 'Suspended';
export type FlashcardGrade = 'again' | 'hard' | 'good' | 'easy';
export interface FlashcardPromptReviewDto {
  reviewedAt: string; grade: 'Again' | 'Hard' | 'Good' | 'Easy'; elapsedDays: number; retrievability: number;
  stabilityBefore: number; stabilityAfter: number; difficultyBefore: number; difficultyAfter: number;
  wasRelearning: boolean; answer: string | null; note: string | null;
}
export interface FlashcardPromptDto {
  id: string; flashcardId: string; book: string; entryDate: string; question: string; answer: string;
  sortOrder: number; state: FlashcardPromptState; difficulty: number; stability: number; retrievability: number;
  dueOn: string; isDue: boolean; relearning: boolean; lapses: number; reviews: number;
  lastReviewedAt: string; createdAt: string; reviewHistory: FlashcardPromptReviewDto[];
}
export interface FlashcardDto {
  id: string; book: string; entryDate: string; content: string;
  promptCount: number; due: number; parked: number; suspended: number; nextDueOn: string | null;
  createdAt: string; updatedAt: string; prompts: FlashcardPromptDto[];
}
export interface FlashcardSessionDto {
  date: string; dailyCap: number; askedToday: number; remaining: number; dueTotal: number; returned: number;
  overflow: number; parkedNow: number; unparked: number; parkedTotal: number; carryCapacityPerDay: number;
  desiredRetention: number; prompts: FlashcardPromptDto[];
}
export interface FlashcardReviewResultDto {
  prompt: FlashcardPromptDto; grade: 'Again' | 'Hard' | 'Good' | 'Easy'; passed: boolean; retrievabilityBefore: number;
  elapsedDays: number; intervalDays: number; leech: boolean; askedToday: number; remaining: number; dailyCap: number;
}
export interface FlashcardMemoryReviewDto { date: string; stabilityAfter: number; }
export interface FlashcardMemoryPointDto {
  book: string; state: FlashcardPromptState; stability: number; elapsedDays: number;
  /** First exposure (yyyy-mm-dd) and the stability it started with; then each review's date and resulting stability. */
  exposure: string; initialStability: number; reviews: FlashcardMemoryReviewDto[];
}
export interface FlashcardMemoryDto { today: string; desiredRetention: number; prompts: FlashcardMemoryPointDto[]; }
export interface FlashcardStatsDto {
  flashcards: number; flashcardsWithPrompts: number; flashcardsWithoutPrompts: number; prompts: number; active: number;
  parked: number; suspended: number; leeches: number; dueToday: number; askedToday: number; dailyCap: number;
  remaining: number; carryCapacityPerDay: number; desiredRetention: number; reviewsAllTime: number;
  reviewsLast7Days: number; trueRetention30d: number | null; lapses: number; averageStability: number;
  upcomingLoad: { date: string; due: number }[];
}

export type ArticleFormat = 'markdown' | 'html';
export interface ArticleImageDto { name: string; contentType: string; sortOrder: number; }
export interface ArticleSummaryDto { id: string; title: string; format: ArticleFormat; readMinutes: number; isRead: boolean; readOn: string | null; sortOrder: number; imageCount: number; readProgress: number; createdAt: string; updatedAt: string; }
export interface ArticleDto { id: string; title: string; format: ArticleFormat; content: string; readMinutes: number; isRead: boolean; readOn: string | null; sortOrder: number; images: ArticleImageDto[]; chatUrl: string | null; readProgress: number; readAnchor: string | null; createdAt: string; updatedAt: string; }

// Professional Newsletter — agent-published HTML editions, one per day, rolling 14-day window.
export interface NewsletterSummaryDto {
  id: string; issueDate: string; title: string; coveredFrom: string; coveredUntil: string;
  itemCount: number; isRead: boolean; readOn: string | null; createdAt: string; updatedAt: string;
}
/** Where the next run picks up, and how wide a window it should cover. */
export interface NewsletterCursorDto {
  lastReadDate: string | null; latestIssueDate: string | null; since: string; sinceDate: string;
  windowDays: number; cappedToMaxWindow: boolean; maxWindowDays: number; retentionDays: number;
  unreadCount: number; issueCount: number; anchor: string;
}

// Stock Signals — the market screener's runs. One per trading day, quiet days included; each signal carries
// the rule's evidence and thesis, the triage verdict written later, and the reader's decision.
export const SIGNAL_STATUSES = ['new', 'reviewed', 'acted', 'dismissed'] as const;
export type SignalStatus = typeof SIGNAL_STATUSES[number];
export interface SignalEvidence { label: string; value: string; threshold?: string | null; passes?: boolean | null; note?: string | null; }
export interface SignalMarket {
  benchmark?: string; close?: number | null; ret_1d?: number | null; dd_252d?: number | null; vol_name?: string;
  vol?: number | null; hy_oas?: number | null; hy_oas_20d_change?: number | null; breadth?: number | null;
}
export interface SignalWatchItem { market?: string; group: string; z?: number | null; dd_20d?: number | null; rev?: number | null; cutting?: number | null; note?: string | null; why_not?: string | null; }
export interface SignalDto {
  id: string; eventId: string; market: string; screen: string; key: string; tier: number; title: string;
  headline: string; thesis: string; evidence: SignalEvidence[]; horizon: string; horizonDays: number | null;
  proposal: string | null; proposalDetail: Record<string, unknown> | null; candidates: Record<string, unknown>[];
  context: Record<string, unknown> | null; invalidation: string | null; regime: string | null; nextStep: string | null;
  sortOrder: number; triage: unknown; triageSummary: string | null; triagedAt: string | null; triageModel: string | null;
  status: string; notes: string | null; decidedAt: string | null; updatedAt: string;
}
export interface SignalRunSummaryDto {
  id: string; runDate: string; status: string; isTradingDay: boolean; summary: string; signalCount: number;
  watchCount: number; isRead: boolean; readOn: string | null; publishedAt: string; gitSha: string | null;
}
export interface SignalRunDto {
  id: string; runDate: string; status: string; isTradingDay: boolean; summary: string; briefing: string | null;
  markets: Record<string, SignalMarket> | null; watch: SignalWatchItem[] | null; warnings: string[];
  reportMarkdown: string | null; gitSha: string | null; isRead: boolean; readOn: string | null;
  publishedAt: string; createdAt: string; updatedAt: string; signals: SignalDto[];
}

// Habits
export interface HabitDto { id: string; name: string; createdAt: string; }
export interface SprintHabitDto { sprintHabitId: string; habitId: string; name: string; isPaused: boolean; currentStreak: number; bestStreak: number; isFormed: boolean; checks: { date: string; isChecked: boolean }[]; }
export interface ScheduleHabitDto { sprintHabitId: string; habitId: string; name: string; isCheckedToday: boolean; currentStreak: number; isFormed: boolean; }

// Single Tasks
export interface SingleTaskDto { id: string; title: string; priority: string; estimatedHours: number; weekdays: string | null; startDate: string; dueDate: string | null; delayedUntil: string | null; isCompleted: boolean; completedDate: string | null; points: number; }
export interface ScheduleTaskDto { id: string; title: string; priority: string; estimatedHours: number; points: number; isCompleted: boolean; dueDate: string | null; isOverdue: boolean; }

// Custom Logs
export interface CustomLogDto { id: string; title: string; points: number; date: string; note: string | null; }

// Schedule Blocks
/** Queue: items take the slot one after another, in order. Pool: the block takes the slot. */
export type ScheduleBlockMode = 'Queue' | 'Pool';
export interface ScheduleBlockDef { id: string; name: string; scheduleTemplate: string | null; sortOrder: number; mode: ScheduleBlockMode; items: ScheduleBlockItem[]; }
export interface ScheduleBlockItem { nodeId: string; title: string; unit: string | null; totalSize: number | null; unitsPerHour: number | null; status: string; blockSortOrder: number; isActiveInBlock: boolean; }

// Sprint Goals
export interface SprintGoalDto { id: string; title: string; unit: string | null; targetAmount: number; description: string | null; sortOrder: number; loggedAmount: number; }

// Job scouting (postings imported from the Finder pipeline, one run per day)
export interface CvFitGapDto { label: string; points: number; note: string | null; }
export interface JobPostingDto { id: string; title: string; company: string; url: string; source: string; location: string | null; postedAt: string | null; description: string; bucket: string; seniorityClass: string | null; aiKeywordHits: number; geoHints: string[]; queries: string[]; score: number | null; reasoning: string | null; sortOrder: number; hasCv: boolean; cvChangeList: string | null; cvFitScore: number | null; cvFitGaps: CvFitGapDto[]; applicationStatus: string | null; appliedAt: string | null; respondedAt: string | null; applicationNotes: string | null; }

// The statuses the Jobs tab offers for an application. The column is free text
// server-side, so this list can grow without a migration.
export const APPLICATION_STATUSES = ['none', 'applied', 'screening', 'interviewing', 'offer', 'rejected', 'ghosted'] as const;
export type ApplicationStatus = typeof APPLICATION_STATUSES[number];
export interface JobRunSummaryDto { id: string; runDate: string; queries: string[]; maxAgeDays: number; rawCount: number; postingCount: number; createdAt: string; }
export interface JobRunDto { id: string; runDate: string; queries: string[]; maxAgeDays: number; rawCount: number; createdAt: string; postings: JobPostingDto[]; }

// English vocabulary (words / idioms / phrasal verbs, SM-2 scheduled; added and reviewed over MCP)
export interface VocabReviewDto { reviewedAt: string; grade: number; promptType: string | null; answer: string | null; note: string | null; intervalBefore: number; intervalAfter: number; easeBefore: number; easeAfter: number; }
export interface VocabEntryDto { id: string; term: string; kind: string; definition: string; glossHy: string | null; glossRu: string | null; frequency: string; register: string; examples: string[]; collocations: string[]; synonyms: string[]; memoryHook: string | null; sourceContext: string | null; notes: string | null; repetitions: number; easeFactor: number; intervalDays: number; dueOn: string; lastReviewedAt: string | null; lapses: number; totalReviews: number; strength: string; isDue: boolean; createdAt: string; reviews: VocabReviewDto[]; }
export interface VocabStatsDto { total: number; dueToday: number; new: number; learning: number; young: number; mature: number; reviewsAllTime: number; reviewsLast7Days: number; averageEase: number; lapses: number; }

// Nutrition (the meal book — what to eat, kept per part of the day)
export type MealSlot = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack';
// hasImage/imageUpdatedAt describe the photo without carrying it — the bytes come from
// /api/meals/{id}/image, and imageUpdatedAt is the cache key that busts a replaced photo.
export interface MealDto { id: string; slot: MealSlot; name: string; summary: string | null; ingredients: string[]; steps: string[]; calories: number | null; proteinG: number | null; carbsG: number | null; fatG: number | null; prepMinutes: number | null; tags: string[]; isFavorite: boolean; sortOrder: number; hasImage: boolean; imageContentType: string | null; imageUpdatedAt: string | null; createdAt: string; updatedAt: string; }

// The food log — what was actually eaten, day by day. Macros on an entry are per serving;
// totals are them times servings. A null macro is unknown, never zero.
export interface MacroTotals { calories: number | null; proteinG: number | null; carbsG: number | null; fatG: number | null; }
export interface FoodLogEntryDto { id: string; date: string; slot: MealSlot; mealId: string | null; name: string; servings: number; calories: number | null; proteinG: number | null; carbsG: number | null; fatG: number | null; totals: MacroTotals; missingMacros: boolean; note: string | null; createdAt: string; updatedAt: string; }
// target: the targets in effect that day (calories = maintenance); null when none were set.
export interface FoodLogDayDto { date: string; totals: MacroTotals; entryCount: number; incompleteEntries: number; entries: FoodLogEntryDto[]; target: MacroTotals | null; }
// One set of daily targets, in effect from effectiveFrom until the next set. calories = maintenance.
export interface NutritionTargetDto { effectiveFrom: string; calories: number | null; proteinG: number | null; carbsG: number | null; fatG: number | null; }
export interface SaveFoodLogEntryRequest { date: string; slot: MealSlot; mealId: string | null; name: string; servings: number; calories: number | null; proteinG: number | null; carbsG: number | null; fatG: number | null; note: string | null; }
export interface SaveMealRequest { slot: MealSlot; name: string; summary?: string | null; ingredients: string[]; steps: string[]; calories?: number | null; proteinG?: number | null; carbsG?: number | null; fatG?: number | null; prepMinutes?: number | null; tags: string[]; isFavorite?: boolean; }

// Courses (a taught course: stages of lessons, each lesson a template's sections plus graded
// exercises). The agent writes them over MCP; the tab reads. Statuses arrive as the same
// snake_case strings the write side takes, so one read straight back into a write is safe.
export type CourseStatus = 'draft' | 'active' | 'paused' | 'completed' | 'archived';
export type StageStatus = 'planned' | 'defined' | 'in_progress' | 'completed' | 'skipped';
export type LessonStatus = 'placeholder' | 'draft' | 'ready' | 'in_progress' | 'submitted' | 'completed' | 'skipped';

export interface CourseFocusDto { code: string; title: string; status: LessonStatus; }
export interface CourseSummaryDto {
  id: string; slug: string; title: string; subtitle: string | null; status: CourseStatus;
  progress: number; score: number | null; definedFraction: number;
  currentLesson: CourseFocusDto | null; updatedAt: string;
}

export interface TemplateSectionDto { kind: string; title: string | null; required: boolean; hasExercises?: boolean; }
export interface CourseResourceDto { id: string; kind: string; title: string; url: string | null; noteMd: string | null; }

export interface CourseLessonDto {
  id: string; code: string; title: string; status: LessonStatus; position: number;
  progress: number; score: number | null; estimatedHours: number | null; exerciseCount: number;
}
export interface CourseStageDto {
  id: string; code: string; title: string; status: StageStatus; position: number;
  targetWeeks: number | null; progress: number; score: number | null; definedFraction: number;
  lessonsDefined: number; lessonsTotal: number; lessons: CourseLessonDto[] | null;
}
export interface CourseDetailDto {
  id: string; slug: string; title: string; subtitle: string | null; status: CourseStatus;
  descriptionMd: string | null; capstoneMd: string | null;
  // The course's instructions.md — how it is taught, graded and written. Read-only here: it is the
  // agent's brief, and the agent is what writes it.
  instructionsMd: string | null;
  targetHoursPerWeek: number | null;
  template: { id: string; name: string; sections: TemplateSectionDto[] };
  progress: number; score: number | null; definedFraction: number;
  startedAt: string | null; completedAt: string | null; updatedAt: string;
  stages: CourseStageDto[]; resources: CourseResourceDto[];
}

export interface GradeDto {
  id: string; score: number; maxScore: number; feedbackMd: string | null;
  gradedBy: string; gradedAt: string; supersedes: string | null;
}
export interface SubmissionDto {
  id: string; attemptNo: number; contentMd: string | null; links: { label?: string; url: string; kind?: string }[];
  submittedBy: string; submittedAt: string; grades: GradeDto[] | null;
}
export interface ExerciseDto {
  id: string; kind: string; sectionKind: string; title: string; promptMd: string;
  referenceMd: string | null; maxScore: number; weight: number; required: boolean;
  attempts: number; submissions: SubmissionDto[] | null;
}
export interface LessonSectionDto { kind: string; title: string | null; contentMd: string; }
export interface LessonDetailDto {
  id: string; code: string; title: string; status: LessonStatus; summaryMd: string | null;
  estimatedHours: number | null; stage: { id: string; code: string; title: string };
  progress: number | null; score: number | null;
  gradedFraction: number | null; submittedFraction: number | null;
  templateSections: TemplateSectionDto[];
  // The template's own order for both — a kind's colour comes from its place here, not from the
  // order things happen to be rendered in, so a kind keeps its hue as work is added.
  templateExerciseKinds: { kind: string; label: string | null }[];
  sections: LessonSectionDto[] | null; exercises: ExerciseDto[] | null;
  resources: CourseResourceDto[];
}

export interface CourseEventDto {
  id: number; type: string; actor: string; createdAt: string;
  stageId: string | null; lessonId: string | null; exerciseId: string | null;
  payload: Record<string, unknown>;
}
export interface CourseResumeDto {
  course: { slug: string; title: string; status: string; progress: number; score: number | null;
    definedFraction: number };
  currentStage: { code: string; title: string; status: StageStatus; progress: number; score: number | null } | null;
  currentLesson: { code: string; title: string; status: LessonStatus; progress: number; score: number | null } | null;
  pendingExercises: { id: string; kind: string; title: string; hasSubmission: boolean; hasGrade: boolean }[];
  latestHandoff: { md: string | null; createdAt: string; actor: string } | null;
  recentGrades: { lessonCode: string; exerciseTitle: string; score: number; maxScore: number; gradedAt: string }[];
  nextUndefinedStage: { code: string; title: string; placeholderLessons: number } | null;
  recentEvents: CourseEventDto[];
}

// Experiences (global — the list of things planned and the record of things done, with pictures).
// Dates are yyyy-MM-dd; only the title is required, since a plan is worth saving before it has
// a place, a date or a picture.
export type ExperienceStatus = 'planned' | 'done';
export interface ExperienceImageDto {
  id: string; contentType: string; caption: string | null; fileName: string | null;
  sortOrder: number; createdAt: string;
}
export interface ExperienceDto {
  id: string; title: string; category: string | null; tags: string[]; status: ExperienceStatus; location: string | null;
  startDate: string | null; endDate: string | null; descriptionMd: string | null;
  images: ExperienceImageDto[]; createdAt: string; updatedAt: string;
}
export interface SaveExperienceRequest {
  title: string; category: string | null; tags: string[]; status: ExperienceStatus; location: string | null;
  startDate: string | null; endDate: string | null; descriptionMd: string | null;
}
