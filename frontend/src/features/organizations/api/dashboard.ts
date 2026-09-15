export interface Dashboard {
  today: string;
  activeProjects: number;
  myWorkItems: number;
  overdue: number;
  dueSoon: number;
  critical: number;
  tasksByState: { stateId: string; name: string; count: number }[];
  myTasks: { id: string; key: string; title: string; dueDate: string | null; state: string }[];
  myTasksPage: number;
  myTasksPageSize: number;
  recentActivity: { id: string; actorId: string; type: string; createdAt: string; workItemKey: string; workItemTitle: string }[];
}
