import { TaskPriority, TaskStatus } from '../../core/tasks/task.models';

/** GET /api/dashboard: statistics over the projects the caller can see (all projects for an Admin). */
export interface Dashboard {
  /** UTC date the API used as "today" (yyyy-mm-dd). */
  asOf: string;
  totalProjects: number;
  totalTasks: number;
  tasksByStatus: { status: TaskStatus; count: number }[];
  tasksByPriority: { priority: TaskPriority; count: number }[];
  completion: { completed: number; pending: number; cancelled: number };
  /** Pending tasks due before asOf. */
  overdueCount: number;
  upcomingWindowDays: number;
  upcomingCount: number;
  upcomingTasks: UpcomingTask[];
  myTasks: { pending: number; overdue: number };
}

export interface UpcomingTask {
  id: string;
  projectId: string;
  projectName: string;
  title: string;
  status: TaskStatus;
  priority: TaskPriority;
  /** yyyy-mm-dd */
  dueDate: string;
  assigneeId: string | null;
  assigneeName: string | null;
}
