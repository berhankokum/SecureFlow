import { expect, test } from "@playwright/test";

const csrfCookie = "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict";

const currentUser = {
  id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  email: "owner@example.com",
  emailConfirmed: true,
};

test.beforeEach(async ({ page }) => {
  await page.route("**/api/v1/antiforgery/token", async (route) => {
    await route.fulfill({ status: 204, headers: { "set-cookie": csrfCookie } });
  });

  await page.route("**/api/v1/me", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(currentUser) });
  });
});

test("creates an organization and enters its isolated tenant route", async ({ page }) => {
  let organizations: object[] = [];
  let receivedCsrfHeader: string | undefined;

  await page.route("**/api/v1/organizations", async (route) => {
    if (route.request().method() === "POST") {
      receivedCsrfHeader = route.request().headers()["x-csrf-token"];
      const organization = {
        id: "11111111-1111-1111-1111-111111111111",
        name: "ACME Software",
        slug: "acme-software",
        logoUrl: null,
        role: "OWNER",
        createdAt: "2026-08-29T00:00:00Z",
        archivedAt: null,
      };
      organizations = [organization];
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(organization) });
      return;
    }

    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(organizations) });
  });

  await page.goto("/organizations/new");
  await page.getByLabel("Organization adı").fill("ACME Software");
  await page.getByRole("button", { name: "Organization oluştur" }).click();

  await expect(page).toHaveURL(/\/app\/acme-software\/dashboard$/);
  await expect(page.getByRole("heading", { name: "ACME Software" })).toBeVisible();
  expect(receivedCsrfHeader).toBe("e2e-csrf-token");
});

test("switches organization context through the URL", async ({ page }) => {
  const organizations = [
    {
      id: "11111111-1111-1111-1111-111111111111",
      name: "ACME",
      slug: "acme",
      logoUrl: null,
      role: "OWNER",
      createdAt: "2026-08-29T00:00:00Z",
      archivedAt: null,
    },
    {
      id: "22222222-2222-2222-2222-222222222222",
      name: "CyberTech",
      slug: "cybertech",
      logoUrl: null,
      role: "MEMBER",
      createdAt: "2026-08-29T00:00:00Z",
      archivedAt: null,
    },
  ];

  await page.route("**/api/v1/organizations", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(organizations) });
  });

  await page.goto("/app/acme/dashboard");
  await page.getByLabel("Organization").selectOption("cybertech");

  await expect(page).toHaveURL(/\/app\/cybertech\/dashboard$/);
  await expect(page.getByRole("heading", { name: "CyberTech" })).toBeVisible();
});

test("does not render a tenant absent from the users membership list", async ({ page }) => {
  await page.route("**/api/v1/organizations", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
  });

  await page.goto("/app/other-tenant/dashboard");

  await expect(page.getByText("Bu organization bulunamadı veya erişim yetkiniz yok.")).toBeVisible();
  await expect(page.getByRole("heading", { name: "other-tenant" })).not.toBeVisible();
});
