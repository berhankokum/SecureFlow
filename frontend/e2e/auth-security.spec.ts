import { expect, test } from "@playwright/test";

const csrfCookie = "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict";

test("application routes wait until the CSRF bootstrap completes", async ({ page }) => {
  await page.route("**/api/v1/antiforgery/token", async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 250));
    await route.fulfill({ status: 204, headers: { "set-cookie": csrfCookie } });
  });

  await page.goto("/login");

  await expect(page.getByText("Güvenli oturum hazırlanıyor...")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Giriş yap" })).toBeVisible();
});

test("email confirmation sends the antiforgery header after bootstrap", async ({ page }) => {
  let receivedCsrfHeader: string | undefined;

  await page.route("**/api/v1/antiforgery/token", async (route) => {
    await route.fulfill({ status: 204, headers: { "set-cookie": csrfCookie } });
  });

  await page.route("**/api/v1/auth/confirm-email", async (route) => {
    receivedCsrfHeader = route.request().headers()["x-csrf-token"];
    await route.fulfill({ status: 204 });
  });

  await page.goto("/confirm-email?userId=550e8400-e29b-41d4-a716-446655440000&code=test-code");

  await expect(page.getByText("E-postanız doğrulandı.")).toBeVisible();
  expect(receivedCsrfHeader).toBe("e2e-csrf-token");
});

test("an unauthenticated protected route redirects to login", async ({ page }) => {
  await page.route("**/api/v1/antiforgery/token", async (route) => {
    await route.fulfill({ status: 204, headers: { "set-cookie": csrfCookie } });
  });

  await page.route("**/api/v1/me", async (route) => {
    await route.fulfill({
      status: 401,
      contentType: "application/problem+json",
      body: JSON.stringify({ title: "Unauthorized", status: 401 }),
    });
  });

  await page.goto("/");

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("heading", { name: "Giriş yap" })).toBeVisible();
});
