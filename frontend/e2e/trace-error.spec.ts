import { expect, test } from "@playwright/test";

test("login errors display the server trace reference without exposing arbitrary identifiers", async ({ page }) => {
  const trace = "0123456789abcdef0123456789abcdef";
  await page.route("**/api/v1/antiforgery/token", route => route.fulfill({ status: 204,
    headers: { "set-cookie": "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict" } }));
  await page.route("**/api/v1/me", route => route.fulfill({ status: 401, body: "" }));
  await page.route("**/api/v1/auth/login", route => route.fulfill({ status: 401,
    headers: { "X-Trace-Id": trace }, contentType: "application/problem+json", body: JSON.stringify({ title: "Giriş başarısız." }) }));
  await page.goto("/login");
  await page.getByLabel("E-posta", { exact: true }).fill("test@example.com");
  await page.getByLabel("Şifre", { exact: true }).fill("Password123!");
  await page.getByRole("button", { name: "Giriş Yap", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText(`Referans: ${trace}`);
});
