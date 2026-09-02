import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { OrganizationProvider } from "@/features/organizations/context/OrganizationProvider";
import type { Organization } from "@/features/organizations/api/types";
import { renderWithProviders } from "@/test/renderWithProviders";
import { TeamsPage } from "./TeamsPage";

const mutateAsync = vi.fn();
const ownerOrganization: Organization = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "ACME",
  slug: "acme",
  logoUrl: null,
  role: "OWNER",
  permissions: ["member.view", "team.create", "team.update", "team.archive", "team.member.manage"],
  createdAt: "2026-09-02T00:00:00Z",
  archivedAt: null,
};

vi.mock("../hooks/useTeams", () => ({
  useTeams: () => ({ data: [{
    id: "t1",
    organizationId: ownerOrganization.id,
    name: "Platform",
    description: "Core services",
    memberCount: 2,
    createdAt: "2026-09-02T00:00:00Z",
    updatedAt: "2026-09-02T00:00:00Z",
    archivedAt: null,
  }], isLoading: false, isError: false }),
  useCreateTeam: () => ({ mutateAsync }),
}));

describe("TeamsPage", () => {
  beforeEach(() => mutateAsync.mockReset().mockResolvedValue({ id: "t2" }));

  it("lists teams and lets an authorized owner create one", async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <OrganizationProvider organization={ownerOrganization}><TeamsPage /></OrganizationProvider>,
      { route: "/app/acme/teams" },
    );

    expect(screen.getByText("Platform")).toBeInTheDocument();
    expect(screen.getByText("2 üye")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Team oluştur" }));
    await user.type(screen.getByLabelText("Team adı"), "Security");
    await user.type(screen.getByLabelText(/Açıklama/), "Security operations");
    await user.click(screen.getAllByRole("button", { name: "Team oluştur" }).at(-1)!);

    expect(mutateAsync).toHaveBeenCalledWith({ name: "Security", description: "Security operations" });
  });

  it("does not expose create action without permission", () => {
    renderWithProviders(
      <OrganizationProvider organization={{ ...ownerOrganization, role: "MEMBER", permissions: ["member.view"] }}>
        <TeamsPage />
      </OrganizationProvider>,
    );
    expect(screen.queryByRole("button", { name: "Team oluştur" })).not.toBeInTheDocument();
  });
});
