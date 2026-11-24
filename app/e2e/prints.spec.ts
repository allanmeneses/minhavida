import { expect, test, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import { reloadAppRoute, screenshotSteps } from './printCapture';

const artifacts = '/opt/cursor/artifacts/circulos-telas-prints';
const WEB_NARROW = 768;

async function skipTourIfNeeded(page: Page): Promise<void> {
  const skip = page.getByRole('button', { name: 'Pular tour' });
  const home = page.getByText('O que pede atenção hoje');
  await expect(skip.or(home)).toBeVisible({ timeout: 20_000 });
  if (await skip.isVisible()) {
    await skip.click();
  }
  await expect(home).toBeVisible({ timeout: 20_000 });
}

async function waitForAppChrome(page: Page): Promise<void> {
  const width = page.viewportSize()?.width ?? 1280;
  if (width < WEB_NARROW) {
    await expect(page.getByRole('button', { name: 'Mais', exact: true })).toBeVisible();
    return;
  }
  await expect(page.getByRole('button', { name: 'Tipos', exact: true })).toBeVisible();
}

async function openReloaded(
  page: Page,
  go: () => Promise<void>,
  ready: () => Promise<void>,
): Promise<void> {
  await go();
  await reloadAppRoute(page);
  await waitForAppChrome(page);
  await ready();
}

test('prints isolados recarregam a rota e rolam em passos', async ({ page }) => {
  test.setTimeout(240_000);
  await mkdir(artifacts, { recursive: true });
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto('/(auth)/login');
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible();
  await screenshotSteps(page, `${artifacts}/01-login-desktop.png`);

  const email = `prints-${Date.now()}@exemplo.com`;
  await page.goto('/(auth)/register');
  await page.getByLabel('Nome', { exact: true }).fill('Marina Alves');
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill('senha-segura-1');
  await page.getByRole('button', { name: 'Criar conta' }).click();
  await skipTourIfNeeded(page);
  await waitForAppChrome(page);
  await screenshotSteps(page, `${artifacts}/04-hoje-desktop.png`);

  await openReloaded(
    page,
    () => page.getByRole('button', { name: 'Círculos' }).click(),
    async () => {
      await expect(page.getByRole('button', { name: 'Abrir Família' })).toHaveCount(1);
      await expect(page.getByText(/Treinos \d{10,}/)).toHaveCount(0);
    },
  );
  await screenshotSteps(page, `${artifacts}/20-circulos-desktop.png`);

  await openReloaded(
    page,
    () => page.getByRole('button', { name: 'Tipos', exact: true }).first().click(),
    () => expect(page.getByText('Meus tipos')).toBeVisible(),
  );
  await screenshotSteps(page, `${artifacts}/11-meus-tipos-desktop.png`);

  await openReloaded(
    page,
    () => page.getByRole('button', { name: 'Lembretes' }).first().click(),
    () => expect(page.getByText('Círculo cíclico')).toBeVisible(),
  );
  await screenshotSteps(page, `${artifacts}/28-lembretes-desktop.png`);

  await openReloaded(
    page,
    () => page.getByRole('button', { name: 'Ajustes', exact: true }).first().click(),
    () => expect(page.getByRole('button', { name: 'Ver preços' })).toBeVisible(),
  );
  await screenshotSteps(page, `${artifacts}/35-ajustes-desktop.png`);
});
