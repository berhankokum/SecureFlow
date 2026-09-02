import { z } from "zod";

export const teamSchema = z.object({
  name: z.string().trim().min(1, "Team adı zorunludur.").max(200, "Team adı en fazla 200 karakter olabilir."),
  description: z.string().trim().max(2000, "Açıklama en fazla 2000 karakter olabilir."),
});

export type TeamFormValues = z.infer<typeof teamSchema>;
