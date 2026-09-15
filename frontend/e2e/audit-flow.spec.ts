import { expect, test } from "@playwright/test";

for (const allowed of [true, false]) {
  test(`audit route ${allowed ? "filters and restores read-only history" : "denies members without requesting history"}`, async ({ page }) => {
    let reads = 0;
    const organization = { id: "org", name: "ACME", slug: "acme", role: allowed ? "ADMIN" : "MEMBER", permissions: allowed ? ["audit.view"] : [], createdAt: "2026-09-15T10:00:00Z", archivedAt: null };
    await page.route("**/api/v1/**", async route => {
      const url = new URL(route.request().url());
      const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
      if (url.pathname.endsWith("/antiforgery/token")) return route.fulfill({ status: 204 });
      if (url.pathname === "/api/v1/me") return json({ id: "owner", email: "owner@example.com", emailConfirmed: true });
      if (url.pathname === "/api/v1/organizations") return json([organization]);
      if (url.pathname === "/api/v1/notifications") return json({ unreadCount: 0, items: [] });
      if (url.pathname.endsWith("/audit-logs")) {
        reads++;
        expect(route.request().method()).toBe("GET");
        return json(url.searchParams.get("action") === "PROJECT_ARCHIVED" ? [] : [{ id: "log", organizationId: "org", actorUserId: "owner", action: "WORK_ITEM_UPDATED", resourceType: "WorkItem", resourceId: "item", timestamp: "2026-09-15T10:00:00Z", ipAddress: "192.0.2.1", userAgent: "<script>unsafe()</script>", metadata: {} }]);
      }
      return json({}, 404);
    });
    await page.goto("/app/acme/audit");
    if (!allowed) {
      await expect(page.getByRole("heading", { name: "Bu alanı görüntüleme yetkiniz yok" })).toBeVisible();
      await expect(page.getByRole("link", { name: "Audit kayıtları" })).toHaveCount(0);
      expect(reads).toBe(0);
      return;
    }
    await expect(page.getByRole("table")).toBeVisible();
    await page.getByText("Detay", { exact: true }).click();
    await expect(page.getByText("User-Agent: <script>unsafe()</script>", { exact: true })).toBeVisible();
    await page.getByRole("combobox", { name: /^İşlem/ }).selectOption("PROJECT_ARCHIVED");
    await page.getByRole("button", { name: "Filtreleri uygula" }).click();
    await expect(page).toHaveURL(/action=PROJECT_ARCHIVED/);
    await expect(page.getByText(/audit kaydı yok/)).toBeVisible();
    await page.reload();
    await expect(page.getByRole("combobox", { name: /^İşlem/ })).toHaveValue("PROJECT_ARCHIVED");
    await expect(page.getByText(/audit kaydı yok/)).toBeVisible();
  });
}
