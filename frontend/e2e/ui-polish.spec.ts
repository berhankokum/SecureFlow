import { expect, test, type Page } from "@playwright/test";
import { resolve } from "node:path";

const organization = { id: "org", name: "ACME", slug: "acme", role: "OWNER",
  permissions: ["member.view", "member.invite", "member.remove", "member.role.change"],
  createdAt: "2026-09-01T00:00:00Z", archivedAt: null };

async function arrange(page: Page) {
  await page.route("**/api/v1/**", async route => {
    const path = new URL(route.request().url()).pathname;
    const json = (value: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(value) });
    if (path.endsWith("/antiforgery/token")) return route.fulfill({ status: 204, headers: {
      "set-cookie": "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict",
    } });
    if (path === "/api/v1/me") return json({ id: "owner", email: "owner@example.com", emailConfirmed: true });
    if (path === "/api/v1/organizations") return json([organization]);
    if (path === "/api/v1/notifications") return json({ unreadCount: 0, items: [] });
    if (path.endsWith("/members")) return json([{ id: "member", userId: "owner", email: "owner@example.com", role: "OWNER", joinedAt: organization.createdAt }]);
    if (path.endsWith("/invitations") || path.endsWith("/projects")) return json([]);
    return json({}, 404);
  });
}

test("invitation modal contains focus, validates, closes with Escape and restores its trigger", async ({ page }) => {
  await arrange(page);
  await page.goto("/app/acme/members");
  const trigger = page.getByRole("button", { name: "Üye davet et" });
  await trigger.click();
  const dialog = page.getByRole("dialog", { name: "Üye davet et" });
  await expect(dialog).toBeVisible();
  await expect(page.getByLabel("E-posta", { exact: true })).toBeFocused();
  for (let i = 0; i < 12; i++) {
    await page.keyboard.press(i < 6 ? "Tab" : "Shift+Tab");
    await expect.poll(() => dialog.evaluate(element => element.contains(document.activeElement))).toBe(true);
  }
  await dialog.getByRole("button", { name: "Davet gönder" }).click();
  await expect(page.getByLabel("E-posta", { exact: true })).toHaveAttribute("aria-invalid", "true");
  await expect(dialog.getByRole("alert")).toBeVisible();
  for (const width of [320, 390, 768, 1280]) {
    await page.setViewportSize({ width, height: 568 });
    await expect.poll(() => dialog.evaluate(element => {
      const bounds = element.getBoundingClientRect();
      return bounds.left >= 0 && bounds.right <= innerWidth && bounds.bottom <= innerHeight
        && element.scrollWidth <= element.clientWidth;
    })).toBe(true);
    if (width === 320 || width === 1280) await page.screenshot({ path: resolve(import.meta.dirname, `../../artifacts/ui-polish/invitation-${width}.png`) });
  }
  await page.keyboard.press("Escape");
  await expect(dialog).toHaveCount(0);
  await expect(trigger).toBeFocused();
  await trigger.press("Enter");
  await dialog.getByRole("button", { name: "Vazgeç" }).click();
  await expect(trigger).toBeFocused();
});

test("notification modal fits mobile, exposes empty state and returns keyboard focus", async ({ page }) => {
  await arrange(page);
  await page.setViewportSize({ width: 320, height: 568 });
  await page.goto("/app/acme/members");
  const trigger = page.getByRole("button", { name: "Bildirimler", exact: true });
  await trigger.click();
  const dialog = page.getByRole("dialog", { name: "Bildirimler", exact: true });
  await expect(dialog.getByText("Henüz bildirim yok.")).toBeVisible();
  await expect.poll(() => dialog.evaluate(element => {
    const bounds = element.getBoundingClientRect();
    return bounds.left >= 0 && bounds.right <= innerWidth && element.scrollWidth <= element.clientWidth;
  })).toBe(true);
  await page.keyboard.press("Escape");
  await expect(trigger).toBeFocused();
});

