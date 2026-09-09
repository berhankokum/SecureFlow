import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { NotificationsPage } from "./NotificationsPage";

const markRead = vi.fn(), markAll = vi.fn(), updatePreference = vi.fn();

vi.mock("../hooks/useNotifications", () => ({
  useNotifications: () => ({ isLoading: false, isError: false, data: {
    unreadCount: 1,
    items: [{ id: "notification", organizationId: "org", type: "WORK_ITEM_ASSIGNED", title: "WorkItem atandı", message: "SF-1 size atandı", resourceType: "WORK_ITEM", resourceId: "workitem", isRead: false, createdAt: "2026-09-05T10:00:00Z", readAt: null }],
  } }),
  useNotificationPreference: () => ({ isLoading: false, data: { inAppEnabled: true, emailEnabled: false, updatedAt: null } }),
  useMarkNotificationRead: () => ({ mutateAsync: markRead, isPending: false }),
  useMarkAllNotificationsRead: () => ({ mutateAsync: markAll, isPending: false }),
  useUpdateNotificationPreference: () => ({ mutateAsync: updatePreference, isPending: false }),
}));

describe("NotificationsPage", () => {
  beforeEach(() => {
    for (const mock of [markRead, markAll, updatePreference]) mock.mockReset().mockResolvedValue(undefined);
  });

  it("keeps in-app delivery mandatory and controls read state and email preference", async () => {
    const user = userEvent.setup();
    renderWithProviders(<NotificationsPage />);
    const inApp = screen.getByRole("checkbox", { name: "Uygulama içi bildirimler" });
    expect(inApp).toBeChecked();
    expect(inApp).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Tümünü okundu işaretle" }));
    expect(markAll).toHaveBeenCalledOnce();
    await user.click(screen.getByRole("button", { name: /WorkItem atandı/ }));
    expect(markRead).toHaveBeenCalledWith("notification");
    await user.click(screen.getByRole("checkbox", { name: "E-posta bildirimleri" }));
    expect(updatePreference).toHaveBeenCalledWith(true);
    expect(screen.getByText(/yeni uygulama içi bildirimleriniz/)).toBeInTheDocument();
  });
});
