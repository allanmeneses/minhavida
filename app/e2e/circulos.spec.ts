import { expect, test, type Page } from '@playwright/test';
import { existsSync } from 'node:fs';
import { mkdir } from 'node:fs/promises';

const artifacts =
  process.env.E2E_ARTIFACTS ??
  (existsSync('/opt/cursor') ? '/opt/cursor/artifacts' : 'test-results/e2e');

async function signUp(page: Page, suffix = ''): Promise<string> {
  const email = `e2e-${Date.now()}${suffix}@exemplo.com`;
  await page.goto('/(auth)/register');
  await page.getByLabel('Nome', { exact: true }).fill('Pessoa E2E');
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
  await page.getByLabel('Nome', { exact: true }).fill('Pessoa E2E editada');
  await page.screenshot({ path: `${artifacts}/perfil.png`, fullPage: true });

  await page.getByText('Vacinas', { exact: true }).click();
  await page.getByLabel('Titulo').fill('Hepatite B');
  await page.getByLabel('Quando').fill('2024-03-10');
  await page.getByLabel('Onde').fill('UBS Centro');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Hepatite B')).toBeVisible();

  await page.getByRole('button', { name: 'Circulos' }).click();
  await page.getByLabel('Nome', { exact: true }).fill('Familia');
  await page.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(page.getByText('Familia').first()).toBeVisible();
  await page.screenshot({ path: `${artifacts}/circulo.png`, fullPage: true });
});

test('atalho de um toque dispara sem confirmacao', async ({ page }) => {
  await signUp(page, '-atalho');
  await page.getByLabel('Nome do atalho').fill('Tomei o remedio');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  const shortcut = page.getByRole('button', { name: 'Tomei o remedio', exact: true });
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
  await expect(page.getByText('Aniversario da Ana', { exact: true })).toBeVisible();
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

test('entrada guarda numero e o log do circulo mostra data', async ({ page }) => {
  await signUp(page, '-numero');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByText('Diario', { exact: true }).click();
  await page.getByLabel('Titulo').fill('Corrida');
  await page.getByLabel('Numero').fill('200');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Numero: 200')).toBeVisible();

  await page.getByRole('button', { name: 'Circulos' }).click();
  await page.getByLabel('Nome', { exact: true }).fill('Familia log');
  await page.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(page.getByText('Log')).toBeVisible();
  await expect(page.getByText(/created:/)).toBeVisible();
  await expect(page.getByText(/\d{2}\/\d{2}\/\d{4}/)).toBeVisible();
});

test('atalho desativado some da tela principal e pode voltar', async ({ page }) => {
  await signUp(page, '-desativar');
  await page.getByLabel('Nome do atalho').fill('Tomei o remedio');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  const shortcut = page.getByRole('button', { name: 'Tomei o remedio', exact: true });
  await expect(shortcut).toBeVisible({ timeout: 15_000 });
  await page.getByRole('button', { name: 'Desativar Tomei o remedio' }).click();
  await expect(page.getByText('Atalhos desativados')).toBeVisible();
  await expect(shortcut).toHaveCount(0);
  await page.getByRole('button', { name: 'Reativar Tomei o remedio' }).click();
  await expect(page.getByRole('button', { name: 'Tomei o remedio', exact: true })).toBeVisible();
});

test('pular lembrete e aviso de recorrencia acabando', async ({ page }) => {
  await signUp(page, '-skip');
  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const created = await page.request.post(`${apiUrl}/api/reminders`, {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    data: {
      title: 'Ultimas doses',
      startAt: new Date(Date.now() + 3600_000).toISOString(),
      recurrence: { frequency: 'daily', interval: 1, maxOccurrences: 2 },
    },
  });
  expect(created.ok()).toBeTruthy();
  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await expect(page.getByText('Ultimas doses', { exact: true })).toBeVisible();
  await expect(page.getByText('Recorrencia acabando')).toBeVisible();
  await page.getByRole('button', { name: 'Pular Ultimas doses' }).click();
  await expect(page.getByRole('button', { name: 'Pular Ultimas doses' })).toBeVisible();
});

test('descobrir circulo e pedir entrada', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-dono-pedido');
  await owner.getByRole('button', { name: 'Circulos' }).click();
  await expect(owner.getByText('Descobrir circulos')).toBeVisible();
  await owner.getByLabel('Nome', { exact: true }).fill('Clube de corrida');
  await owner.getByLabel('Descricao').fill('Treinos de rua');
  await owner.getByRole('button', { name: 'Oculto na busca' }).click();
  await owner.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(owner.getByText('Quem esta aqui')).toBeVisible();

  const visitorContext = await browser.newContext();
  const visitor = await visitorContext.newPage();
  await signUp(visitor, '-visitante-pedido');
  await visitor.getByRole('button', { name: 'Circulos' }).click();
  await visitor.getByLabel('Buscar circulos').fill('corrida');
  await visitor.getByRole('button', { name: 'Buscar' }).click();
  await visitor.getByText('Clube de corrida').first().click();
  await visitor.getByRole('button', { name: 'Pedir para entrar' }).click();
  await expect(visitor.getByText('Pedido de entrada enviado.')).toBeVisible();

  await owner.reload();
  await expect(owner.getByText('Pedidos de entrada')).toBeVisible();
  await owner.getByRole('button', { name: /Aceitar / }).click();
  await expect(owner.getByText('Pedido aceito.')).toBeVisible();
  await ownerContext.close();
  await visitorContext.close();
});

test('remover foto de perfil', async ({ page }) => {
  await signUp(page, '-foto');
  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const png = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
    'base64',
  );
  const uploaded = await page.request.post(`${apiUrl}/api/profile/photo`, {
    headers: { Authorization: `Bearer ${token}` },
    multipart: {
      file: { name: 'foto.png', mimeType: 'image/png', buffer: png },
    },
  });
  expect(uploaded.ok()).toBeTruthy();
  await page.reload();
  await expect(page.getByText('O que pede atencao hoje')).toBeVisible({ timeout: 20_000 });
  await page.getByRole('button', { name: 'Perfil' }).click();
  await expect(page.getByRole('button', { name: 'Remover foto' })).toBeVisible();
  await page.getByRole('button', { name: 'Remover foto' }).click();
  await expect(page.getByText('Foto removida.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Remover foto' })).toHaveCount(0);
});

test('modo escuro troca a paleta na hora', async ({ page }) => {
  await signUp(page, '-tema');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByRole('button', { name: 'Escuro' }).click();
  await expect(page.getByText('Agora: dark')).toBeVisible();
  await page.screenshot({ path: `${artifacts}/tema-escuro.png`, fullPage: true });
});