test("projects announce loading and error, retry into empty state, and respect reduced motion", async ({ page }) => {
  await arrange(page);
  await page.emulateMedia({ reducedMotion: "reduce" });
  let fail = true;
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route("**/api/v1/organizations/org/projects", async route => {
    await gate;
    await route.fulfill({ status: fail ? 503 : 200, contentType: "application/json", body: fail ? '{}' : '[]' });
  });
  await page.goto("/app/acme/projects");
  await expect(page.getByRole("status")).toContainText("Projects yükleniyor");
  await expect(page.getByRole("status").locator('[aria-hidden="true"]')).toHaveCSS("animation-name", "none");
  release();
  await expect(page.getByRole("alert")).toHaveText("Projects yüklenemedi.");
  fail = false;
  await page.getByRole("button", { name: "Yeniden dene" }).click();
  await expect(page.getByText("Henüz erişebildiğiniz bir Project yok.")).toBeVisible();
  await expect(page.getByRole("alert")).toHaveCount(0);
  await expect(page.getByRole("status")).toHaveCount(0);
});

test("skip link bypasses navigation with a visible keyboard focus", async ({ page }) => {
  await arrange(page);
  await page.goto("/app/acme/projects");
  await expect(page.getByRole("heading", { name: "Projects", exact: true })).toBeVisible();
  await page.keyboard.press("Tab");
  const skip = page.getByRole("link", { name: "Ana içeriğe geç" });
  await expect(skip).toBeFocused();
  await expect(skip).toBeVisible();
  await expect(skip).toHaveCSS("outline-style", "solid");
  await skip.press("Enter");
  await expect(page.getByRole("main")).toBeFocused();
});

test("canceling a destructive confirmation sends no removal request", async ({ page }) => {
  await arrange(page);
  let removals = 0;
  page.on("request", request => { if (request.method() === "DELETE") removals++; });
  await page.goto("/app/acme/members");
  page.once("dialog", async dialog => { expect(dialog.type()).toBe("confirm"); await dialog.dismiss(); });
  await page.getByRole("button", { name: "Ayrıl", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Üyeler", exact: true })).toBeVisible();
  expect(removals).toBe(0);
});

test("notification read failure stays visible and failed preferences cannot be edited", async ({ page }) => {
  await arrange(page);
  const uncaught: string[] = [];
  let readRequests = 0;
  page.on("pageerror", error => uncaught.push(error.message));
  await page.route("**/api/v1/notifications", route => route.fulfill({ json: { unreadCount: 1,
    items: [{ id: "n1", title: "Yeni görev", message: "SF-1", isRead: false }] } }));
  await page.route("**/api/v1/notifications/n1/read", route => {
    readRequests++;
    return route.fulfill({ status: 503, json: {} });
  });
  await page.route("**/api/v1/notifications/preferences", route => route.fulfill({ status: 503, json: {} }));
  await page.goto("/app/acme/members");
  await page.getByRole("button", { name: "Bildirimler", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: /Yeni görev/ }).click();
  await expect(dialog.getByRole("alert")).toContainText("işaretlenemedi");
  await dialog.getByRole("link", { name: "Tümünü gör" }).click();
  await expect(page.getByRole("alert")).toHaveText("Bildirim tercihleri yüklenemedi.");
  await expect(page.getByRole("checkbox", { name: "E-posta bildirimleri" })).toBeDisabled();
  expect(uncaught).toEqual([]);
  expect(readRequests).toBe(1);
});

test("revoking an invitation requires confirmation before the mutation", async ({ page }) => {
  await arrange(page);
  let revoked = false;
  await page.route("**/api/v1/organizations/org/invitations", route => route.fulfill({ json: revoked ? [] : [{
    id: "invitation", email: "invited@example.com", role: "MEMBER", expiresAt: organization.createdAt,
  }] }));
  await page.route("**/api/v1/organizations/org/invitations/invitation", route => {
    expect(route.request().method()).toBe("DELETE");
    revoked = true;
    return route.fulfill({ status: 204 });
  });
  await page.goto("/app/acme/members");
  page.once("dialog", dialog => dialog.dismiss());
  await page.getByRole("button", { name: "Revoke et" }).click();
  expect(revoked).toBe(false);
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "Revoke et" }).click();
  await expect(page.getByText("Bekleyen davet yok.")).toBeVisible();
  expect(revoked).toBe(true);
});
