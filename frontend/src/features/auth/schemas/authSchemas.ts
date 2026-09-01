import { z } from "zod";

const passwordSchema = z
  .string()
  .min(8, "Şifre en az 8 karakter olmalı.")
  .regex(/[a-z]/, "Şifre en az bir küçük harf içermeli.")
  .regex(/[A-Z]/, "Şifre en az bir büyük harf içermeli.")
  .regex(/[0-9]/, "Şifre en az bir rakam içermeli.");

export const registerSchema = z.object({
  email: z.string().min(1, "E-posta zorunludur.").email("Geçerli bir e-posta girin."),
  password: passwordSchema,
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
  newPassword: passwordSchema,
});

export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;
