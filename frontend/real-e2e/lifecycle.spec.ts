import { expect, test, type Page } from "@playwright/test";

// No page.route()/API mocks: all requests reach the isolated HTTPS application.
async function api(page: Page, path: string, method = "GET", body?: unknown, version?: number) {
  const result = await page.evaluate(async ({ path, method, body, version }) => {
    const token = document.cookie.split("; ").find(c => c.startsWith("XSRF-TOKEN="))?.split("=").slice(1).join("=");
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    if (method !== "GET" && token) headers["X-CSRF-TOKEN"] = decodeURIComponent(token);
    if (version !== undefined) headers["If-Match"] = `"${version}"`;
    const response = await fetch(`/api/v1${path}`, { method, headers, credentials: "include",
      body: body === undefined ? undefined : JSON.stringify(body) });
    return { status: response.status, text: await response.text() };
  }, { path, method, body, version });
  expect(result.status, `${method} ${path}: ${result.text}`).toBeGreaterThanOrEqual(200);
  expect(result.status, `${method} ${path}: ${result.text}`).toBeLessThan(300);
  return result.text ? JSON.parse(result.text) : undefined;
}

async function mailLink(page: Page, email: string, contains: string) {
  const response = await page.request.get(`/__test/mail?email=${encodeURIComponent(email)}`,
    { headers: { "X-Test-Key": process.env.SECUREFLOW_E2E_MAIL_KEY! } });
  expect(response.ok()).toBe(true);
  const messages: string[] = await response.json();
  const html = messages.find(message => message.includes(contains));
  expect(html).toBeDefined();
  const link = /href="([^"]+)"/.exec(html!)![1].replaceAll("&amp;", "&");
  const url = new URL(link);
  return url.pathname + url.search;
}

async function registerAndLogin(page: Page, email: string) {
  await page.goto("/register");
  await page.getByLabel("E-posta", { exact: true }).fill(email);
  await page.getByLabel("Şifre", { exact: true }).fill("BrowserTest123!");
  await page.getByRole("button", { name: "Kayıt Ol", exact: true }).click();
  await expect(page.getByText(/Hesabınız oluşturuldu/)).toBeVisible();
  await page.goto(await mailLink(page, email, "/confirm-email"));
  await expect(page.getByText("E-postanız doğrulandı.")).toBeVisible();
  await page.goto("/login");
  await page.getByLabel("E-posta", { exact: true }).fill(email);
  await page.getByLabel("Şifre", { exact: true }).fill("BrowserTest123!");
  await page.getByRole("button", { name: "Giriş Yap", exact: true }).click();
  await expect(page).not.toHaveURL(/\/login$/);
  return api(page, "/me");
}

