import { expect, test } from "@playwright/test";

test("real production edge serves SPA with CSP, secure cookies and no inline script execution", async ({ page }) => {
  const response = await page.goto("/login");
  await expect(page.getByRole("button", { name: "Giriş Yap", exact: true })).toBeVisible();
  expect(response?.headers()["content-security-policy"]).toContain("frame-ancestors 'none'");
  expect(response?.headers()["strict-transport-security"]).toBe("max-age=15552000");
  expect(response?.headers()["x-frame-options"]).toBe("DENY");
  expect(response?.headers()["x-content-type-options"]).toBe("nosniff");
  const cookies = await page.context().cookies();
  expect(cookies.some(cookie => cookie.name === "XSRF-TOKEN" && cookie.secure && cookie.sameSite === "Strict")).toBe(true);
  const script = page.locator('script[src]').first();
  const asset = await page.request.get((await script.getAttribute("src"))!);
  expect(asset.headers()["cache-control"]).toContain("immutable");
  await page.evaluate(() => {
    const script = document.createElement("script");
    script.textContent = "document.documentElement.dataset.injected = 'true'";
    document.head.append(script);
  });
  await expect(page.locator("html")).not.toHaveAttribute("data-injected");
  expect((await page.request.get("/assets/does-not-exist.js")).status()).toBe(404);
  const csrf = cookies.find(cookie => cookie.name === "XSRF-TOKEN")!.value;
  const oversized = await page.request.post("/api/v1/not-a-real-upload", {
    headers: { "X-CSRF-TOKEN": csrf }, data: Buffer.alloc(12 * 1024 * 1024),
  });
  expect(oversized.status()).toBe(413);
});
