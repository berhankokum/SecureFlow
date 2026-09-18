import { expect, test } from "@playwright/test";

test("login rate limiting displays safe retry guidance and does not automatically retry", async ({ page }) => {
  let attempts = 0;
  await page.route("**/api/v1/antiforgery/token", route => route.fulfill({ status: 204,
    headers: { "set-cookie": "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict" } }));
  await page.route("**/api/v1/me", route => route.fulfill({ status: 401, body: "" }));
  await page.route("**/api/v1/auth/login", route => {
    attempts++;
    return route.fulfill({ status: 429, headers: { "Retry-After": "60" },
      contentType: "application/problem+json", body: JSON.stringify({ title: "Çok fazla istek gönderdiniz." }) });
  });
  await page.goto("/login");
  await page.getByLabel("E-posta", { exact: true }).fill("test@example.com");
  await page.getByLabel("Şifre", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Giriş Yap", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("60 saniye sonra tekrar deneyin.");
  expect(attempts).toBe(1);
});
