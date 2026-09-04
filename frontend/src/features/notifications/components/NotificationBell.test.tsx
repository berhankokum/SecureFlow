import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { NotificationBell } from "./NotificationBell";

const markRead = vi.fn();

vi.mock("@/features/organizations/hooks/useCurrentOrganization", () => ({
  useCurrentOrganization: () => ({ slug: "acme" }),
}));
vi.mock("../hooks/useNotifications", () => ({
  useNotifications: () => ({ isLoading: false, isError: false, data: {
    unreadCount: 1,
    items: [{ id: "notification", organizationId: "org", type: "MENTIONED", title: "Bir yorumda bahsedildiniz", message: "SF-1", resourceType: "WORK_ITEM", resourceId: "workitem", isRead: false, createdAt: "2026-09-05T10:00:00Z", readAt: null }],
  } }),
  useMarkNotificationRead: () => ({ mutateAsync: markRead, isPending: false }),
}));

describe("NotificationBell", () => {
  beforeEach(() => markRead.mockReset().mockResolvedValue(undefined));

  it("shows the unread count, latest notification and marks it read", async () => {
    const user = userEvent.setup();
    renderWithProviders(<NotificationBell />);
    expect(screen.getByLabelText("1 okunmamış bildirim")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Bildirimler" }));
    expect(screen.getByText("Bir yorumda bahsedildiniz")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Tümünü gör" })).toHaveAttribute("href", "/app/acme/notifications");
    await user.click(screen.getByRole("button", { name: /Bir yorumda bahsedildiniz/ }));
    expect(markRead).toHaveBeenCalledWith("notification");
  });
});
