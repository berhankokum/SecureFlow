import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./security-e2e",
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  use: { baseURL: "http://localhost:4174", ...devices["Desktop Chrome"] },
  webServer: {
    command: "npm run preview -- --host 127.0.0.1 --port 4174 --strictPort",
    url: "http://localhost:4174/login",
    reuseExistingServer: false,
  },
});
