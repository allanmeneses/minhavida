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
  await page.getByLabel('Titulo', { exact: true }).fill('Hepatite B');
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
  await page.getByLabel('Titulo', { exact: true }).fill('Aniversario da Ana');
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
  await page.getByLabel('Titulo', { exact: true }).fill('Corrida');
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
  await page.getByLabel('Titulo', { exact: true }).fill('Consulta anual');
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
  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await page.getByLabel('Titulo', { exact: true }).fill('Tomei');
  const reminderCircle = page.getByRole('button', { name: /^(Sem circulo|Eu|Familia)$/ });
  for (let i = 0; i < 4; i++) {
    if (await page.getByRole('button', { name: 'Familia', exact: true }).isVisible()) {
      break;
    }
    await reminderCircle.click();
  }
  await expect(page.getByRole('button', { name: 'Familia', exact: true })).toBeVisible();
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
  await page.getByLabel('Titulo', { exact: true }).fill('Texto livre da medicacao');
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

test('access expirado renova com refresh; logout revoga', async ({ page, context }) => {
  await signUp(page, '-refresh');
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const access = await page.evaluate(() => localStorage.getItem('circulos.token'));
  expect(access).toBeTruthy();
  expect(await page.evaluate(() => localStorage.getItem('circulos.refresh'))).toBeNull();
  const apiCookies = await context.cookies(apiUrl);
  const refreshCookie = apiCookies.find((item) => item.name === 'circulos.refresh');
  expect(refreshCookie?.httpOnly).toBeTruthy();
  const me = await page.request.get(`${apiUrl}/api/auth/me`, {
    headers: { Authorization: `Bearer ${access}` },
  });
  expect(me.ok()).toBeTruthy();
  const user = (await me.json()) as { id: string };
  await page.evaluate((jwt) => localStorage.setItem('circulos.token', jwt), expiredAccess(user.id));
  await page.reload();
  await expect(page.getByText('O que pede atencao hoje')).toBeVisible({ timeout: 20_000 });
  const reuse = await page.request.post(`${apiUrl}/api/auth/refresh`, {
    headers: { 'X-Client': 'web' },
    data: { refreshToken: refreshCookie?.value },
  });
  expect(reuse.status()).toBe(401);
  await page.getByRole('button', { name: 'Sair' }).click();
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible({ timeout: 20_000 });
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

test('album de memorias no perfil e rascunho sobrevive ao rerender', async ({ page }) => {
  await signUp(page, '-memoria');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Memorias' }).first().click();
  await page.getByLabel('Titulo', { exact: true }).fill('Primeiro dia de escola');
  await page.getByLabel('Texto').fill('Foto da entrada');
  await page.getByRole('button', { name: 'Guardar memoria' }).click();
  await expect(page.getByText('Primeiro dia de escola', { exact: true })).toBeVisible();
  await page.getByLabel('Texto de Primeiro dia de escola').fill('Texto que a pessoa digitou');
  await page.getByLabel('Titulo', { exact: true }).fill('outra memoria');
  await expect(page.getByLabel('Texto de Primeiro dia de escola')).toHaveValue('Texto que a pessoa digitou');
  await page.getByRole('button', { name: 'Salvar Primeiro dia de escola' }).click();
  await expect(page.getByLabel('Texto de Primeiro dia de escola')).toHaveValue('Texto que a pessoa digitou');
});

test('calendario mostra lembrete e baixa ics', async ({ page }) => {
  await signUp(page, '-cal');
  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const created = await page.request.post(`${apiUrl}/api/reminders`, {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    data: {
      title: 'Tomei o remedio',
      startAt: new Date().toISOString(),
      recurrence: { frequency: 'daily', interval: 1 },
    },
  });
  expect(created.ok()).toBeTruthy();
  await page.getByRole('button', { name: 'Calendario' }).first().click();
  await expect(page.getByText('Tomei o remedio').first()).toBeVisible({ timeout: 15_000 });
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Baixar .ics' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/circulos-lembretes\.ics/);
  await expect(page.getByText(/somente leitura/)).toBeVisible();
  await expect(page.getByText(/Assinatura: \/api\/calendar\/feed\//)).toBeVisible();
});

test('modelos prontos copiam lista em portugues', async ({ page }) => {
  await signUp(page, '-modelo');
  await page.getByRole('button', { name: 'Modelos' }).first().click();
  await expect(page.getByText('Compras da semana', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Usar Compras da semana' }).click();
  await expect(page.getByText('Lista copiada: Compras da semana. Voce pode editar os itens.')).toBeVisible();
  await page.getByRole('button', { name: 'Perfil' }).first().click();
  await page.getByRole('button', { name: 'Listas' }).click();
  await expect(page.getByRole('button', { name: 'Compras da semana', exact: true })).toBeVisible();
});

test('perfil de pet no circulo', async ({ page }) => {
  await signUp(page, '-pet');
  await page.getByRole('button', { name: 'Circulos' }).click();
  const circleName = `Casa pet ${Date.now()}`;
  await page.getByLabel('Nome', { exact: true }).fill(circleName);
  await page.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(page.getByText('Quem esta aqui')).toBeVisible();
  await page.getByRole('button', { name: 'Perfil' }).first().click();
  await page.getByRole('button', { name: 'Pet e grupo' }).click();
  await expect(page.getByText('Pet e grupo', { exact: true })).toBeVisible();
  await page.getByPlaceholder('Luna').fill('Luna');
  await page.getByPlaceholder('Nossa cachorra').fill('Nossa cachorra');
  await page.getByRole('button', { name: 'So no meu perfil' }).click();
  for (let i = 0; i < 6; i++) {
    if (await page.getByRole('button', { name: `Circulo: ${circleName}` }).isVisible()) {
      break;
    }
    await page.getByRole('button', { name: /^Circulo: / }).click();
  }
  await expect(page.getByRole('button', { name: `Circulo: ${circleName}` })).toBeVisible();
  await page.getByRole('button', { name: 'Criar perfil' }).click();
  await expect(page.getByText('Luna', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Circulos' }).click();
  await page.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await expect(page.getByText('Luna · pet')).toBeVisible();
});

test('revelacao surpresa guarda recordacao no perfil', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-rev-dono');
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const honradoEmail = `e2e-${Date.now()}-rev-honrado@exemplo.com`;
  const registered = await owner.request.post(`${apiUrl}/api/auth/register`, {
    data: { email: honradoEmail, password: 'senha-segura-1', name: 'Aniversariante' },
  });
  expect(registered.ok()).toBeTruthy();
  await owner.getByRole('button', { name: 'Circulos' }).click();
  const circleName = `Festa ${Date.now()}`;
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByRole('button', { name: 'Surpresa desligada' }).click();
  await owner.getByLabel('E-mail de quem vai se surpreender').fill(honradoEmail);
  await owner.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(owner.getByText('Quem esta aqui')).toBeVisible();
  await owner.getByLabel('Nova entrada').fill('Feliz aniversario');
  await owner.getByRole('button', { name: 'Publicar' }).click();
  await expect(owner.getByText('Feliz aniversario').first()).toBeVisible();
  await owner.getByRole('button', { name: 'Revelar surpresa' }).click();
  await expect(owner.getByText('Resumo da revelacao')).toBeVisible();
  await expect(owner.getByText('Feliz aniversario').first()).toBeVisible();

  const honradoContext = await browser.newContext();
  const honrado = await honradoContext.newPage();
  await honrado.goto('/(auth)/login');
  await honrado.getByLabel('E-mail').fill(honradoEmail);
  await honrado.getByLabel('Senha').fill('senha-segura-1');
  await honrado.getByRole('button', { name: 'Entrar' }).click();
  await skipTourIfNeeded(honrado);
  await honrado.getByRole('button', { name: 'Circulos' }).click();
  await honrado.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await expect(honrado.getByText('Resumo da revelacao')).toBeVisible();
  await honrado.getByRole('button', { name: 'Guardar recordacao no meu perfil' }).click();
  await expect(honrado.getByText('Recordacao guardada no seu perfil. A edicao e silenciosa.')).toBeVisible();
  await honrado.getByRole('button', { name: 'Perfil', exact: true }).click();
  await honrado.getByRole('button', { name: 'Memorias' }).first().click();
  await expect(honrado.getByText(`Recordacao: ${circleName}`, { exact: true })).toBeVisible();
  await ownerContext.close();
  await honradoContext.close();
});

test('pagina de status publica', async ({ page }) => {
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const health = await page.request.get(`${apiUrl}/health`);
  expect(health.ok()).toBeTruthy();
  await page.goto(`${apiUrl}/status`);
  await expect(page.getByRole('heading', { name: 'Status do Circulos' })).toBeVisible();
  await expect(page.getByText('OpenTelemetry')).toBeVisible();
  await expect(page.getByText('Banco')).toBeVisible();
});

test('recap de uso no perfil so mostra fluxo usado', async ({ page }) => {
  await signUp(page, '-recap');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Resumo de uso' }).click();
  await expect(page.getByText('Ainda nao ha uso para resumir.')).toBeVisible();
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByText('Vacinas', { exact: true }).click();
  await page.getByLabel('Titulo', { exact: true }).fill('Hepatite B');
  await page.getByLabel('Quando').fill('2024-03-10');
  await page.getByLabel('Onde').fill('UBS Centro');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Hepatite B')).toBeVisible();
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Resumo de uso' }).click();
  await expect(page.getByText('Vacinas · 1')).toBeVisible();
  await expect(page.getByText('Diario', { exact: true })).toHaveCount(0);
  await page.context().setOffline(true);
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Resumo de uso' }).click();
  await expect(page.getByText('Vacinas · 1')).toBeVisible();
  await page.context().setOffline(false);
});

test('circulo temporario arquiva e compartilha a historia', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-recap-circulo');
  await owner.getByRole('button', { name: 'Circulos' }).click();
  const circleName = `Curso recap ${Date.now()}`;
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByRole('button', { name: 'Tipo: Duradouro' }).click();
  await expect(owner.getByRole('button', { name: 'Tipo: Temporario' })).toBeVisible();
  await owner.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(owner.getByText('Temporario · active')).toBeVisible();
  await owner.getByLabel('Nova entrada').fill('Terminei o modulo');
  await owner.getByRole('button', { name: 'Publicar' }).click();
  await expect(owner.getByText('Terminei o modulo').first()).toBeVisible();
  await expect(owner.getByText('Minha retrospectiva')).toBeVisible();
  await expect(owner.getByText('Minhas entradas · 1')).toBeVisible();
  await owner.getByRole('button', { name: 'Encerrar ciclo e arquivar' }).click();
  await expect(owner.getByText('Ciclo encerrado. A retrospectiva fica para consulta.', { exact: true })).toBeVisible();
  await expect(owner.getByText('Temporario · archived')).toBeVisible();
  await owner.getByRole('button', { name: 'Compartilhar minha historia' }).click();
  await expect(owner.getByText(/Historia compartilhada: \/r\//)).toBeVisible();
  const shareLine = await owner.getByText(/Historia compartilhada: \/r\//).innerText();
  const sharePath = shareLine.replace('Historia compartilhada: ', '').trim();

  const visitorContext = await browser.newContext();
  const visitor = await visitorContext.newPage();
  await visitor.goto(sharePath);
  await expect(visitor.getByText('Historia compartilhada')).toBeVisible();
  await expect(visitor.getByText('Terminei o modulo')).toBeVisible();
  await expect(visitor.getByText('Pessoa E2E')).toBeVisible();
  await ownerContext.close();
  await visitorContext.close();
});

test('perfil publico compartilhavel com privacidade', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-publico');
  await owner.getByRole('button', { name: 'Perfil' }).click();
  await expect(owner.getByText('O perfil esta privado. Ninguem de fora ve nome nem foto.')).toBeVisible();
  await owner.getByRole('button', { name: 'Perfil privado' }).click();
  await owner.getByRole('button', { name: 'Salvar perfil' }).click();
  await expect(owner.getByText('Perfil atualizado.')).toBeVisible();
  await expect(owner.getByText(/Link publico: \/p\//)).toBeVisible();
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const token = await owner.evaluate(() => localStorage.getItem('circulos.token'));
  const me = await owner.request.get(`${apiUrl}/api/auth/me`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(me.ok()).toBeTruthy();
  const user = (await me.json()) as { id: string; name: string };

  const visitorContext = await browser.newContext();
  const visitor = await visitorContext.newPage();
  await visitor.goto(`/p/${user.id}`);
  await expect(visitor.getByText('Perfil publico')).toBeVisible();
  await expect(visitor.getByText('Pessoa E2E', { exact: true })).toBeVisible();
  await expect(visitor.getByText('Quem visita ve nome, foto e descricao. Telefone, e-mail e diario ficam fora.')).toBeVisible();
  await expect(visitor.getByLabel('Telefone')).toHaveCount(0);

  await owner.getByRole('button', { name: 'Perfil publico' }).click();
  await owner.getByRole('button', { name: 'Salvar perfil' }).click();
  await expect(owner.getByText('O perfil esta privado. Ninguem de fora ve nome nem foto.')).toBeVisible();
  await visitor.reload();
  await expect(visitor.getByText('Perfil nao encontrado.')).toBeVisible();
  await ownerContext.close();
  await visitorContext.close();
});

test('contraste alto troca a paleta na hora', async ({ page }) => {
  await signUp(page, '-contraste');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByRole('button', { name: 'Contraste alto' }).click();
  await expect(page.getByText('Contraste: alto.')).toBeVisible();
  await page.screenshot({ path: `${artifacts}/contraste-alto.png`, fullPage: true });
});

test('diario mostra exemplo de preenchimento', async ({ page }) => {
  await signUp(page, '-exemplo');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByText('Diario', { exact: true }).click();
  await expect(page.getByText('Exemplo: um paragrafo do dia. Busca por palavra-chave. Sem versao.')).toBeVisible();
  await expect(page.getByPlaceholder('Corrida de manha')).toBeVisible();
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

test('assinatura simulada e webhook', async ({ page }) => {
  await signUp(page, '-plano');
  await page.goto('/pricing');
  await expect(page.getByText('Precos aprovados pelo Allan em 22/11/2026')).toBeVisible();
  await page.getByRole('button', { name: 'Assinar mensal (sandbox)' }).click();
  await expect(page.getByText('Assinatura simulada no sandbox')).toBeVisible();
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const planRes = await page.request.get(`${apiUrl}/api/plan`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(planRes.ok()).toBeTruthy();
  const plan = (await planRes.json()) as { subscriptionProviderId: string; plan: string };
  expect(plan.plan).toBe('paid');
  const ts = Math.floor(Date.now() / 1000).toString();
  const requestId = `e2e-${Date.now()}`;
  const id = plan.subscriptionProviderId.toLowerCase();
  const manifest = `id:${id};request-id:${requestId};ts:${ts};`;
  const v1 = createHmac('sha256', 'fake-webhook-secret').update(manifest).digest('hex');
  const webhook = await page.request.post(`${apiUrl}/webhooks/mercadopago`, {
    headers: {
      'content-type': 'application/json',
      'x-signature': `ts=${ts},v1=${v1}`,
      'x-request-id': requestId,
    },
    data: { type: 'subscription_preapproval', data: { id: plan.subscriptionProviderId } },
  });
  expect(webhook.ok()).toBeTruthy();
  await page.getByRole('button', { name: 'Voltar' }).click();
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await expect(page.getByText('Status: active. Em dia. Sandbox. Assinatura ativa.')).toBeVisible();
});

test('votacao de sucessao com prazo', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-voto-dono');
  const memberContext = await browser.newContext();
  const member = await memberContext.newPage();
  const memberEmail = await signUp(member, '-voto-membro');
  await owner.getByRole('button', { name: 'Circulos' }).click();
  await owner.getByRole('button', { name: 'Privado' }).click();
  await owner.getByLabel('Nome', { exact: true }).fill('Casa voto');
  await owner.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(owner.getByText('Casa voto').first()).toBeVisible();
  await owner.getByLabel('Convidar por e-mail').fill(memberEmail);
  await owner.getByRole('button', { name: 'Enviar convite' }).click();
  const inviteText = await owner.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Codigo: ', '').trim();
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const memberToken = await member.evaluate(() => localStorage.getItem('circulos.token'));
  const accepted = await member.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${memberToken}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();
  await owner.getByRole('button', { name: 'Sair e abrir votacao' }).click();
  await member.getByRole('button', { name: 'Circulos' }).click();
  await member.getByRole('button', { name: 'Abrir Casa voto' }).click();
  await expect(member.getByText('Votacao de sucessao')).toBeVisible();
  await expect(member.getByText(/Prazo de 3 dias/)).toBeVisible();
  await member.getByRole('button', { name: /Votar em/ }).first().click();
  await expect(member.getByText(/Status: (open|decided)/)).toBeVisible();
  await expect(member.getByText('O dono eleito so pode ser substituido apos 30 dias da eleicao.')).toBeVisible();
  await ownerContext.close();
  await memberContext.close();
});

test('busca por raio com e sem permissao', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-geo-dono');
  const seekerContext = await browser.newContext();
  const seeker = await seekerContext.newPage();
  await signUp(seeker, '-geo-busca');
  await seeker.getByRole('button', { name: 'Busca' }).click();
  await seeker.getByRole('button', { name: 'Buscar por perto' }).click();
  await expect(seeker.getByText('Ninguem por perto com permissao.')).toBeVisible();
  const geoName = `Geo ${Date.now()}`;
  await owner.getByRole('button', { name: 'Circulos' }).click();
  await owner.getByLabel('Nome', { exact: true }).fill(geoName);
  await owner.getByRole('button', { name: 'Nao aparecer na busca por perto' }).click();
  await owner.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(owner.getByText(geoName).first()).toBeVisible();
  await seeker.getByRole('button', { name: 'Buscar por perto' }).click();
  await expect(seeker.getByText(`${geoName} · circle`, { exact: false })).toBeVisible();
  await expect(seeker.getByText(`${geoName} · person`, { exact: false })).toHaveCount(0);
  await ownerContext.close();
  await seekerContext.close();
});

test('perfil publico mostra descricao', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-bio');
  await owner.getByRole('button', { name: 'Perfil' }).click();
  await owner.getByLabel('Descricao publica').fill('Corro de manha');
  await owner.getByRole('button', { name: 'Perfil privado' }).click();
  await owner.getByRole('button', { name: 'Salvar perfil' }).click();
  await expect(owner.getByText('Perfil atualizado.')).toBeVisible();
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const token = await owner.evaluate(() => localStorage.getItem('circulos.token'));
  const me = await owner.request.get(`${apiUrl}/api/auth/me`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  const user = (await me.json()) as { id: string };
  const visitorContext = await browser.newContext();
  const visitor = await visitorContext.newPage();
  await visitor.goto(`/p/${user.id}`);
  await expect(visitor.getByText('Corro de manha')).toBeVisible();
  await ownerContext.close();
  await visitorContext.close();
});

test('exclusao definitiva do circulo', async ({ page }) => {
  await signUp(page, '-apagar');
  await page.getByRole('button', { name: 'Circulos' }).click();
  await page.getByLabel('Nome', { exact: true }).fill('Apagar ja');
  await page.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(page.getByText('Exclusao definitiva. Nao da para desfazer.')).toBeVisible();
  await page.getByLabel('Confirmacao da exclusao').fill('excluir definitivo');
  await page.getByRole('button', { name: 'Excluir circulo de vez' }).click();
  await expect(page.getByText('Os seus')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Abrir Apagar ja' })).toHaveCount(0);
});

test('permissao de pet e grupo', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-pet-dono');
  const memberContext = await browser.newContext();
  const member = await memberContext.newPage();
  const memberEmail = await signUp(member, '-pet-membro');
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const memberToken = await member.evaluate(() => localStorage.getItem('circulos.token'));
  const memberMe = await member.request.get(`${apiUrl}/api/auth/me`, {
    headers: { Authorization: `Bearer ${memberToken}` },
  });
  const memberUser = (await memberMe.json()) as { id: string };
  await owner.getByRole('button', { name: 'Circulos' }).click();
  await owner.getByLabel('Nome', { exact: true }).fill('Casa pet');
  await owner.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(owner.getByText('Quem esta aqui')).toBeVisible();
  await owner.getByLabel('Convidar por e-mail').fill(memberEmail);
  await owner.getByRole('button', { name: 'Enviar convite' }).click();
  const inviteText = await owner.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Codigo: ', '').trim();
  const accepted = await member.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${memberToken}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();
  await owner.getByRole('button', { name: 'Perfil' }).first().click();
  await owner.getByRole('button', { name: 'Pet e grupo' }).click();
  await expect(owner.getByText('Pet e grupo', { exact: true })).toBeVisible();
  await owner.getByPlaceholder('Luna').fill('Luna');
  await owner.getByPlaceholder('Nossa cachorra').fill('Nossa cachorra');
  await owner.getByRole('button', { name: 'So no meu perfil' }).click();
  for (let i = 0; i < 6; i++) {
    if (await owner.getByRole('button', { name: 'Circulo: Casa pet' }).isVisible()) {
      break;
    }
    await owner.getByRole('button', { name: /^Circulo: / }).click();
  }
  await expect(owner.getByRole('button', { name: 'Circulo: Casa pet' })).toBeVisible();
  await owner.getByRole('button', { name: 'Criar perfil' }).click();
  await expect(owner.getByText('Luna', { exact: true })).toBeVisible();
  await owner.getByPlaceholder('uuid da pessoa').fill(memberUser.id);
  await owner.getByRole('button', { name: 'Permissao: visualizar' }).click();
  await expect(owner.getByRole('button', { name: 'Permissao: editar' })).toBeVisible();
  await owner.getByRole('button', { name: 'Conceder edit em Luna' }).click();
  const ownerToken = await owner.evaluate(() => localStorage.getItem('circulos.token'));
  await expect
    .poll(async () => {
      const res = await owner.request.get(`${apiUrl}/api/living-profiles`, {
        headers: { Authorization: `Bearer ${ownerToken}` },
      });
      const items = (await res.json()) as { grants?: { userId: string; access: string }[] }[];
      return items.some((item) =>
        item.grants?.some((grant) => grant.userId === memberUser.id && grant.access === 'edit'),
      );
    })
    .toBeTruthy();
  await member.getByRole('button', { name: 'Circulos' }).click();
  await member.getByRole('button', { name: 'Abrir Casa pet' }).click();
  await expect(member.getByText(/Luna · pet · pode editar/)).toBeVisible();
  await member.getByRole('button', { name: 'Renomear Luna' }).click();
  await expect(member.getByText('Luna editado · pet · pode editar')).toBeVisible();
  await ownerContext.close();
  await memberContext.close();
});

test('tags opcionais na entrada e busca', async ({ page }) => {
  await signUp(page, '-tags');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByText('Diario', { exact: true }).click();
  await page.getByLabel('Titulo', { exact: true }).fill('Treino 5k');
  await page.getByLabel('Tags (opcional)').fill('corrida, manha');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Tags: corrida, manha')).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await expect(page.getByText('O que pede atencao hoje')).toBeVisible();
  await page.getByRole('button', { name: 'Busca' }).first().click();
  await page.getByLabel('Buscar', { exact: true }).fill('corrida');
  await page.getByRole('button', { name: 'Buscar agora' }).click();
  await expect(page.getByText('Treino 5k')).toBeVisible();
});

test('a mesma entrada aparece em dois circulos sem duplicar', async ({ page }) => {
  await signUp(page, '-share-entry');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByText('Diario', { exact: true }).click();
  await page.getByLabel('Titulo', { exact: true }).fill('Corrida 200m');
  await page.getByRole('button', { name: 'Fora do circulo: Eu' }).click();
  await page.getByRole('button', { name: 'Fora do circulo: Familia' }).click();
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Corrida 200m')).toBeVisible();
  await page.getByRole('button', { name: 'Circulos' }).click();
  await page.getByRole('button', { name: 'Abrir Eu' }).click();
  await expect(page.getByText('Entradas compartilhadas', { exact: true })).toBeVisible();
  await expect(page.getByText('Corrida 200m').first()).toBeVisible();
  await expect(page.getByText('A mesma entrada, sem duplicar.')).toBeVisible();
  await page.getByRole('button', { name: 'Circulos' }).click();
  await page.getByRole('button', { name: 'Abrir Familia' }).click();
  await expect(page.getByText('Corrida 200m').first()).toBeVisible();
  await expect(page.getByText('Entradas compartilhadas', { exact: true })).toHaveCount(1);
});

test('compartilhar atalho no circulo mostra de quem veio e o aviso do que mudou', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-atalho-dono');
  const memberContext = await browser.newContext();
  const member = await memberContext.newPage();
  const memberEmail = await signUp(member, '-atalho-filha');
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const memberToken = await member.evaluate(() => localStorage.getItem('circulos.token'));
  await owner.getByRole('button', { name: 'Circulos' }).click();
  await owner.getByLabel('Nome', { exact: true }).fill('Cuidado idoso');
  await owner.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(owner.getByText('Quem esta aqui')).toBeVisible();
  await owner.getByLabel('Convidar por e-mail').fill(memberEmail);
  await owner.getByRole('button', { name: 'Enviar convite' }).click();
  const inviteText = await owner.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Codigo: ', '').trim();
  const accepted = await member.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${memberToken}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();
  await owner.getByRole('button', { name: 'Hoje' }).click();
  await owner.getByLabel('Nome do atalho').fill('Tomei o remedio');
  await owner.getByRole('button', { name: 'Adicionar atalho' }).click();
  await expect(owner.getByRole('button', { name: 'Tomei o remedio', exact: true })).toBeVisible({ timeout: 15_000 });
  await owner.getByRole('button', { name: 'Compartilhar Tomei o remedio em Cuidado idoso' }).click();
  await member.reload();
  await skipTourIfNeeded(member);
  await expect(member.getByRole('button', { name: 'Tomei o remedio', exact: true })).toBeVisible({ timeout: 15_000 });
  await expect(member.getByText('De Pessoa E2E')).toBeVisible();
  await member.getByRole('button', { name: 'Tomei o remedio', exact: true }).click();
  await owner.getByRole('button', { name: 'Circulos' }).click();
  await owner.getByRole('button', { name: 'Abrir Cuidado idoso' }).click();
  await expect(owner.getByText(/disparou Tomei o remedio/)).toBeVisible();
  await owner.getByRole('button', { name: 'Hoje' }).click();
  await owner.getByRole('button', { name: 'Renomear Tomei o remedio' }).click();
  await member.reload();
  await skipTourIfNeeded(member);
  await expect(member.getByText('Nome: Tomei o remedio -> Tomei o remedio (editado)')).toBeVisible({
    timeout: 15_000,
  });
  await ownerContext.close();
  await memberContext.close();
});

test('aviso do atalho some depois de 1 segundo', async ({ page }) => {
  await signUp(page, '-flash');
  await page.getByLabel('Nome do atalho').fill('Tomei o remedio');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  const shortcut = page.getByRole('button', { name: 'Tomei o remedio', exact: true });
  await expect(shortcut).toBeVisible({ timeout: 15_000 });
  await shortcut.click();
  await expect(page.getByTestId('shortcut-flash')).toBeVisible();
  await expect(page.getByTestId('shortcut-flash')).toHaveCount(0, { timeout: 3_000 });
});

test('circulo temporario com data de fim arquiva sozinho', async ({ page }) => {
  await signUp(page, '-fim');
  await page.getByRole('button', { name: 'Circulos' }).click();
  const circleName = `Curso fim ${Date.now()}`;
  await page.getByLabel('Nome', { exact: true }).fill(circleName);
  await page.getByRole('button', { name: 'Tipo: Duradouro' }).click();
  await expect(page.getByRole('button', { name: 'Tipo: Temporario' })).toBeVisible();
  await page.getByLabel('Data de fim').fill('2020-01-01');
  await page.getByRole('button', { name: 'Criar circulo' }).click();
  await expect(page.getByText('Arquivo e retrospectiva ficam visiveis neste app, para qualquer plano.')).toBeVisible({
    timeout: 15_000,
  });
  await expect(page.getByText(/Temporario · archived/)).toBeVisible();
  await expect(page.getByText('Minha retrospectiva')).toBeVisible();
});

test('denuncia de circulo e post fica registrada', async ({ page }) => {
  await signUp(page, '-denuncia');
  await page.getByRole('button', { name: 'Circulos' }).click();
  const circleName = `Denuncia ${Date.now()}`;
  await page.getByLabel('Nome', { exact: true }).fill(circleName);
  await page.getByRole('button', { name: 'Criar circulo' }).click();
  await page.getByLabel('Nova entrada').fill('Post denunciavel');
  await page.getByRole('button', { name: 'Publicar' }).click();
  await expect(page.getByText('Post denunciavel').first()).toBeVisible();
  await page.getByRole('button', { name: 'Denunciar circulo' }).click();
  await expect(page.getByText('Denuncia registrada.')).toBeVisible();
  await page.getByRole('button', { name: 'Denunciar post Post denunciavel' }).click();
  await expect(page.getByText('Denuncia registrada.')).toBeVisible();
});

test('cookie httpOnly na web nao aparece no document.cookie', async ({ page, request }) => {
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const email = `e2e-${Date.now()}-cookie@exemplo.com`;
  const register = await request.post(`${apiUrl}/api/auth/register`, {
    headers: { 'X-Client': 'web' },
    data: { email, password: 'senha-segura-1', name: 'Cookie' },
  });
  expect(register.ok()).toBeTruthy();
  const setCookie = register.headers()['set-cookie'] ?? '';
  expect(setCookie.toLowerCase()).toContain('httponly');
  expect(setCookie).toContain('circulos.refresh');
  const refresh = await request.post(`${apiUrl}/api/auth/refresh`, {
    headers: { 'X-Client': 'web' },
    data: {},
  });
  expect(refresh.ok()).toBeTruthy();
  await signUp(page, '-cookie-doc');
  const visible = await page.evaluate(() => document.cookie);
  expect(visible.includes('circulos.refresh')).toBeFalsy();
});

test('lembrete por e-mail opcional dispara na caixa local', async ({ page }) => {
  await signUp(page, '-email-lembrete');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByRole('button', { name: 'Lembrete por e-mail: so no app' }).click();
  await expect(page.getByRole('button', { name: 'Lembrete por e-mail: ligado' })).toBeVisible();
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const created = await page.request.post(`${apiUrl}/api/reminders`, {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    data: {
      title: 'Remedio das oito',
      startAt: new Date(Date.now() - 2 * 3600_000).toISOString(),
      recurrence: { frequency: 'none', interval: 1 },
    },
  });
  expect(created.ok()).toBeTruthy();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await expect(page.getByText('O que pede atencao hoje')).toBeVisible();
  await expect(page.getByText('Enviamos o lembrete Remedio das oito por e-mail')).toBeVisible({
    timeout: 15_000,
  });
});

test('cadastro mostra circulos prontos e exemplos para confirmar', async ({ page }) => {
  await signUp(page, '-onboarding');
  await expect(page.getByTestId('onboarding-examples')).toBeVisible();
  await expect(page.getByText('Como foi o meu dia')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Confirmar' }).first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Descartar' }).first()).toBeVisible();
  await page.getByRole('button', { name: 'Circulos' }).click();
  await expect(page.getByText('Eu', { exact: true }).first()).toBeVisible();
  await expect(page.getByText('Familia').first()).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await expect(page.getByTestId('on-this-day')).toBeVisible();
  await expect(page.getByText('Neste dia')).toBeVisible();
});

test('descartar exemplos deixa circulos prontos vazios', async ({ page }) => {
  await signUp(page, '-descartar');
  await expect(page.getByTestId('onboarding-examples')).toBeVisible();
  const discards = page.getByRole('button', { name: 'Descartar' });
  await expect(discards).toHaveCount(3);
  await discards.nth(0).click();
  await expect(page.getByRole('button', { name: 'Descartar' })).toHaveCount(2);
  await page.getByRole('button', { name: 'Descartar' }).nth(0).click();
  await expect(page.getByRole('button', { name: 'Descartar' })).toHaveCount(1);
  await page.getByRole('button', { name: 'Descartar' }).nth(0).click();
  await expect(page.getByTestId('onboarding-examples')).toHaveCount(0);
  await page.getByRole('button', { name: 'Circulos' }).click();
  await expect(page.getByRole('button', { name: 'Abrir Eu' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Abrir Familia' })).toBeVisible();
  await page.getByRole('button', { name: 'Abrir Eu' }).click();
  await expect(page.getByText('Quem esta aqui')).toBeVisible();
  await expect(page.getByText('Como foi o meu dia')).toHaveCount(0);
});

test('login nao mostra Google desligado por padrao', async ({ page }) => {
  await page.goto('/(auth)/login');
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Entrar com Google' })).toHaveCount(0);
});

test('convite por link abre o circulo depois do cadastro', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-convite-link');
  await owner.getByRole('button', { name: 'Circulos' }).click();
  await owner.getByRole('button', { name: 'Abrir Familia' }).click();
  await expect(owner.getByText('Quem esta aqui')).toBeVisible();
  await owner.getByRole('button', { name: 'Criar link para WhatsApp' }).click();
  const shareBox = owner.getByTestId('invite-share-text');
  await expect(shareBox).toBeVisible();
  await expect(shareBox).toContainText(/Entre no circulo Familia/);
  const share = await shareBox.innerText();
  const match = share.match(/\/i\/([A-Za-z0-9_-]+)/);
  expect(match).toBeTruthy();
  const inviteToken = match![1];

  const guestContext = await browser.newContext();
  const guest = await guestContext.newPage();
  await guest.goto(`/i/${inviteToken}`);
  await expect(guest.getByText('Familia').first()).toBeVisible();
  await guest.getByRole('button', { name: 'Criar conta e entrar' }).click();
  await guest.getByLabel('Nome', { exact: true }).fill('Convidada');
  await guest.getByLabel('E-mail').fill(`e2e-${Date.now()}-convite@exemplo.com`);
  await guest.getByLabel('Senha').fill('senha-segura-1');
  await guest.getByRole('button', { name: 'Criar conta' }).click();
  const skip = guest.getByRole('button', { name: 'Pular tour' });
  const inside = guest.getByText('Quem esta aqui');
  await expect(skip.or(inside)).toBeVisible({ timeout: 20_000 });
  if (await skip.isVisible()) {
    await skip.click();
  }
  await expect(inside).toBeVisible({ timeout: 20_000 });
  await expect(guest.getByText('Convidada', { exact: true })).toBeVisible();
});

test('privacidade visivel e exportar json', async ({ page }) => {
  await signUp(page, '-privacidade');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await expect(page.getByTestId('privacy-overview')).toBeVisible();
  await expect(page.getByText(/Diario, documentos e vacinas/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Exportar meus dados em JSON' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Enviar mensagem ao suporte' })).toBeVisible();
});

test('leitor de tela acha cadastro pelo papel de botao', async ({ page }) => {
  await page.goto('/(auth)/register');
  await expect(page.getByRole('button', { name: 'Criar conta' })).toBeVisible();
  await expect(page.getByLabel('Nome', { exact: true })).toBeVisible();
  await expect(page.getByLabel('E-mail')).toBeVisible();
});

test('busca no diario por palavra-chave', async ({ page }) => {
  await signUp(page, '-diario-busca');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByText('Diario', { exact: true }).click();
  const word = `pagina-${Date.now()}`;
  await page.getByLabel('Titulo da pagina').fill(word);
  await page.getByLabel('Texto da pagina').fill('texto livre da busca');
  await page.getByRole('button', { name: 'Guardar pagina' }).click();
  await expect(page.getByText(word, { exact: true }).first()).toBeVisible();
  await page.getByLabel('Palavra-chave').fill(word);
  await page.getByRole('button', { name: 'Buscar no diario' }).click();
  await expect(page.getByText(word)).toHaveCount(2);
});

test('ideias de presente: editor adiciona e viewer so ve', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-ideia-dono');
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const ownerToken = await owner.evaluate(() => localStorage.getItem('circulos.token'));
  const circleName = `Presentes ${Date.now()}`;
  const created = await owner.request.post(`${apiUrl}/api/circles`, {
    headers: { Authorization: `Bearer ${ownerToken}`, 'Content-Type': 'application/json' },
    data: { name: circleName, visibility: 'private' },
  });
  expect(created.ok()).toBeTruthy();
  const circle = (await created.json()) as { id: string };

  const editorEmail = `e2e-${Date.now()}-ideia-editor@exemplo.com`;
  const viewerEmail = `e2e-${Date.now()}-ideia-viewer@exemplo.com`;
  const editorRes = await owner.request.post(`${apiUrl}/api/auth/register`, {
    data: { email: editorEmail, password: 'senha-segura-1', name: 'Editor Ideia' },
  });
  const viewerRes = await owner.request.post(`${apiUrl}/api/auth/register`, {
    data: { email: viewerEmail, password: 'senha-segura-1', name: 'Viewer Ideia' },
  });
  expect(editorRes.ok()).toBeTruthy();
  expect(viewerRes.ok()).toBeTruthy();
  const editorAuth = (await editorRes.json()) as { token: string };
  const viewerAuth = (await viewerRes.json()) as { token: string };

  for (const [email, role, token] of [
    [editorEmail, 'editor', editorAuth.token],
    [viewerEmail, 'viewer', viewerAuth.token],
  ] as const) {
    const invite = await owner.request.post(`${apiUrl}/api/circles/${circle.id}/invites`, {
      headers: { Authorization: `Bearer ${ownerToken}`, 'Content-Type': 'application/json' },
      data: { email, role },
    });
    expect(invite.ok()).toBeTruthy();
    const body = (await invite.json()) as { token: string };
    const accepted = await owner.request.post(`${apiUrl}/api/circles/invites/${body.token}/accept`, {
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      data: {},
    });
    expect(accepted.ok()).toBeTruthy();
  }

  const reminder = await owner.request.post(`${apiUrl}/api/reminders`, {
    headers: { Authorization: `Bearer ${ownerToken}`, 'Content-Type': 'application/json' },
    data: {
      title: 'Aniversario da casa',
      kind: 'birthday',
      startAt: new Date(Date.now() + 20 * 24 * 3600_000).toISOString(),
      personName: 'Ana',
      giftIdeas: ['Livro'],
      circleId: circle.id,
    },
  });
  expect(reminder.ok()).toBeTruthy();
  const reminderBody = (await reminder.json()) as { id: string };

  const editorContext = await browser.newContext();
  const editorPage = await editorContext.newPage();
  await editorPage.goto('/(auth)/login');
  await editorPage.getByLabel('E-mail').fill(editorEmail);
  await editorPage.getByLabel('Senha').fill('senha-segura-1');
  await editorPage.getByRole('button', { name: 'Entrar' }).click();
  await skipTourIfNeeded(editorPage);
  await editorPage.getByRole('button', { name: 'Circulos' }).click();
  await editorPage.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await expect(editorPage.getByText('Ideias de presente')).toBeVisible();
  await editorPage.getByLabel('Nova ideia para Aniversario da casa').fill('Planta');
  await editorPage.getByRole('button', { name: 'Adicionar ideia' }).click();
  await expect(editorPage.getByText(/Planta/)).toBeVisible();

  const viewerContext = await browser.newContext();
  const viewerPage = await viewerContext.newPage();
  await viewerPage.goto('/(auth)/login');
  await viewerPage.getByLabel('E-mail').fill(viewerEmail);
  await viewerPage.getByLabel('Senha').fill('senha-segura-1');
  await viewerPage.getByRole('button', { name: 'Entrar' }).click();
  await skipTourIfNeeded(viewerPage);
  await viewerPage.getByRole('button', { name: 'Circulos' }).click();
  await viewerPage.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await expect(viewerPage.getByText('Quem so visualiza nao edita ideias de presente.')).toBeVisible();
  await expect(viewerPage.getByRole('button', { name: 'Adicionar ideia' })).toHaveCount(0);
  const forbidden = await viewerPage.request.post(
    `${apiUrl}/api/circles/${circle.id}/gift-ideas/${reminderBody.id}/ideas`,
    {
      headers: { Authorization: `Bearer ${viewerAuth.token}`, 'Content-Type': 'application/json' },
      data: { idea: 'Chocolate' },
    },
  );
  expect(forbidden.status()).toBe(403);

  await ownerContext.close();
  await editorContext.close();
  await viewerContext.close();
});

