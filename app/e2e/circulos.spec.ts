import { expect, test, type Page } from '@playwright/test';
import { createHmac } from 'node:crypto';
import { existsSync } from 'node:fs';
import { mkdir } from 'node:fs/promises';
import { totpCode } from '../lib/totp';

const artifacts =
  process.env.E2E_ARTIFACTS ??
  (existsSync('/opt/cursor') ? '/opt/cursor/artifacts' : 'test-results/e2e');

async function skipTourIfNeeded(page: Page): Promise<void> {
  const skip = page.getByRole('button', { name: 'Pular tour' });
  const home = page.getByText('O que pede atencao hoje');
  await expect(skip.or(home)).toBeVisible({ timeout: 20_000 });
  if (await skip.isVisible()) {
    await skip.click();
  }
  await expect(home).toBeVisible({ timeout: 20_000 });
}

async function signUp(page: Page, suffix = ''): Promise<string> {
  const email = `e2e-${Date.now()}${suffix}@exemplo.com`;
  await page.goto('/(auth)/register');
  await page.getByLabel('Nome', { exact: true }).fill('Pessoa E2E');
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill('senha-segura-1');
  await page.getByRole('button', { name: 'Criar conta' }).click();
  await skipTourIfNeeded(page);
  return email;
}

async function waitTotpWindow(): Promise<void> {
  const remaining = 30 - (Math.floor(Date.now() / 1000) % 30);
  if (remaining <= 3) {
    await new Promise((resolve) => setTimeout(resolve, (remaining + 1) * 1000));
  }
}