test("real lifecycle, persisted reload, notification ownership and immediate membership revocation", async ({ page, browser, baseURL }) => {
  expect((await page.request.get("/__test/mail")).status()).toBe(404);
  expect((await page.request.get("/api/v1/me")).status()).toBe(401);
  const suffix = crypto.randomUUID().replaceAll("-", "");
  const owner = await registerAndLogin(page, `owner-${suffix}@example.com`);
  await page.goto("/organizations/new");
  await page.getByLabel("Organization adı").fill(`Browser ${suffix}`);
  await page.getByRole("button", { name: "Organization oluştur" }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  const [organization] = await api(page, "/organizations");
  const memberContext = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
  const outsiderContext = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
  try {
    const memberPage = await memberContext.newPage();
    const memberEmail = `member-${suffix}@example.com`;
    const member = await registerAndLogin(memberPage, memberEmail);
    await api(page, `/organizations/${organization.id}/invitations`, "POST", { email: memberEmail, role: "MEMBER" });
    const invitation = await mailLink(memberPage, memberEmail, "/invitations/");
    const token = invitation.split("/").at(-1)!;
    await api(memberPage, `/invitations/${token}/accept`, "POST");

    const team = await api(page, `/organizations/${organization.id}/teams`, "POST", { name: "Delivery", description: "Real test" });
    await api(page, `/teams/${team.id}/members/${member.id}`, "POST");
    await page.goto(`/app/${organization.slug}/projects`);
    await page.getByRole("button", { name: "Project oluştur", exact: true }).click();
    await page.getByLabel("Project key").fill("LIVE");
    await page.getByLabel("Project adı").fill("Real delivery");
    await page.getByRole("button", { name: "Project oluştur", exact: true }).last().click();
    await page.getByRole("link", { name: /LIVE Real delivery/ }).click();
    const [project] = await api(page, `/organizations/${organization.id}/projects`);
    await api(page, `/projects/${project.id}/members`, "POST", { userId: member.id, role: "CONTRIBUTOR" });
    const label = await api(page, `/projects/${project.id}/labels`, "POST", { name: "Security" });

    await page.getByRole("button", { name: "WorkItem oluştur", exact: true }).click();
    await page.getByLabel("Başlık", { exact: true }).fill("Persisted browser task");
    await page.getByRole("button", { name: "WorkItem oluştur", exact: true }).last().click();
    await page.getByRole("link", { name: /LIVE-1.*Persisted browser task/s }).click();
    const [item] = await api(page, `/projects/${project.id}/work-items`);
    const detailPath = `/work-items/${item.id}`;
    let detail = await api(page, detailPath);
    expect(detail.reporterId).toBe(owner.id);
    await api(page, `${detailPath}/assign`, "POST", { assigneeId: member.id }, detail.version);
    await api(page, `${detailPath}/labels/${label.id}`, "POST");
    const workflow = await api(page, `/projects/${project.id}/workflow`);
    detail = await api(page, detailPath);
    await api(page, `${detailPath}/transition`, "POST", { toStateId: workflow.states.find((s: { name: string }) => s.name === "TODO").id }, detail.version);
    await page.reload();
    await expect(page.getByRole("heading", { name: "Persisted browser task", exact: true })).toBeVisible();
    await page.getByLabel("Yorum", { exact: true }).fill("Persisted browser comment");
    await page.getByRole("button", { name: "Yorum ekle", exact: true }).click();
    await expect(page.getByText("Persisted browser comment", { exact: true })).toBeVisible();
    await page.reload();
    await expect(page.getByText("Persisted browser comment", { exact: true })).toBeVisible();
    detail = await api(page, detailPath);
    expect(detail.assigneeId).toBe(member.id);
    expect(detail.workflowStateName).toBe("TODO");
    expect(detail.labels.map((l: { id: string }) => l.id)).toContain(label.id);
    const notifications = await api(memberPage, "/notifications");
    expect(notifications.items.some((n: { type: string }) => n.type === "WORK_ITEM_ASSIGNED")).toBe(true);
    const ownerNotifications = await api(page, "/notifications");
    expect(ownerNotifications.items.map((n: { id: string }) => n.id)).not.toContain(notifications.items[0].id);

    const outsiderPage = await outsiderContext.newPage();
    await registerAndLogin(outsiderPage, `outsider-${suffix}@example.com`);
    for (const path of [detailPath, `${detailPath}/comments`, `${detailPath}/attachments`, `/projects/${project.id}/labels`, `/teams/${team.id}`]) {
      expect((await outsiderPage.request.get(`/api/v1${path}`)).status()).toBe(404);
    }
    // Same authenticated session must lose access immediately, without re-login.
    expect((await memberPage.request.get(`/api/v1${detailPath}`)).status()).toBe(200);
    await api(page, `/organizations/${organization.id}/members/${member.id}`, "DELETE");
    expect((await memberPage.request.get(`/api/v1${detailPath}`)).status()).toBe(404);
    expect((await page.request.post(`/api/v1${detailPath}/comments`, { data: { content: "No CSRF" } })).status()).toBe(400);
  } finally {
    await memberContext.close();
    await outsiderContext.close();
  }
});
