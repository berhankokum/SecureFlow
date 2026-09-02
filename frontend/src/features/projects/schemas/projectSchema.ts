import { z } from "zod";

const fields = {
  name: z.string().trim().min(1, "Project adı zorunludur.").max(200, "Project adı en fazla 200 karakter olabilir."),
  description: z.string().trim().max(2000, "Açıklama en fazla 2000 karakter olabilir."),
};

export const createProjectSchema = z.object({
  key: z.string().trim().regex(/^[A-Za-z][A-Za-z0-9]{1,9}$/, "Key, harfle başlamalı ve 2-10 harf/rakam içermelidir."),
  ...fields,
});

export const updateProjectSchema = z.object(fields);
export type CreateProjectFormValues = z.infer<typeof createProjectSchema>;
export type UpdateProjectFormValues = z.infer<typeof updateProjectSchema>;
