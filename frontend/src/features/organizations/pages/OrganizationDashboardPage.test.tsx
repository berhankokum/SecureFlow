import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { OrganizationDashboardPage } from "./OrganizationDashboardPage";
import type { Dashboard } from "../api/dashboard";

const state = vi.hoisted(() => ({ get: vi.fn(), userId: "user", org: "org" }));
vi.mock("@/lib/api/apiClient", () => ({ apiClient: { get: state.get } }));
vi.mock("@/features/auth/hooks/useCurrentUser", () => ({ useCurrentUser: () => ({ data: state.userId ? { id: state.userId } : undefined }) }));
vi.mock("../hooks/useCurrentOrganization", () => ({ useCurrentOrganization: () => ({ id: state.org, slug: "acme", name: "ACME" }) }));
const empty: Dashboard = { today: "2026-09-16", activeProjects: 0, myWorkItems: 0, overdue: 0, dueSoon: 0, critical: 0, tasksByState: [], myTasks: [], myTasksPage: 1, myTasksPageSize: 20, recentActivity: [] };

describe("OrganizationDashboardPage", () => {
  beforeEach(() => { state.userId = "user"; state.org = "org"; state.get.mockReset().mockResolvedValue(empty); });
  it("shows all empty sections and waits for authenticated user context", async () => {
    state.userId = "";
    const view = renderWithProviders(<OrganizationDashboardPage />);
    expect(state.get).not.toHaveBeenCalled();
    view.unmount();
    state.userId = "user";
    renderWithProviders(<OrganizationDashboardPage />);
    expect(await screen.findByText("Görüntülenecek iş yok.")).toBeInTheDocument();
    expect(screen.getByText("Henüz aktivite yok.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sonraki sayfa" })).toBeDisabled();
  });
  it("renders accessible state bars and safe links, changes task pages and restores URL state", async () => {
    state.get.mockResolvedValue({ ...empty, activeProjects: 1, myWorkItems: 41, tasksByState: [{ stateId: "todo", name: "TODO", count: 41 }], myTasks: [{ id: "item", key: "SF-1", title: "<script>unsafe()</script>", dueDate: null, state: "TODO" }] });
    const user = userEvent.setup();
    const { container } = renderWithProviders(<OrganizationDashboardPage />, { route: "/?myTasksPage=2" });
    expect(await screen.findByRole("meter", { name: "TODO" })).toHaveAttribute("aria-valuenow", "41");
    expect(screen.getByRole("link", { name: /unsafe/ })).toHaveAttribute("href", "/app/acme/work-items/SF-1");
    expect(container.querySelector("script")).toBeNull();
    await user.click(screen.getByRole("button", { name: "Sonraki sayfa" }));
    await waitFor(() => expect(state.get).toHaveBeenLastCalledWith("/organizations/org/dashboard?myTasksPage=3"));
    expect(screen.getByRole("button", { name: "Sonraki sayfa" })).toBeDisabled();
  });
  it("hides old data when refresh loses access and permits retry", async () => {
    const user = userEvent.setup();
    renderWithProviders(<OrganizationDashboardPage />);
    await screen.findByText("Henüz aktivite yok.");
    state.get.mockRejectedValue(new Error("Private server detail"));
    await user.click(screen.getByRole("button", { name: "Yenile" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Dashboard yüklenemedi");
    expect(screen.queryByText("Henüz aktivite yok.")).not.toBeInTheDocument();
    expect(screen.queryByText("Private server detail")).not.toBeInTheDocument();
    state.get.mockResolvedValue(empty);
    await user.click(screen.getByRole("button", { name: "Yenile" }));
    await screen.findByText("Henüz aktivite yok.");
  });
});
