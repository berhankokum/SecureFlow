import { expect, test } from "@playwright/test";

test("built SPA starts under CSP and blocks injected inline script", async ({ page }) => {
  const violations: string[] = [];
  page.on("console", message => {
    if (message.type() === "error" && /content security policy/i.test(message.text())) violations.push(message.text());
  });
  await page.route("**/api/v1/me", route => route.fulfill({ status: 401, body: "" }));
  await page.route("**/api/v1/antiforgery/token", route => route.fulfill({ status: 204,
    headers: { "set-cookie": "XSRF-TOKEN=csrf-fixture; Path=/; Secure; SameSite=Strict" } }));
  const response = await page.goto("/login");
  await expect(page.getByRole("button", { name: "Giriş Yap", exact: true })).toBeVisible();
  expect(violations).toEqual([]);
  expect(response?.headers()["content-security-policy"]).toContain("frame-ancestors 'none'");
  expect(response?.headers()["x-content-type-options"]).toBe("nosniff");
  await expect(page.locator('meta[http-equiv="Content-Security-Policy"]')).toHaveAttribute("content", /script-src 'self'/);
  await page.evaluate(() => {
    const script = document.createElement("script");
    script.textContent = "document.documentElement.dataset.xss = 'executed'";
    document.head.append(script);
  });
  await expect(page.locator("html")).not.toHaveAttribute("data-xss", "executed");
  expect(violations.length).toBeGreaterThan(0);
});
