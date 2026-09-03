import { z } from "zod";

export const labelSchema = z.object({
  name: z.string().transform(value => value.normalize("NFKC").trim())
    .pipe(z.string().min(1, "Label adı gerekli.").max(50, "Label adı en fazla 50 karakter olabilir.")),
});
export type LabelFormValues = z.infer<typeof labelSchema>;
