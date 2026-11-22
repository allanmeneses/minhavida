import { defineConfig } from '@playwright/test';

const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
const webUrl = process.env.PLAYWRIGHT_BASE_URL ?? 'http://127.0.0.1:43123';
const connectionString =
  process.env.ConnectionStrings__Default ??
  process.env.CONNECTION_STRING ??
  'Data Source=/tmp/circulos-e2e.db';
const databaseProvider = process.env.Database__Provider ?? 'Sqlite';

function envWith(extra: Record<string, string>): Record<string, string> {
  const env: Record<string, string> = {};
  for (const [key, value] of Object.entries(process.env)) {
    if (value !== undefined) {
      env[key] = value;
    }
  }
  return { ...env, ...extra };
}

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  fullyParallel: false,
  workers: 1,
  use: {
    baseURL: webUrl,
    screenshot: 'only-on-failure',
  },
  reporter: [['list']],
  webServer: [
    {
      command: 'dotnet run --project ../backend/src/Circulos.Api --urls http://127.0.0.1:5288 --no-launch-profile',
      url: 'http://127.0.0.1:5288/health',
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      stdout: 'pipe',
      stderr: 'pipe',
      env: envWith({
        ASPNETCORE_ENVIRONMENT: 'Development',
        Database__Provider: databaseProvider,
        ConnectionStrings__Default: connectionString,
      }),
    },
    {
      command: 'npx expo start --web --port 43123 --non-interactive',
      url: webUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
      env: envWith({
        CI: '1',
        EXPO_NO_TELEMETRY: '1',
        EXPO_PUBLIC_API_URL: apiUrl,
      }),
    },
  ],
});
