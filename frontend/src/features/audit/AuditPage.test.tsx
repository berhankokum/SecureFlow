import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { AuditPage } from "./AuditPage";

const state = vi.hoisted(() => ({ get: vi.fn(), allowed: true }));
vi.mock("@/lib/api/apiClient", () => ({ apiClient: { get: state.get } }));
vi.mock("@/features/organizations/hooks/useCurrentOrganization", () => ({
  useCurrentOrganization: () => ({ id: "org", name: "ACME", permissions: state.allowed ? ["audit.view"] : [] }),
}));

describe("AuditPage", () => {
  beforeEach(() => { state.allowed = true; state.get.mockReset().mockResolvedValue([]); });

  it("does not fetch when audit permission is absent", () => {
    state.allowed = false;
    renderWithProviders(<AuditPage />);
    expect(screen.getByRole("alert")).toHaveTextContent("yetkiniz yok");
    expect(state.get).not.toHaveBeenCalled();
  });

  it("restores URL filters, paginates, and resets page when applying filters", async () => {
    const user = userEvent.setup();
    state.get.mockResolvedValue(Array.from({ length: 10 }, (_, id) => ({ id: String(id), organizationId: "org", actorUserId: null, action: "WORK_ITEM_UPDATED", resourceType: "WorkItem", resourceId: "item", timestamp: "2026-09-15T12:00:00Z", ipAddress: null, userAgent: "<script>unsafe()</script>", metadata: {} })));
    const { container } = renderWithProviders(<AuditPage />, { route: "/?action=WORK_ITEM_UPDATED&pageSize=10" });
    await screen.findByRole("table");
    expect(screen.getByLabelText("İşlem")).toHaveValue("WORK_ITEM_UPDATED");
    expect(container.querySelector("script")).toBeNull();
    expect(screen.queryByRole("option", { name: "LOGIN_FAILURE" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Sonraki sayfa" }));
    await waitFor(() => expect(state.get).toHaveBeenLastCalledWith(expect.stringContaining("page=2")));
    await user.selectOptions(screen.getByLabelText("İşlem"), "MEMBER_INVITED");
    await user.click(screen.getByRole("button", { name: "Filtreleri uygula" }));
    await waitFor(() => expect(state.get).toHaveBeenLastCalledWith(expect.stringContaining("action=MEMBER_INVITED")));
    expect(state.get.mock.lastCall![0]).not.toContain("page=2");
    await user.click(screen.getByRole("button", { name: "Temizle" }));
    await waitFor(() => expect(state.get).toHaveBeenLastCalledWith("/organizations/org/audit-logs?"));
  });

  it("rejects reversed dates and displays an empty state", async () => {
    renderWithProviders(<AuditPage />);
    await screen.findByText(/audit kaydı yok/);
    fireEvent.change(screen.getByLabelText("Başlangıç (UTC)"), { target: { value: "2026-09-16" } });
    fireEvent.change(screen.getByLabelText("Bitiş (UTC)"), { target: { value: "2026-09-15" } });
    fireEvent.submit(screen.getByRole("form", { name: "Audit filtreleri" }));
    expect(screen.getByRole("alert")).toHaveTextContent("Başlangıç tarihi bitişten sonra olamaz");
    expect(state.get).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "Sonraki sayfa" })).toBeDisabled();
  });

  it("shows a safe error message without rendering response content", async () => {
    state.get.mockRejectedValue(new Error("Sensitive server detail"));
    renderWithProviders(<AuditPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent("Audit kayıtları yüklenemedi");
    expect(screen.queryByText("Sensitive server detail")).not.toBeInTheDocument();
  });
});
