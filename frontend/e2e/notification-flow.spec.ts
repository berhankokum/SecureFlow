import { expect, test } from "@playwright/test";

test("notification center reads user-owned items and stores the email preference with CSRF", async ({ page }) => {
  const organization = { id: "org", name: "ACME", slug: "acme", role: "OWNER", permissions: ["member.view"], createdAt: "2026-09-05T10:00:00Z", archivedAt: null };
  let isRead = false;
  let emailEnabled = false;
  const mutations: { path: string; csrf?: string }[] = [];

  await page.route("**/api/v1/**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
    if (path.endsWith("/antiforgery/token")) return route.fulfill({ status: 204, headers: { "set-cookie": "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict" } });
    if (method !== "GET") mutations.push({ path, csrf: request.headers()["x-csrf-token"] });
    if (path === "/api/v1/me") return json({ id: "owner", email: "owner@example.com", emailConfirmed: true });
    if (path === "/api/v1/organizations") return json([organization]);
    if (path === "/api/v1/notifications" && method === "GET") return json({ unreadCount: isRead ? 0 : 1, items: [{ id: "notification", organizationId: "org", type: "MENTIONED", title: "Bir yorumda bahsedildiniz", message: "SF-1 yorumunda sizden bahsedildi", resourceType: "WORK_ITEM", resourceId: "item", isRead, createdAt: "2026-09-05T10:00:00Z", readAt: isRead ? "2026-09-05T10:05:00Z" : null }] });
    if (path === "/api/v1/notifications/preferences" && method === "GET") return json({ inAppEnabled: true, emailEnabled, updatedAt: null });
    if (path === "/api/v1/notifications/preferences" && method === "PUT") {
      emailEnabled = request.postDataJSON().emailEnabled;
      return json({ inAppEnabled: true, emailEnabled, updatedAt: "2026-09-05T10:06:00Z" });
    }
    if (path === "/api/v1/notifications/notification/read" && method === "POST") {
      isRead = true;
      return route.fulfill({ status: 204 });
    }
    if (path === "/api/v1/notifications/read-all" && method === "POST") {
      isRead = true;
      return route.fulfill({ status: 204 });
    }
    return json({ title: "Unexpected test request" }, 404);
  });

  await page.goto("/app/acme/notifications");
  await expect(page.getByRole("heading", { name: "Bildirimler", exact: true })).toBeVisible();
  await expect(page.getByLabel("1 okunmamış bildirim")).toBeVisible();
  await page.getByRole("button", { name: /Bir yorumda bahsedildiniz/ }).click();
  await expect(page.getByLabel("1 okunmamış bildirim")).toHaveCount(0);
  const inApp = page.getByRole("checkbox", { name: "Uygulama içi bildirimler" });
  await expect(inApp).toBeChecked();
  await expect(inApp).toBeDisabled();
  await page.getByRole("checkbox", { name: "E-posta bildirimleri" }).check();
  expect(emailEnabled).toBe(true);
  expect(mutations).toEqual([
    { path: "/api/v1/notifications/notification/read", csrf: "e2e-csrf-token" },
    { path: "/api/v1/notifications/preferences", csrf: "e2e-csrf-token" },
  ]);
});
