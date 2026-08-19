import { createBrowserRouter } from "react-router-dom";
import { AppShell } from "@/layouts/AppShell";
import { ProtectedRoute } from "@/app/router/ProtectedRoute";
import { FoundationStatusPage } from "@/app/router/FoundationStatusPage";
import { LoginPage } from "@/features/auth/pages/LoginPage";
import { RegisterPage } from "@/features/auth/pages/RegisterPage";
import { ForgotPasswordPage } from "@/features/auth/pages/ForgotPasswordPage";
import { ResetPasswordPage } from "@/features/auth/pages/ResetPasswordPage";
import { ConfirmEmailPage } from "@/features/auth/pages/ConfirmEmailPage";

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  { path: "/register", element: <RegisterPage /> },
  { path: "/forgot-password", element: <ForgotPasswordPage /> },
  { path: "/reset-password", element: <ResetPasswordPage /> },
  { path: "/confirm-email", element: <ConfirmEmailPage /> },
  {
    path: "/",
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [{ index: true, element: <FoundationStatusPage /> }],
      },
    ],
  },
]);
