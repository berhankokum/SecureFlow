import { expect, test } from "@playwright/test";

test("dashboard cards, chart, task paging and safe activity links work on desktop and mobile", async ({ page }) => {
  const org = { id: "org", name: "ACME", slug: "acme", role: "OWNER", permissions: ["member.view"], createdAt: "2026-09-16T10:00:00Z", archivedAt: null };
  let revoked = false;
  await page.route("**/api/v1/**", async route => {
    const url = new URL(route.request().url());
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
    if (url.pathname.endsWith("/antiforgery/token")) return route.fulfill({ status: 204 });
    if (url.pathname === "/api/v1/me") return json({ id: "owner", email: "owner@example.com", emailConfirmed: true });
    if (url.pathname === "/api/v1/organizations") return json([org]);
    if (url.pathname === "/api/v1/notifications") return json({ unreadCount: 0, items: [] });
    if (url.pathname.endsWith("/dashboard")) {
      expect(route.request().method()).toBe("GET");
      if (revoked) return json({}, 404);
      const taskPage = Number(url.searchParams.get("myTasksPage") ?? 1);
      return json({ today: "2026-09-16", activeProjects: 2, myWorkItems: 21, overdue: 1, dueSoon: 3, critical: 2,
        tasksByState: [{ stateId: "todo", name: "TODO", count: 21 }, { stateId: "done", name: "DONE", count: 2 }],
        myTasks: [{ id: "task", key: `SF-${taskPage}`, title: taskPage === 1 ? "İlk iş" : "İkinci sayfadaki iş", dueDate: null, state: "TODO" }],
        myTasksPage: taskPage, myTasksPageSize: 20,
        recentActivity: [{ id: "event", actorId: "owner", type: "WORK_ITEM_CREATED", createdAt: "2026-09-16T10:00:00Z", workItemKey: "SF-1", workItemTitle: "<script>unsafe()</script>" }] });
    }
    return json({}, 404);
  });
  await page.goto("/app/acme/dashboard");
  await expect(page.getByRole("heading", { name: "ACME", exact: true })).toBeVisible();
  await expect(page.getByRole("meter", { name: "TODO" })).toHaveAttribute("aria-valuenow", "21");
  await expect(page.getByRole("link", { name: "SF-1 · İlk iş", exact: true })).toHaveAttribute("href", "/app/acme/work-items/SF-1");
  await expect(page.getByRole("link", { name: /unsafe/ })).toBeVisible();
  await page.getByRole("button", { name: "Sonraki sayfa" }).click();
  await expect(page).toHaveURL(/myTasksPage=2/);
  await expect(page.getByRole("link", { name: /İkinci sayfadaki iş/ })).toBeVisible();
  await page.reload();
  await expect(page.getByText("Sayfa 2", { exact: true })).toBeVisible();
  for (const width of [390, 320, 768, 1280]) {
    await page.setViewportSize({ width, height: 844 });
    await expect(page.getByRole("heading", { name: "İşlerim", exact: true })).toBeVisible();
    await expect.poll(() => page.evaluate(() => ({
      overflow: document.documentElement.scrollWidth > window.innerWidth,
      elements: [...document.querySelectorAll("body *")].filter(element => {
        const rect = element.getBoundingClientRect();
        return rect.width > 0 && (rect.right > window.innerWidth + 1 || rect.left < -1);
      }).map(element => `${element.tagName}.${element.className}`),
    })), { message: `No horizontal overflow at ${width}px` }).toEqual({ overflow: false, elements: [] });
    await expect(page.getByRole("button", { name: "Çıkış yap", exact: true })).toBeVisible();
  }
  revoked = true;
  await page.getByRole("button", { name: "Yenile", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Dashboard yüklenemedi");
  await expect(page.getByRole("meter")).toHaveCount(0);
});
