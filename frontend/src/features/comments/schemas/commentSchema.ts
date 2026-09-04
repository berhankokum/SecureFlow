import { z } from "zod";

export const commentSchema = z.object({
  content: z.string().trim().min(1, "Yorum boş olamaz.").max(5_000, "Yorum en fazla 5000 karakter olabilir."),
});

export type CommentFormValues = z.infer<typeof commentSchema>;
