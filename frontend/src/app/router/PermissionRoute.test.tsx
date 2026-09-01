import { screen } from "@testing-library/react";
import { Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { permissions } from "@/features/authorization/permissions";
import type { Organization } from "@/features/organizations/api/types";
import { OrganizationProvider } from "@/features/organizations/context/OrganizationProvider";
import { renderWithProviders } from "@/test/renderWithProviders";
import { PermissionRoute } from "./PermissionRoute";

const organization: Organization = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "ACME",
  slug: "acme",
  logoUrl: null,
  role: "MEMBER",
  permissions: [permissions.memberView],
  createdAt: "2026-09-02T00:00:00Z",
  archivedAt: null,
};

describe("PermissionRoute", () => {
  it("renders the protected route when the effective permission exists", () => {
    renderWithProviders(
      <OrganizationProvider organization={organization}>
        <Routes>
          <Route element={<PermissionRoute permission={permissions.memberView} />}>
            <Route path="members" element={<h1>Members content</h1>} />
          </Route>
        </Routes>
      </OrganizationProvider>,
      { route: "/members" },
    );

    expect(screen.getByRole("heading", { name: "Members content" })).toBeInTheDocument();
  });

  it("renders a forbidden state when the permission is absent", () => {
    renderWithProviders(
      <OrganizationProvider organization={organization}>
        <Routes>
          <Route element={<PermissionRoute permission={permissions.organizationManage} />}>
            <Route path="settings" element={<h1>Settings content</h1>} />
          </Route>
        </Routes>
      </OrganizationProvider>,
      { route: "/settings" },
    );

    expect(screen.getByRole("heading", { name: "Bu alanı görüntüleme yetkiniz yok" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Settings content" })).not.toBeInTheDocument();
  });
});
