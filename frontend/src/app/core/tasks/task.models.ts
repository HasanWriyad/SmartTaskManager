/** Task enums as the API sends them (string names). */
export type TaskStatus = 'ToDo' | 'InProgress' | 'Completed' | 'Cancelled';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export const taskStatusLabels: Record<TaskStatus, string> = {
  ToDo: 'To do',
  InProgress: 'In progress',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

export const taskPriorityLabels: Record<TaskPriority, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Critical: 'Critical',
};

/** Signal-bar count shown in the priority badge (1 of 4 for Low ... 4 of 4 for Critical). */
export const taskPriorityLevels: Record<TaskPriority, number> = {
  Low: 1,
  Medium: 2,
  High: 3,
  Critical: 4,
};
