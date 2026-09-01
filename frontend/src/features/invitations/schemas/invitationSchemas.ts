import { z } from "zod";

export const invitationSchema = z.object({
  email: z.string().trim().min(1, "E-posta zorunludur.").email("Geçerli bir e-posta girin."),
  role: z.enum(["OWNER", "ADMIN", "MEMBER"]),
});

export type InvitationFormValues = z.infer<typeof invitationSchema>;
