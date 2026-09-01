import { z } from "zod";

const optionalHttpUrl = z
  .string()
  .trim()
  .max(2048, "Logo adresi en fazla 2048 karakter olabilir.")
  .refine(
    (value) => {
      if (value.length === 0) {
        return true;
      }

      try {
        const url = new URL(value);
        return url.protocol === "http:" || url.protocol === "https:";
      } catch {
        return false;
      }
    },
    "Logo adresi http:// veya https:// ile başlamalıdır.",
  );

export const createOrganizationSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Organization adı zorunludur.")
    .max(200, "Organization adı en fazla 200 karakter olabilir."),
  logoUrl: optionalHttpUrl,
});

export type CreateOrganizationFormValues = z.infer<typeof createOrganizationSchema>;
