import { expect, test, type Page } from '@playwright/test';
import { existsSync } from 'node:fs';
import { mkdir } from 'node:fs/promises';

const artifacts =
  process.env.E2E_ARTIFACTS ??
  (existsSync('/opt/cursor') ? '/opt/cursor/artifacts' : 'test-results/e2e');

async function signUp(page: Page, suffix = ''): Promise<string> {
  const email = `e2e-${Date.now()}${suffix}@exemplo.com`;
  await page.goto('/(auth)/register');
  await page.getByLabel('Nome').fill('Pessoa E2E');
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill('senha-segura-1');
  await page.getByRole('button', { name: 'Criar conta' }).click();
  await expect(page.getByText('O que pede atencao hoje')).toBeVisible({ timeout: 20_000 });
  return email;
}

test.beforeAll(async () => {
  await mkdir(artifacts, { recursive: true });
});

test('cadastro abre o painel do dia, o perfil solo e um circulo', async ({ page }) => {
  await signUp(page);
  await page.screenshot({ path: `${artifacts}/hoje.png`, fullPage: true });

  await page.getByRole('button', { name: 'Perfil' }).click();
  await expect(page.getByText('Vacinas', { exact: true })).toBeVisible();
  await expect(page.getByText('Diario', { exact: true })).toBeVisible();
  await page.getByLabel('Nome').fill('Pessoa E2E editada');
  await page.screenshot({ path: `${artifacts}/perfil.png`, fullPage: true });

  await page.getByText('Vacinas', { exact: true }).click();
  await page.getByLabel('Titulo').fill('Hepatite B');
  await page.getByLabel('Quando').fill('2024-03-10');
  await page.getByLabel('Onde').fill('UBS Centro');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Hepatite B')).toBeVisible();

  await page.getByRole('button', { name: 'Circulos' }).click();
  await page.getByLabel('Nome').fill('Familia');
  await page.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(page.getByText('Familia').first()).toBeVisible();
  await page.screenshot({ path: `${artifacts}/circulo.png`, fullPage: true });
});

test('atalho de um toque dispara sem confirmacao', async ({ page }) => {
  await signUp(page, '-atalho');
  await page.getByLabel('Nome do atalho').fill('Tomei o remedio');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  const shortcut = page.getByRole('button', { name: 'Tomei o remedio' });
  await expect(shortcut).toBeVisible({ timeout: 15_000 });
  await shortcut.click();
  await expect(page.getByText('Tomei o remedio').first()).toBeVisible();
  await page.screenshot({ path: `${artifacts}/atalho.png`, fullPage: true });
});

test('lembrete de aniversario entra na lista', async ({ page }) => {
  await signUp(page, '-lembrete');
  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await page.getByLabel('Titulo').fill('Aniversario da Ana');
  await page.getByRole('button', { name: 'Geral' }).click();
  await page.getByLabel('Quem').fill('Ana');
  await page.getByLabel('Ideias de presente').fill('Livro, planta');
  await page.getByRole('button', { name: 'Criar lembrete' }).click();
  await expect(page.getByText('Aniversario da Ana')).toBeVisible();
  await page.screenshot({ path: `${artifacts}/lembrete.png`, fullPage: true });
});

test('esqueci a senha avisa que o email saiu', async ({ page }) => {
  const email = await signUp(page, '-senha');
  await page.getByRole('button', { name: 'Sair' }).click();
  await page.goto('/(auth)/forgot');
  await page.getByLabel('E-mail').fill(email);
  await page.getByRole('button', { name: 'Enviar codigo' }).click();
  await expect(page.getByText('Se existir conta neste e-mail, enviamos o codigo.')).toBeVisible();
  await page.screenshot({ path: `${artifacts}/esqueci-senha.png`, fullPage: true });
});

test('modo escuro troca a paleta na hora', async ({ page }) => {
  await signUp(page, '-tema');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByRole('button', { name: 'Escuro' }).click();
  await expect(page.getByText('Agora: dark')).toBeVisible();
  await page.screenshot({ path: `${artifacts}/tema-escuro.png`, fullPage: true });
});
