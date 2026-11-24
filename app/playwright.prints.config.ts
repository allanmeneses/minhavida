import { defineConfig } from '@playwright/test';

const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5289';
const webUrl = process.env.PLAYWRIGHT_BASE_URL ?? 'http://127.0.0.1:43124';
const connectionString =
  process.env.PRINTS_CONNECTION
  ?? 'Host=127.0.0.1;Port=5432;Database=circulos_prints;Username=circulos;Password=circulos';
const databaseProvider = process.env.Database__Provider ?? 'Postgres';

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
  testMatch: 'prints.spec.ts',
  timeout: 180_000,
  fullyParallel: false,
  workers: 1,
  use: {
    baseURL: webUrl,
    locale: 'pt-BR',
    screenshot: 'only-on-failure',
  },
  reporter: [['list']],
  webServer: [
    {
      command:
        'psql postgresql://circulos:circulos@127.0.0.1:5432/postgres -c "CREATE DATABASE circulos_prints" || true; dotnet run --project ../backend/src/Circulos.Api --urls http://127.0.0.1:5289 --no-launch-profile',
      url: 'http://127.0.0.1:5289/health',
      reuseExistingServer: false,
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
      env: envWith({
        ASPNETCORE_ENVIRONMENT: 'Development',
        Database__Provider: databaseProvider,
        ConnectionStrings__Default: connectionString,
        PRINTS_ISOLATED: '1',
        Logging__LogLevel__Default: 'Warning',
        Logging__LogLevel__Microsoft: 'Warning',
      }),
    },
    {
      command: 'npx expo start --web --port 43124 --non-interactive',
      url: webUrl,
      reuseExistingServer: false,
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
