import { Navigate, createBrowserRouter } from "react-router-dom";
import { AppShell } from "@/layouts/AppShell";
import { ProtectedRoute } from "@/app/router/ProtectedRoute";
import { OrganizationRoute } from "@/app/router/OrganizationRoute";
import { LoginPage } from "@/features/auth/pages/LoginPage";
import { RegisterPage } from "@/features/auth/pages/RegisterPage";
import { ForgotPasswordPage } from "@/features/auth/pages/ForgotPasswordPage";
import { ResetPasswordPage } from "@/features/auth/pages/ResetPasswordPage";
import { ConfirmEmailPage } from "@/features/auth/pages/ConfirmEmailPage";
import { CreateOrganizationPage } from "@/features/organizations/pages/CreateOrganizationPage";
import { OrganizationDashboardPage } from "@/features/organizations/pages/OrganizationDashboardPage";
import { OrganizationEntryPage } from "@/features/organizations/pages/OrganizationEntryPage";
import { OrganizationSettingsPage } from "@/features/organizations/pages/OrganizationSettingsPage";
import { MembersPage } from "@/features/memberships/pages/MembersPage";
import { InvitationAcceptancePage } from "@/features/invitations/pages/InvitationAcceptancePage";
import { permissions } from "@/features/authorization/permissions";
import { PermissionRoute } from "./PermissionRoute";

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  { path: "/register", element: <RegisterPage /> },
  { path: "/forgot-password", element: <ForgotPasswordPage /> },
  { path: "/reset-password", element: <ResetPasswordPage /> },
  { path: "/confirm-email", element: <ConfirmEmailPage /> },
  { path: "/invitations/:token", element: <InvitationAcceptancePage /> },
  {
    path: "/",
    element: <ProtectedRoute />,
    children: [
      { index: true, element: <OrganizationEntryPage /> },
      { path: "organizations/new", element: <CreateOrganizationPage /> },
      {
        path: "app/:organizationSlug",
        element: <OrganizationRoute />,
        children: [
          {
            element: <AppShell />,
            children: [
              { index: true, element: <Navigate to="dashboard" replace /> },
              { path: "dashboard", element: <OrganizationDashboardPage /> },
              {
                element: <PermissionRoute permission={permissions.memberView} />,
                children: [{ path: "members", element: <MembersPage /> }],
              },
              {
                element: <PermissionRoute permission={permissions.organizationManage} />,
                children: [{ path: "settings", element: <OrganizationSettingsPage /> }],
              },
            ],
          },
        ],
      },
    ],
  },
]);
