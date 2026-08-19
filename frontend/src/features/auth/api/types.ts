export interface RegisterPayload {
  email: string;
  password: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface ConfirmEmailPayload {
  userId: string;
  code: string;
}

export interface ForgotPasswordPayload {
  email: string;
}

export interface ResetPasswordPayload {
  email: string;
  code: string;
  newPassword: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  emailConfirmed: boolean;
}
