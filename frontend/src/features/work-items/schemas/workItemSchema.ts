import { z } from "zod";

const fields = {
  type: z.enum(["TASK", "BUG", "STORY"]),
  title: z.string().trim().min(1, "Başlık zorunludur.").max(200, "Başlık en fazla 200 karakter olabilir."),
  description: z.string().trim().max(10_000, "Açıklama en fazla 10.000 karakter olabilir."),
  priority: z.enum(["LOW", "MEDIUM", "HIGH", "CRITICAL"]),
  dueDate: z.string(),
};

const todayUtc = () => new Date().toISOString().slice(0, 10);

export const createWorkItemSchema = z.object(fields).refine(
  values => !values.dueDate || values.dueDate >= todayUtc(),
  { path: ["dueDate"], message: "Due date geçmiş bir tarih olamaz." },
);
export const updateWorkItemSchema = z.object(fields);

export type WorkItemFormValues = z.infer<typeof updateWorkItemSchema>;
