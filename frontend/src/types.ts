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

export interface ScheduleBlock {
  nodeId: string; nodeTitle: string; nodePath: string; unit: string | null;
  unitsPerHour: number | null; plannedUnits: number;
  startMinute: number; durationMinutes: number;
  totalLogged: number; totalSize: number | null; completionPercent: number;
  pointsPerUnit: number | null;
  isChecklist: boolean;
}
export interface ScheduleResponse { blocks: ScheduleBlock[]; activeSprint: SprintDto | null; isRelaxDay: boolean; }

export interface ActionableItem {
  id: string; title: string; path: string; status: ActionItemStatus;
  unit: string | null; totalSize: number | null; unitsPerHour: number | null;
  pointsPerUnit: number | null; totalLogged: number; scheduleTemplate: string | null;
}

export interface SprintDto { id: string; name: string; startDate: string; endDate: string; isOpen: boolean; isStarted: boolean; relaxDays: string | null; }
export interface WorkLogDto { id: string; nodeId: string; nodeTitle: string; date: string; amount: number; unit: string | null; note: string | null; }

export interface PerformanceSummary {
  items: PerformanceItem[]; totalPlannedPoints: number; totalEarnedPoints: number;
  dailyPoints: { date: string; points: number }[];
  completedTasks: { id: string; title: string; priority: string; estimatedHours: number; points: number; completedDate: string }[];
  customLogs: { id: string; title: string; points: number; date: string; note: string | null }[];
  categoryBreakdown: CategoryTimeNode[];
  sprintGoals: SprintGoalDto[];
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
export interface ScheduleBlockDef { id: string; name: string; scheduleTemplate: string | null; sortOrder: number; items: ScheduleBlockItem[]; }
export interface ScheduleBlockItem { nodeId: string; title: string; unit: string | null; totalSize: number | null; unitsPerHour: number | null; status: string; blockSortOrder: number; }

// Sprint Goals
export interface SprintGoalDto { id: string; title: string; unit: string | null; targetAmount: number; description: string | null; sortOrder: number; loggedAmount: number; }
