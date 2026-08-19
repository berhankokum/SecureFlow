import { z } from "zod";

export const registerSchema = z.object({
  email: z.string().min(1, "E-posta zorunludur.").email("Geçerli bir e-posta girin."),
  password: z.string().min(8, "Şifre en az 8 karakter olmalı."),
});

export type RegisterFormValues = z.infer<typeof registerSchema>;

export const loginSchema = z.object({
  email: z.string().min(1, "E-posta zorunludur.").email("Geçerli bir e-posta girin."),
  password: z.string().min(1, "Şifre zorunludur."),
});

export type LoginFormValues = z.infer<typeof loginSchema>;

export const forgotPasswordSchema = z.object({
  email: z.string().min(1, "E-posta zorunludur.").email("Geçerli bir e-posta girin."),
});

export type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;

export const resetPasswordSchema = z.object({
  newPassword: z.string().min(8, "Şifre en az 8 karakter olmalı."),
});

export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;