function expiredAccess(userId: string): string {
  const encode = (value: unknown) => Buffer.from(JSON.stringify(value)).toString('base64url');
  const now = Math.floor(Date.now() / 1000);
  const header = encode({ alg: 'HS256', typ: 'JWT' });
  const payload = encode({
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': userId,
    sub: userId,
    typ: 'access',
    nbf: now - 1200,
    exp: now - 60,
    iss: 'Circulos',
    aud: 'Circulos',
  });
  const signature = createHmac('sha256', 'troque-esta-chave-local-de-desenvolvimento-32')
    .update(`${header}.${payload}`)
    .digest('base64url');
  return `${header}.${payload}.${signature}`;
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
  await expect(page.getByText('Log', { exact: true })).toBeVisible();
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
  const stamp = Date.now();
  const circleName = `Clube corrida ${stamp}`;
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByLabel('Descricao').fill(`Treinos ${stamp}`);
  await owner.getByRole('button', { name: 'Oculto na busca' }).click();
  await owner.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(owner.getByText('Quem esta aqui')).toBeVisible();

  const visitorContext = await browser.newContext();
  const visitor = await visitorContext.newPage();
  await signUp(visitor, '-visitante-pedido');
  await visitor.getByRole('button', { name: 'Circulos' }).click();
  await visitor.getByLabel('Buscar circulos').fill(String(stamp));
  await visitor.getByRole('button', { name: 'Buscar' }).click();
  await visitor.getByRole('button', { name: `Descobrir ${circleName}` }).click();
  await visitor.getByRole('button', { name: 'Pedir para entrar' }).click();
  await expect(visitor.getByText('Pedido de entrada enviado.')).toBeVisible();

  await owner.getByRole('button', { name: 'Circulos' }).click();
  await owner.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await expect(owner.getByText('Pedidos de entrada')).toBeVisible({ timeout: 15_000 });
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

test('transferir dono do circulo em vida', async ({ page }) => {
  await signUp(page, '-dono-vida');
  await page.getByRole('button', { name: 'Circulos' }).click();
  await expect(page.getByText('Descobrir circulos')).toBeVisible();
  await page.getByLabel('Nome', { exact: true }).fill('Cuidado');
  await page.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(page.getByText('Quem esta aqui')).toBeVisible();

  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const memberEmail = `e2e-${Date.now()}-sucessor@exemplo.com`;
  const registered = await page.request.post(`${apiUrl}/api/auth/register`, {
    data: { email: memberEmail, password: 'senha-segura-1', name: 'Sucessor' },
  });
  expect(registered.ok()).toBeTruthy();
  const member = (await registered.json()) as { token: string };
  await page.getByLabel('Convidar por e-mail').fill(memberEmail);
  await page.getByRole('button', { name: 'Enviar convite' }).click();
  await expect(page.getByText(/Convite criado/)).toBeVisible();
  const inviteText = await page.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Codigo: ', '').trim();
  const accepted = await page.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${member.token}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Transferir dono para Sucessor' })).toBeVisible();
  await page.getByRole('button', { name: 'Transferir dono para Sucessor' }).click();
  await expect(page.getByText('Dono transferido. Historico e permissoes ficam com o circulo.')).toBeVisible();
  await expect(page.getByText(/Sucessor · owner/)).toBeVisible();
});

test('lembrete anual aparece na tela', async ({ page }) => {
  await signUp(page, '-anual');
  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await page.getByLabel('Titulo').fill('Consulta anual');
  await page.getByRole('button', { name: 'Recorrencia: none' }).click();
  await page.getByRole('button', { name: 'Recorrencia: daily' }).click();
  await page.getByRole('button', { name: 'Recorrencia: weekly' }).click();
  await page.getByRole('button', { name: 'Recorrencia: monthly' }).click();
  await expect(page.getByRole('button', { name: 'Recorrencia: yearly' })).toBeVisible();
  await page.getByRole('button', { name: 'Criar lembrete' }).click();
  await expect(page.getByText('Consulta anual', { exact: true })).toBeVisible();
  await expect(page.getByText(/^yearly ·/)).toBeVisible();
});

test('ferias com prazo escolhido', async ({ page }) => {
  await signUp(page, '-ferias');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByLabel('Dias de ferias').fill('3');
  await page.getByRole('button', { name: 'Silenciar ferias' }).click();
  await expect(page.getByText('Modo ferias ligado por 3 dia(s). Lembretes ficam guardados.')).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await expect(page.getByText(/Modo ferias ate/)).toBeVisible();
});

test('atalho marca lembrete de circulo como feito', async ({ page }) => {
  await signUp(page, '-atalho-ciclo');
  await page.getByRole('button', { name: 'Circulos' }).click();
  await page.getByLabel('Nome', { exact: true }).fill('Casa');
  await page.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(page.getByText('Quem esta aqui')).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await page.getByLabel('Titulo').fill('Tomei');
  await page.getByRole('button', { name: 'Sem circulo' }).click();
  await expect(page.getByRole('button', { name: 'Casa' })).toBeVisible();
  await page.getByRole('button', { name: 'Criar lembrete' }).click();
  await expect(page.getByText('Tomei', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await page.getByRole('button', { name: 'Sem lembrete de circulo' }).click();
  await expect(page.getByRole('button', { name: 'Lembrete: Tomei' })).toBeVisible();
  await page.getByLabel('Nome do atalho').fill('Foi feito');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  await page.getByRole('button', { name: 'Foi feito', exact: true }).click();
  await expect(page.getByText('Foi feito').first()).toBeVisible();
});

test('painel do dia atualiza sozinho', async ({ page }) => {
  await signUp(page, '-painel');
  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const created = await page.request.post(`${apiUrl}/api/reminders`, {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    data: {
      title: 'Painel sozinho',
      startAt: new Date(Date.now() + 3600_000).toISOString(),
      recurrence: { frequency: 'none', interval: 1 },
    },
  });
  expect(created.ok()).toBeTruthy();
  await expect(page.getByText('Painel sozinho', { exact: true })).toBeVisible({ timeout: 15_000 });
});

test('busca unificada com historico', async ({ page }) => {
  await signUp(page, '-busca');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByText('Diario', { exact: true }).click();
  await page.getByLabel('Titulo').fill('Texto livre da medicacao');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Texto livre da medicacao')).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await expect(page.getByText('O que pede atencao hoje')).toBeVisible();
  await page.getByRole('button', { name: 'Busca' }).first().click();
  await page.getByLabel('Buscar', { exact: true }).fill('medicacao');
  await page.getByRole('button', { name: 'Buscar agora' }).click();
  await expect(page.getByText('Texto livre da medicacao')).toBeVisible();
  await expect(page.getByText('Mais usadas')).toBeVisible();
});

test('atalhos reordenam na tela', async ({ page }) => {
  await signUp(page, '-ordem');
  await page.getByLabel('Nome do atalho').fill('Primeiro');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  await expect(page.getByRole('button', { name: 'Primeiro', exact: true })).toBeVisible({ timeout: 15_000 });
  await page.getByLabel('Nome do atalho').fill('Segundo');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  await expect(page.getByRole('button', { name: 'Segundo', exact: true })).toBeVisible({ timeout: 15_000 });
  await page.getByRole('button', { name: 'Subir Segundo' }).click();
  await expect.poll(async () => {
    return page.evaluate(() => {
      const labels = [...document.querySelectorAll('[role="button"]')]
        .map((el) => el.getAttribute('aria-label') || '')
        .filter((label) => label === 'Primeiro' || label === 'Segundo');
      return labels[0] ?? '';
    });
  }).toBe('Segundo');
});

test('termos e privacidade no app', async ({ page }) => {
  await page.goto('/termos');
  await expect(page.getByText('Termos de uso')).toBeVisible();
  await expect(page.getByText(/ALLAN WANDERSON MENESES DE SOUSA/).first()).toBeVisible();
  await page.goto('/privacidade');
  await expect(page.getByText('Politica de privacidade')).toBeVisible();
  await expect(page.getByText('Lei Geral de Protecao de Dados')).toBeVisible();
  await expect(page.getByText(/ALLAN WANDERSON MENESES DE SOUSA/).first()).toBeVisible();
});

test('lista de compras no perfil', async ({ page }) => {
  await signUp(page, '-lista');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Listas' }).click();
  await page.getByLabel('Nome da lista').fill('Feira');
  await page.getByRole('button', { name: 'Criar lista' }).click();
  await page.getByRole('button', { name: 'Feira', exact: true }).click();
  await page.getByLabel('Item', { exact: true }).fill('Arroz');
  await page.getByLabel('Quantidade').fill('2 kg');
  await page.getByRole('button', { name: 'Adicionar item' }).click();
  await expect(page.getByText('Arroz', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Marcar Arroz' }).click();
  await expect(page.getByText(/feito por/)).toBeVisible();
});

test('permissao da lista: so ver e depois marcar feito', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-lista-dono');
  await owner.getByRole('button', { name: 'Circulos' }).click();
  const circleName = `Casa lista ${Date.now()}`;
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(owner.getByText('Listas do circulo')).toBeVisible();

  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const memberEmail = `e2e-${Date.now()}-lista-membro@exemplo.com`;
  const registered = await owner.request.post(`${apiUrl}/api/auth/register`, {
    data: { email: memberEmail, password: 'senha-segura-1', name: 'Membro Lista' },
  });
  expect(registered.ok()).toBeTruthy();
  const member = (await registered.json()) as { token: string };
  await owner.getByLabel('Convidar por e-mail').fill(memberEmail);
  await owner.getByRole('button', { name: 'Enviar convite' }).click();
  await expect(owner.getByText(/Convite criado/)).toBeVisible();
  const inviteText = await owner.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Codigo: ', '').trim();
  const accepted = await owner.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${member.token}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();

  await owner.getByLabel('Nome da lista').fill('Compras da casa');
  await expect(owner.getByLabel('Nome da lista')).toHaveValue('Compras da casa');
  await expect(owner.getByRole('button', { name: 'Criar lista' })).toBeEnabled();
  await owner.getByRole('button', { name: 'Criar lista' }).click();
  await expect(owner.getByRole('button', { name: 'Compras da casa', exact: true })).toBeVisible({ timeout: 15_000 });
  await owner.getByRole('button', { name: 'Compras da casa', exact: true }).click();
  await owner.getByLabel('Item', { exact: true }).fill('Leite');
  await owner.getByRole('button', { name: 'Adicionar item' }).click();
  await expect(owner.getByText('Leite', { exact: true })).toBeVisible();

  const memberContext = await browser.newContext();
  const memberPage = await memberContext.newPage();
  await memberPage.goto('/(auth)/login');
  await memberPage.getByLabel('E-mail').fill(memberEmail);
  await memberPage.getByLabel('Senha').fill('senha-segura-1');
  await memberPage.getByRole('button', { name: 'Entrar' }).click();
  await skipTourIfNeeded(memberPage);
  await memberPage.getByRole('button', { name: 'Circulos' }).click();
  await memberPage.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await memberPage.getByRole('button', { name: 'Compras da casa', exact: true }).click();
  await expect(memberPage.getByText('Voce so ve esta lista.')).toBeVisible();
  await expect(memberPage.getByRole('button', { name: 'Adicionar item' })).toHaveCount(0);
  await expect(memberPage.getByRole('button', { name: 'Marcar Leite' })).toHaveCount(0);

  await owner.getByRole('button', { name: /^Permissao dos membros:/ }).click();
  await owner.getByRole('button', { name: /^Permissao dos membros:/ }).click();
  await expect(owner.getByRole('button', { name: 'Permissao dos membros: membros marcam feito' })).toBeVisible();
  await expect(memberPage.getByRole('button', { name: 'Marcar Leite' })).toBeVisible({ timeout: 15_000 });
  await memberPage.getByRole('button', { name: 'Marcar Leite' }).click();
  await expect(memberPage.getByText(/feito por/)).toBeVisible();
  await expect(memberPage.getByRole('button', { name: 'Excluir Leite' })).toHaveCount(0);

  await ownerContext.close();
  await memberContext.close();
});

test('modo escuro troca a paleta na hora', async ({ page }) => {
  await signUp(page, '-tema');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByRole('button', { name: 'Escuro' }).click();
  await expect(page.getByText('Agora: dark')).toBeVisible();
  await page.screenshot({ path: `${artifacts}/tema-escuro.png`, fullPage: true });
});

test('tour na primeira abertura e FAQ no menu', async ({ browser }) => {
  const context = await browser.newContext();
  const page = await context.newPage();
  const email = `e2e-${Date.now()}-tour@exemplo.com`;
  await page.goto('/(auth)/register');
  await page.getByLabel('Nome', { exact: true }).fill('Pessoa Tour');
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill('senha-segura-1');
  await page.getByRole('button', { name: 'Criar conta' }).click();
  await expect(page.getByText('Seu perfil solo')).toBeVisible({ timeout: 20_000 });
  await page.getByRole('button', { name: 'Avancar' }).click();
  await expect(page.getByText('Painel do dia')).toBeVisible();
  await page.getByRole('button', { name: 'Pular tour' }).click();
  await expect(page.getByText('O que pede atencao hoje')).toBeVisible({ timeout: 20_000 });
  await page.getByRole('button', { name: 'FAQ' }).first().click();
  await expect(page.getByText('O que e o Circulos?')).toBeVisible();
  await expect(page.getByText('Role ate achar o assunto. Nao ha busca nesta lista.')).toBeVisible();
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByRole('button', { name: 'Ver tour de novo' }).click();
  await expect(page.getByText('Seu perfil solo')).toBeVisible();
  await page.getByRole('button', { name: 'Pular tour' }).click();
  await expect(page.getByText('O que pede atencao hoje')).toBeVisible({ timeout: 20_000 });
  await context.close();
});

test('access expirado renova com refresh; logout revoga', async ({ page }) => {
  await signUp(page, '-refresh');
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const access = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const refresh = await page.evaluate(() => localStorage.getItem('circulos.refresh'));
  expect(access).toBeTruthy();
  expect(refresh).toBeTruthy();
  const me = await page.request.get(`${apiUrl}/api/auth/me`, {
    headers: { Authorization: `Bearer ${access}` },
  });
  expect(me.ok()).toBeTruthy();
  const user = (await me.json()) as { id: string };
  await page.evaluate((jwt) => localStorage.setItem('circulos.token', jwt), expiredAccess(user.id));
  await page.reload();
  await expect(page.getByText('O que pede atencao hoje')).toBeVisible({ timeout: 20_000 });
  const rotated = await page.evaluate(() => localStorage.getItem('circulos.refresh'));
  expect(rotated).toBeTruthy();
  const reuse = await page.request.post(`${apiUrl}/api/auth/refresh`, {
    data: { refreshToken: refresh },
  });
  expect(reuse.status()).toBe(401);
  await page.getByRole('button', { name: 'Sair' }).click();
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible({ timeout: 20_000 });
  const afterLogout = await page.request.post(`${apiUrl}/api/auth/refresh`, {
    data: { refreshToken: rotated },
  });
  expect(afterLogout.status()).toBe(401);
});

test('segundo fator com QR exige codigo no login', async ({ page }) => {
  const email = await signUp(page, '-2fa');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByRole('button', { name: 'Ativar segundo fator' }).click();
  await expect(page.getByLabel('QR do autenticador')).toBeVisible({ timeout: 15_000 });
  const secretLine = await page.getByText(/^Segredo: /).innerText();
  const secret = secretLine.replace('Segredo: ', '').trim();
  await waitTotpWindow();
  await page.getByLabel('Codigo do autenticador').fill(await totpCode(secret));
  await page.getByRole('button', { name: 'Confirmar segundo fator' }).click();
  await expect(page.getByText('Segundo fator ativo. Guarde os codigos de recuperacao.')).toBeVisible();
  await page.getByRole('button', { name: 'Sair' }).first().click();
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible({ timeout: 20_000 });
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill('senha-segura-1');
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page.getByText(/Informe o codigo do autenticador/)).toBeVisible();
  await waitTotpWindow();
  await page.getByLabel('Codigo do autenticador').fill(await totpCode(secret));
  await page.getByRole('button', { name: 'Confirmar codigo' }).click();
  await skipTourIfNeeded(page);
});

test('offline le o cache, enfileira item e 403 descarta com aviso', async ({ browser }) => {
  test.setTimeout(90_000);
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-off-dono');
  await owner.getByRole('button', { name: 'Circulos' }).click();
  const circleName = `Casa off ${Date.now()}`;
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(owner.getByText('Listas do circulo')).toBeVisible();

  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const memberEmail = `e2e-${Date.now()}-off-membro@exemplo.com`;
  const registered = await owner.request.post(`${apiUrl}/api/auth/register`, {
    data: { email: memberEmail, password: 'senha-segura-1', name: 'Membro Off' },
  });
  expect(registered.ok()).toBeTruthy();
  const member = (await registered.json()) as { token: string };
  await owner.getByLabel('Convidar por e-mail').fill(memberEmail);
  await owner.getByRole('button', { name: 'Enviar convite' }).click();
  await expect(owner.getByText(/Convite criado/)).toBeVisible();
  const inviteText = await owner.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Codigo: ', '').trim();
  const accepted = await owner.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${member.token}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();

  await owner.getByLabel('Nome da lista').fill('Compras off');
  await owner.getByRole('button', { name: 'Criar lista' }).click();
  await expect(owner.getByRole('button', { name: 'Compras off', exact: true })).toBeVisible({ timeout: 15_000 });
  await owner.getByRole('button', { name: 'Compras off', exact: true }).click();
  await owner.getByLabel('Item', { exact: true }).fill('Leite');
  await owner.getByRole('button', { name: 'Adicionar item' }).click();
  await expect(owner.getByText('Leite', { exact: true })).toBeVisible();
  await owner.getByRole('button', { name: /^Permissao dos membros:/ }).click();
  await owner.getByRole('button', { name: /^Permissao dos membros:/ }).click();
  await expect(owner.getByRole('button', { name: 'Permissao dos membros: membros marcam feito' })).toBeVisible();

  const memberContext = await browser.newContext();
  const memberPage = await memberContext.newPage();
  await memberPage.goto('/(auth)/login');
  await memberPage.getByLabel('E-mail').fill(memberEmail);
  await memberPage.getByLabel('Senha').fill('senha-segura-1');
  await memberPage.getByRole('button', { name: 'Entrar' }).click();
  await skipTourIfNeeded(memberPage);
  await memberPage.getByRole('button', { name: 'Circulos' }).click();
  await memberPage.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await memberPage.getByRole('button', { name: 'Compras off', exact: true }).click();
  await expect(memberPage.getByRole('button', { name: 'Marcar Leite' })).toBeVisible({ timeout: 15_000 });

  await memberPage.context().setOffline(true);
  await expect(memberPage.getByLabel('Offline')).toBeVisible();
  await memberPage.getByRole('button', { name: 'Marcar Leite' }).click();
  await expect(memberPage.getByText(/feito por/)).toBeVisible();

  await owner.getByRole('button', { name: /^Permissao dos membros:/ }).click();
  await owner.getByRole('button', { name: /^Permissao dos membros:/ }).click();
  await expect(owner.getByRole('button', { name: 'Permissao dos membros: so o dono edita' })).toBeVisible();

  await memberPage.context().setOffline(false);
  await memberPage.getByRole('button', { name: 'Sincronizar fila' }).click();
  await expect(memberPage.getByText('Esta lista nao permite marcar itens.')).toBeVisible({ timeout: 15_000 });

  await ownerContext.close();
  await memberContext.close();
});

test('offline adiciona item na fila e sobe ao voltar', async ({ page }) => {
  await signUp(page, '-off-add');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Listas' }).click();
  await page.getByLabel('Nome da lista').fill('Feira off');
  await page.getByRole('button', { name: 'Criar lista' }).click();
  await page.getByRole('button', { name: 'Feira off', exact: true }).click();
  await page.getByLabel('Item', { exact: true }).fill('Arroz');
  await page.getByRole('button', { name: 'Adicionar item' }).click();
  await expect(page.getByText('Arroz', { exact: true })).toBeVisible();
  await page.context().setOffline(true);
  await expect(page.getByLabel('Offline')).toBeVisible();
  await page.getByLabel('Item', { exact: true }).fill('Feijao');
  await page.getByRole('button', { name: 'Adicionar item' }).click();
  await expect(page.getByText('Feijao', { exact: true })).toBeVisible();
  await page.context().setOffline(false);
  await page.getByRole('button', { name: 'Sincronizar fila' }).click();
  await expect(page.getByLabel('Online')).toBeVisible();
  await page.reload();
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Listas' }).click();
  await page.getByRole('button', { name: 'Feira off', exact: true }).click();
  await expect(page.getByText('Feijao', { exact: true })).toBeVisible({ timeout: 20_000 });
});

test('backup do perfil e grafico de armazenamento', async ({ page }) => {
  await signUp(page, '-backup');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await expect(page.getByText('Uso de armazenamento')).toBeVisible();
  await expect(page.getByLabel(/Uso de armazenamento/)).toBeVisible();
  await expect(page.getByText(/Sem separar por tipo/)).toBeVisible();
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Exportar meu perfil' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/circulos-perfil/);
  await page.getByRole('button', { name: 'Circulos' }).click();
  await page.getByLabel('Nome', { exact: true }).fill('Casa backup');
  await page.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(page.getByText('Quem esta aqui')).toBeVisible();
  const circleDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Exportar este circulo' }).click();
  const circleFile = await circleDownload;
  expect(circleFile.suggestedFilename()).toMatch(/circulos-/);
});
