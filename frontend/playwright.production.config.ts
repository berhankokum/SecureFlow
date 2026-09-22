import { defineConfig } from "@playwright/test";

const baseURL = process.env.PRODUCTION_SMOKE_URL;
if (!baseURL || !/^https:\/\/localhost:\d+\/?$/.test(baseURL)) {
  throw new Error("Run through tools/production-smoke; only disposable localhost HTTPS is supported.");
}
export default defineConfig({
  testDir: "./production-e2e",
  outputDir: "../artifacts/production-browser",
  workers: 1,
  retries: 0,
  use: { baseURL, ignoreHTTPSErrors: true }, // disposable self-signed localhost certificate only
});
