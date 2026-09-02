import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Organization } from "@/features/organizations/api/types";
import { OrganizationProvider } from "@/features/organizations/context/OrganizationProvider";
import { renderWithProviders } from "@/test/renderWithProviders";
import { ProjectsPage } from "./ProjectsPage";

const mutateAsync = vi.fn();
const organization: Organization = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "ACME",
  slug: "acme",
  logoUrl: null,
  role: "OWNER",
  permissions: ["member.view", "project.create", "project.update", "project.archive"],
  createdAt: "2026-09-02T00:00:00Z",
  archivedAt: null,
};

vi.mock("../hooks/useProjects", () => ({
  useProjects: () => ({ data: [{
    id: "p1", organizationId: organization.id, key: "SF", name: "SecureFlow",
    description: "Delivery platform", createdBy: "owner", createdAt: "2026-09-02T00:00:00Z",
    updatedAt: "2026-09-02T00:00:00Z", archivedAt: null, projectRole: "MANAGER",
    permissions: ["project.update", "project.archive"],
  }], isLoading: false, isError: false }),
  useCreateProject: () => ({ mutateAsync }),
}));

describe("ProjectsPage", () => {
  beforeEach(() => mutateAsync.mockReset().mockResolvedValue({ id: "p2" }));

  it("lists accessible projects and creates a normalized project key", async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrganizationProvider organization={organization}><ProjectsPage /></OrganizationProvider>);
    expect(screen.getByText("SecureFlow")).toBeInTheDocument();
    expect(screen.getByText("Rol: MANAGER")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Project oluştur" }));
    await user.type(screen.getByLabelText("Project key"), "web");
    await user.type(screen.getByLabelText("Project adı"), "Web Platform");
    await user.click(screen.getAllByRole("button", { name: "Project oluştur" }).at(-1)!);

    expect(mutateAsync).toHaveBeenCalledWith({ key: "WEB", name: "Web Platform", description: undefined });
  });

  it("hides create action without project.create", () => {
    renderWithProviders(<OrganizationProvider organization={{ ...organization, role: "MEMBER", permissions: ["member.view"] }}><ProjectsPage /></OrganizationProvider>);
    expect(screen.queryByRole("button", { name: "Project oluştur" })).not.toBeInTheDocument();
  });
});
