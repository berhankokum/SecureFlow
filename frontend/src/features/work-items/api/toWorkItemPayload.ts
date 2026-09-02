import type { WorkItemFormValues } from "../schemas/workItemSchema";

export function toWorkItemPayload(values: WorkItemFormValues) {
  return {
    type: values.type,
    title: values.title,
    description: values.description || undefined,
    priority: values.priority,
    dueDate: values.dueDate || undefined,
  };
}
