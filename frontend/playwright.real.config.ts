import { defineConfig, devices } from "@playwright/test";

const url = process.env.SECUREFLOW_E2E_URL;
if (!url || new URL(url).hostname !== "127.0.0.1" || !process.env.SECUREFLOW_E2E_MAIL_KEY) {
  throw new Error("Run the Category=Browser .NET test: it supplies the isolated loopback host.");
}
export default defineConfig({
  testDir: "./real-e2e",
  workers: 1,
  retries: 0,
  timeout: 120_000,
  forbidOnly: Boolean(process.env.CI),
  outputDir: "../artifacts/real-browser",
  use: { ...devices["Desktop Chrome"], baseURL: url, ignoreHTTPSErrors: true, trace: "off" },
});
