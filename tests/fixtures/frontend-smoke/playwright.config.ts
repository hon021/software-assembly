import { defineConfig, devices } from '@playwright/test';
import { resolve } from 'node:path';

process.env.PLAYWRIGHT_JUNIT_OUTPUT_FILE = resolve(__dirname, 'test-results/e2e.xml');

export default defineConfig({
  testDir: './e2e',
  outputDir: './test-results/browser-artifacts',
  retries: 0,
  use: { baseURL: 'http://127.0.0.1:4307', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], channel: 'chromium' } }],
  webServer: {
    command: `"${process.execPath}" "${resolve(__dirname, 'node_modules/@angular/cli/bin/ng.js')}" serve --host 127.0.0.1 --port 4307`,
    url: 'http://127.0.0.1:4307',
    reuseExistingServer: false,
    timeout: 120_000,
  },
});