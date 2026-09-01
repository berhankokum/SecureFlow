import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { OrganizationProvider } from "../context/OrganizationProvider";
import { OrganizationSwitcher } from "./OrganizationSwitcher";
import type { Organization } from "../api/types";

const navigateMock = vi.fn();

const organizations: Organization[] = [
  {
    id: "11111111-1111-1111-1111-111111111111",
    name: "ACME",
    slug: "acme",
    logoUrl: null,
    role: "OWNER",
    permissions: ["organization.manage", "member.view"],
    createdAt: "2026-08-29T00:00:00Z",
    archivedAt: null,
  },
  {
    id: "22222222-2222-2222-2222-222222222222",
    name: "CyberTech",
    slug: "cybertech",
    logoUrl: null,
    role: "MEMBER",
    permissions: ["member.view"],
    createdAt: "2026-08-29T00:00:00Z",
    archivedAt: null,
  },
];

vi.mock("../hooks/useOrganizations", () => ({
  useOrganizations: () => ({ data: organizations }),
}));

vi.mock("react-router-dom", async (importOriginal) => {
  const actual = await importOriginal<typeof import("react-router-dom")>();
  return { ...actual, useNavigate: () => navigateMock };
});

describe("OrganizationSwitcher", () => {
  beforeEach(() => navigateMock.mockReset());

  it("navigates to the selected tenant", async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <OrganizationProvider organization={organizations[0]}>
        <OrganizationSwitcher />
      </OrganizationProvider>,
    );

    await user.selectOptions(screen.getByLabelText("Organization"), "cybertech");

    expect(navigateMock).toHaveBeenCalledWith("/app/cybertech/dashboard");
  });
});
