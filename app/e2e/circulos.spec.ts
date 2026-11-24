import { expect, test, type Download, type Page } from '@playwright/test';
import { createHmac } from 'node:crypto';
import { existsSync } from 'node:fs';
import { copyFile, mkdir } from 'node:fs/promises';
import { totpCode } from '../lib/totp';
import { reloadAppRoute, screenshotSteps } from './printCapture';

const artifacts =
  process.env.E2E_ARTIFACTS ??
  (existsSync('/opt/cursor') ? '/opt/cursor/artifacts' : 'test-results/e2e');

async function skipTourIfNeeded(page: Page): Promise<void> {
  const skip = page.getByRole('button', { name: 'Pular tour' });
  const home = page.getByText('O que pede atenção hoje');
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

const WEB_NARROW = 768;

async function waitForAppChrome(page: Page): Promise<void> {
  const width = page.viewportSize()?.width ?? 1280;
  if (width < WEB_NARROW) {
    await expect(page.getByRole('button', { name: 'Mais', exact: true })).toBeVisible();
    return;
  }
  await expect(page.getByRole('button', { name: 'Tipos', exact: true })).toBeVisible();
}

async function settleLayout(page: Page): Promise<void> {
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      }),
  );
}

async function screenshotFull(page: Page, path: string): Promise<void> {
  await screenshotSteps(page, path);
}

async function openTypes(page: Page): Promise<void> {
  const width = page.viewportSize()?.width ?? 1280;
  const tipos = page.getByRole('button', { name: 'Tipos', exact: true }).first();
  if (width < WEB_NARROW && !(await tipos.isVisible())) {
    await page.getByRole('button', { name: 'Mais', exact: true }).click();
  }
  await expect(tipos).toBeVisible();
  await tipos.click();
  await expect(page.getByText('Meus tipos')).toBeVisible();
}

async function cycleMemberAccess(page: Page, from: string, to: string): Promise<void> {
  const button = page.getByRole('button', { name: `Permissão dos membros: ${from}` });
  await expect(button).toBeVisible();
  await button.click();
  await expect(page.getByRole('button', { name: `Permissão dos membros: ${to}` })).toBeVisible();
}

async function downloadFromButton(page: Page, name: string | RegExp): Promise<Download> {
  const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name }).click()]);
  return download;
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

test('cadastro abre o painel do dia, o perfil solo e um círculo', async ({ page }) => {
  await signUp(page);
  await page.screenshot({ path: `${artifacts}/hoje.png`, fullPage: true });

  await openTypes(page);
  await expect(page.getByText('Vacinas', { exact: true })).toBeVisible();
  await expect(page.getByText('Diário', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByLabel('Nome', { exact: true }).fill('Pessoa E2E editada');
  await page.screenshot({ path: `${artifacts}/perfil.png`, fullPage: true });

  await openTypes(page);
  await page.getByText('Vacinas', { exact: true }).click();
  await page.getByLabel('Título', { exact: true }).fill('Hepatite B');
  await page.getByLabel('Quando').fill('2024-03-10');
  await page.getByLabel('Onde').fill('UBS Centro');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Hepatite B')).toBeVisible();

  await page.getByRole('button', { name: 'Círculos' }).click();
  await expect(page.getByRole('button', { name: 'Abrir Família' })).toHaveCount(1);
  await page.getByLabel('Nome', { exact: true }).fill('Família');
  await page.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(page.getByText('Já existe um círculo com este nome.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Abrir Família' })).toHaveCount(1);
  await page.screenshot({ path: `${artifacts}/círculo.png`, fullPage: true });
});

test('atalho de um toque dispara sem confirmação', async ({ page }) => {
  await signUp(page, '-atalho');
  await page.getByLabel('Nome do atalho').fill('Tomei o remédio');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  const shortcut = page.getByRole('button', { name: 'Tomei o remédio', exact: true });
  await expect(shortcut).toBeVisible({ timeout: 15_000 });
  await shortcut.click();
  await expect(page.getByText('Tomei o remédio').first()).toBeVisible();
  await page.screenshot({ path: `${artifacts}/atalho.png`, fullPage: true });
});

test('atalho dispara pela tecla 1 no computador', async ({ page }) => {
  await signUp(page, '-atalho-tecla');
  await page.getByLabel('Nome do atalho').fill('Tomei água');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  const shortcut = page.getByRole('button', { name: 'Tomei água', exact: true });
  await expect(shortcut).toBeVisible({ timeout: 15_000 });
  await page.getByRole('heading', { name: 'O que pede atenção hoje' }).click();
  await page.keyboard.press('1');
  await expect(page.getByTestId('shortcut-flash')).toBeVisible();
  await expect(page.getByTestId('shortcut-flash')).toContainText('Tomei água');
});

test('lembrete de aniversário entra na lista', async ({ page }) => {
  await signUp(page, '-lembrete');
  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await page.getByLabel('Título', { exact: true }).fill('Aniversário da Ana');
  await page.getByRole('button', { name: 'Geral' }).click();
  await page.getByLabel('Quem').fill('Ana');
  await page.getByLabel('Ideias de presente').fill('Livro, planta');
  await expect(page.getByText('Círculo cíclico (opcional). O atalho marca que foi feito.')).toBeVisible();
  await expect(page.getByText('Círculo ciclico')).toHaveCount(0);
  await page.getByRole('button', { name: 'Criar lembrete' }).click();
  await expect(page.getByText('Aniversário da Ana', { exact: true })).toBeVisible();
  await expect(page.getByText(/\d{2}\/\d{2}\/\d{4} \d{2}:\d{2}/)).toBeVisible();
  await expect(page.getByText(/\d{2}:\d{2}:\d{2}/)).toHaveCount(0);
  await page.screenshot({ path: `${artifacts}/lembrete.png`, fullPage: true });
});

test('esqueci a senha avisa que o email saiu', async ({ page }) => {
  const email = await signUp(page, '-senha');
  await page.getByRole('button', { name: 'Sair' }).click();
  await page.goto('/(auth)/forgot');
  await page.getByLabel('E-mail').fill(email);
  await page.getByRole('button', { name: 'Enviar código' }).click();
  await expect(page.getByText('Se existir conta neste e-mail, enviamos o código.')).toBeVisible();
  await page.screenshot({ path: `${artifacts}/esqueci-senha.png`, fullPage: true });
});

test('entrada guarda número e o log do círculo mostra data', async ({ page }) => {
  await signUp(page, '-número');
  await openTypes(page);
  await page.getByText('Diário', { exact: true }).click();
  await page.getByLabel('Título', { exact: true }).fill('Corrida');
  await page.getByRole('button', { name: 'Mais detalhes' }).click();
  await page.getByLabel('Número').fill('200');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Número: 200')).toBeVisible();

  await page.getByRole('button', { name: 'Círculos' }).click();
  await page.getByLabel('Nome', { exact: true }).fill('Família log');
  await page.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(page.getByText('Log', { exact: true })).toBeVisible();
  await expect(page.getByText(/criado:/)).toBeVisible();
  await expect(page.getByText(/\d{2}\/\d{2}\/\d{4}/)).toBeVisible();
});

test('atalho desativado some da tela principal e pode voltar', async ({ page }) => {
  await signUp(page, '-desativar');
  await page.getByLabel('Nome do atalho').fill('Tomei o remédio');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  const shortcut = page.getByRole('button', { name: 'Tomei o remédio', exact: true });
  await expect(shortcut).toBeVisible({ timeout: 15_000 });
  await page.getByRole('button', { name: 'Desativar Tomei o remédio' }).click();
  await expect(page.getByText('Atalhos desativados')).toBeVisible();
  await expect(shortcut).toHaveCount(0);
  await page.getByRole('button', { name: 'Reativar Tomei o remédio' }).click();
  await expect(page.getByRole('button', { name: 'Tomei o remédio', exact: true })).toBeVisible();
});

test('pular lembrete e aviso de recorrência acabando', async ({ page }) => {
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
  await expect(page.getByText('Recorrência acabando')).toBeVisible();
  await page.getByRole('button', { name: 'Pular Ultimas doses' }).click();
  await expect(page.getByRole('button', { name: 'Pular Ultimas doses' })).toBeVisible();
});

test('descobrir círculo e pedir entrada', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-dono-pedido');
  await owner.getByRole('button', { name: 'Círculos' }).click();
  await expect(owner.getByText('Descobrir círculos')).toBeVisible();
  const stamp = Date.now();
  const circleName = `Clube corrida ${stamp}`;
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByLabel('Descrição').fill(`Treinos ${stamp}`);
  await owner.getByRole('button', { name: 'Oculto na busca' }).click();
  await owner.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(owner.getByText('Quem está aqui')).toBeVisible();

  const visitorContext = await browser.newContext();
  const visitor = await visitorContext.newPage();
  await signUp(visitor, '-visitante-pedido');
  await visitor.getByRole('button', { name: 'Círculos' }).click();
  await visitor.getByLabel('Buscar círculos').fill(String(stamp));
  await visitor.getByRole('button', { name: 'Buscar' }).click();
  await visitor.getByRole('button', { name: `Descobrir ${circleName}` }).click();
  await expect(visitor.getByRole('button', { name: 'Publicar' })).toHaveCount(0);
  await expect(visitor.getByText('Quem está aqui')).toBeVisible();
  await visitor.getByRole('button', { name: 'Pedir para entrar' }).click();
  await expect(visitor.getByText('Pedido de entrada enviado.')).toBeVisible();

  await owner.getByRole('button', { name: 'Círculos' }).click();
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
  await expect(page.getByText('O que pede atenção hoje')).toBeVisible({ timeout: 20_000 });
  await page.getByRole('button', { name: 'Perfil' }).click();
  await expect(page.getByRole('button', { name: 'Remover foto' })).toBeVisible();
  await page.getByRole('button', { name: 'Remover foto' }).click();
  await expect(page.getByText('Foto removida.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Remover foto' })).toHaveCount(0);
});

test('transferir dono do círculo em vida', async ({ page }) => {
  await signUp(page, '-dono-vida');
  await page.getByRole('button', { name: 'Círculos' }).click();
  await expect(page.getByText('Descobrir círculos')).toBeVisible();
  await page.getByLabel('Nome', { exact: true }).fill('Cuidado');
  await page.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(page.getByText('Quem está aqui')).toBeVisible();

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
  await expect(page.getByRole('button', { name: 'Copiar código do convite' })).toBeVisible();
  await expect(page.getByTestId('invite-code')).toBeVisible();
  const inviteText = await page.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Código: ', '').trim();
  expect(inviteToken).toMatch(/^[A-HJ-NP-Z2-9]{8}$/);
  await expect(page.getByTestId('invite-code')).toHaveText(`Código do convite: ${inviteToken}`);
  expect(inviteToken).not.toMatch(/[0-9a-f]{32}/i);
  const accepted = await page.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${member.token}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Transferir dono para Sucessor' })).toBeVisible();
  await page.getByRole('button', { name: 'Transferir dono para Sucessor' }).click();
  await expect(page.getByText('Dono transferido. Histórico e permissões ficam com o círculo.')).toBeVisible();
  await expect(page.getByText(/Sucessor · Dono/)).toBeVisible();
});

test('lembrete anual aparece na tela', async ({ page }) => {
  await signUp(page, '-anual');
  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await page.getByLabel('Título', { exact: true }).fill('Consulta anual');
  await page.getByRole('button', { name: 'Recorrência: nenhuma' }).click();
  await page.getByRole('button', { name: 'Recorrência: diária' }).click();
  await page.getByRole('button', { name: 'Recorrência: semanal' }).click();
  await page.getByRole('button', { name: 'Recorrência: mensal' }).click();
  await expect(page.getByRole('button', { name: 'Recorrência: anual' })).toBeVisible();
  await page.getByRole('button', { name: 'Criar lembrete' }).click();
  await expect(page.getByText('Consulta anual', { exact: true })).toBeVisible();
  await expect(page.getByText(/^anual ·/)).toBeVisible();
});

test('férias com prazo escolhido', async ({ page }) => {
  await signUp(page, '-férias');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByLabel('Dias de férias').fill('3');
  await page.getByRole('button', { name: 'Silenciar férias' }).click();
  await expect(page.getByText('Modo férias ligado por 3 dias. Lembretes ficam guardados.')).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await expect(page.getByText(/Modo férias até/)).toBeVisible();
});

test('atalho marca lembrete de círculo como feito', async ({ page }) => {
  await signUp(page, '-atalho-ciclo');
  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await page.getByLabel('Título', { exact: true }).fill('Tomei');
  const reminderCircle = page.getByRole('button', { name: /^(Sem círculo|Eu|Família)$/ });
  for (let i = 0; i < 4; i++) {
    if (await page.getByRole('button', { name: 'Família', exact: true }).isVisible()) {
      break;
    }
    await reminderCircle.click();
  }
  await expect(page.getByRole('button', { name: 'Família', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Criar lembrete' }).click();
  await expect(page.getByText('Tomei', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await page.getByRole('button', { name: 'Sem lembrete de círculo' }).click();
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

test('busca unificada com histórico', async ({ page }) => {
  await signUp(page, '-busca');
  await openTypes(page);
  await page.getByText('Diário', { exact: true }).click();
  await page.getByLabel('Título', { exact: true }).fill('Texto livre da medicação');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Texto livre da medicação')).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await expect(page.getByText('O que pede atenção hoje')).toBeVisible();
  await page.getByRole('button', { name: 'Busca' }).first().click();
  await page.getByLabel('Buscar', { exact: true }).fill('medicação');
  await page.getByRole('button', { name: 'Buscar agora' }).click();
  await expect(page.getByText('Texto livre da medicação')).toBeVisible();
  await expect(page.getByText('diário', { exact: true })).toBeVisible();
  await expect(page.getByText('diary', { exact: true })).toHaveCount(0);
  await expect(page.getByText('Mais usadas')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Buscar medicação' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Buscar recente medicação' })).toBeVisible();
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
  await expect(page.getByText('Política de privacidade')).toBeVisible();
  await expect(page.getByText('Lei Geral de Proteção de Dados')).toBeVisible();
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
  await page.getByLabel('Item', { exact: true }).fill('Leite integral');
  await page.getByRole('button', { name: 'Adicionar item' }).click();
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Listas' }).click();
  await expect(page.getByText('0 de 2 feitos')).toBeVisible();
  await page.getByRole('button', { name: 'Feira', exact: true }).click();
  await page.getByRole('button', { name: 'Marcar Arroz' }).click();
  await expect(page.getByText(/feito por/)).toBeVisible();
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Listas' }).click();
  await expect(page.getByText('1 de 2 feitos')).toBeVisible();
});

test('permissão da lista: só ver e depois marcar feito', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-lista-dono');
  await owner.getByRole('button', { name: 'Círculos' }).click();
  const circleName = `Casa lista ${Date.now()}`;
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(owner.getByText('Listas do círculo')).toBeVisible();

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
  const inviteToken = inviteText.replace('Convite criado. Código: ', '').trim();
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
  await memberPage.getByRole('button', { name: 'Círculos' }).click();
  await memberPage.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await memberPage.getByRole('button', { name: 'Compras da casa', exact: true }).click();
  await expect(memberPage.getByText('Você só vê esta lista.')).toBeVisible();
  await expect(memberPage.getByRole('button', { name: 'Adicionar item' })).toHaveCount(0);
  await expect(memberPage.getByRole('button', { name: 'Marcar Leite' })).toHaveCount(0);

  await cycleMemberAccess(owner, 'só o dono edita', 'membros adicionam');
  await cycleMemberAccess(owner, 'membros adicionam', 'membros marcam feito');
  await expect(memberPage.getByRole('button', { name: 'Marcar Leite' })).toBeVisible({ timeout: 15_000 });
  await memberPage.getByRole('button', { name: 'Marcar Leite' }).click();
  await expect(memberPage.getByText(/feito por/)).toBeVisible();
  await expect(memberPage.getByRole('button', { name: 'Excluir Leite' })).toHaveCount(0);

  await ownerContext.close();
  await memberContext.close();
});

test('modo escuro troca a paleta na hora', async ({ page }) => {
  const pageErrors: string[] = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  await signUp(page, '-tema');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByRole('button', { name: 'Escuro' }).click();
  await expect(page.getByTestId('theme-status')).toContainText('Agora: escuro');
  expect(pageErrors.filter((item) => item.includes('setColorScheme') || item.includes('is not a function'))).toEqual([]);
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
  await page.getByRole('button', { name: 'Avançar' }).click();
  await expect(page.getByText('Painel do dia')).toBeVisible();
  await page.getByRole('button', { name: 'Pular tour' }).click();
  await expect(page.getByText('O que pede atenção hoje')).toBeVisible({ timeout: 20_000 });
  await page.getByRole('button', { name: 'FAQ' }).first().click();
  await expect(page.getByText('O que é o Círculos?')).toBeVisible();
  await expect(page.getByText('Role até achar o assunto. Não há busca nesta lista.')).toBeVisible();
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByRole('button', { name: 'Ver tour de novo' }).click();
  await expect(page.getByText('Seu perfil solo')).toBeVisible();
  await page.getByRole('button', { name: 'Pular tour' }).click();
  await expect(page.getByText('O que pede atenção hoje')).toBeVisible({ timeout: 20_000 });
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
  await expect(page.getByText('O que pede atenção hoje')).toBeVisible({ timeout: 20_000 });
  const reuse = await page.request.post(`${apiUrl}/api/auth/refresh`, {
    headers: { 'X-Client': 'web' },
    data: { refreshToken: refreshCookie?.value },
  });
  expect(reuse.status()).toBe(401);
  await page.getByRole('button', { name: 'Sair' }).click();
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible({ timeout: 20_000 });
});

test('segundo fator com QR exige código no login', async ({ page }) => {
  const email = await signUp(page, '-2fa');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await page.getByRole('button', { name: 'Ativar segundo fator' }).click();
  await expect(page.getByLabel('QR do autenticador')).toBeVisible({ timeout: 15_000 });
  const secretLine = await page.getByText(/^Segredo: /).innerText();
  const secret = secretLine.replace('Segredo: ', '').trim();
  await waitTotpWindow();
  await page.getByLabel('Código do autenticador').fill(await totpCode(secret));
  await page.getByRole('button', { name: 'Confirmar segundo fator' }).click();
  await expect(page.getByText('Segundo fator ativo. Guarde os códigos de recuperação.')).toBeVisible();
  await page.getByRole('button', { name: 'Sair' }).first().click();
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible({ timeout: 20_000 });
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill('senha-segura-1');
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page.getByText(/Informe o código do autenticador/)).toBeVisible();
  await waitTotpWindow();
  await page.getByLabel('Código do autenticador').fill(await totpCode(secret));
  await page.getByRole('button', { name: 'Confirmar código' }).click();
  await skipTourIfNeeded(page);
});

test('offline le o cache, enfileira item e 403 descarta com aviso', async ({ browser }) => {
  test.setTimeout(90_000);
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-off-dono');
  await owner.getByRole('button', { name: 'Círculos' }).click();
  const circleName = `Casa off ${Date.now()}`;
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(owner.getByText('Listas do círculo')).toBeVisible();

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
  const inviteToken = inviteText.replace('Convite criado. Código: ', '').trim();
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
  await cycleMemberAccess(owner, 'só o dono edita', 'membros adicionam');
  await cycleMemberAccess(owner, 'membros adicionam', 'membros marcam feito');

  const memberContext = await browser.newContext();
  const memberPage = await memberContext.newPage();
  await memberPage.goto('/(auth)/login');
  await memberPage.getByLabel('E-mail').fill(memberEmail);
  await memberPage.getByLabel('Senha').fill('senha-segura-1');
  await memberPage.getByRole('button', { name: 'Entrar' }).click();
  await skipTourIfNeeded(memberPage);
  await memberPage.getByRole('button', { name: 'Círculos' }).click();
  await memberPage.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await memberPage.getByRole('button', { name: 'Compras off', exact: true }).click();
  await expect(memberPage.getByRole('button', { name: 'Marcar Leite' })).toBeVisible({ timeout: 15_000 });

  await memberPage.context().setOffline(true);
  await expect(memberPage.getByLabel('Sem rede')).toBeVisible();
  await memberPage.getByRole('button', { name: 'Marcar Leite' }).click();
  await expect(memberPage.getByText(/feito por/)).toBeVisible();

  await cycleMemberAccess(owner, 'membros marcam feito', 'membros editam e excluem');
  await cycleMemberAccess(owner, 'membros editam e excluem', 'só o dono edita');

  await memberPage.context().setOffline(false);
  await memberPage.getByRole('button', { name: 'Sincronizar fila' }).click();
  await expect(memberPage.getByText('Esta lista não permite marcar itens.')).toBeVisible({ timeout: 15_000 });

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
  await expect(page.getByLabel('Sem rede')).toBeVisible();
  await page.getByLabel('Item', { exact: true }).fill('Feijao');
  await page.getByRole('button', { name: 'Adicionar item' }).click();
  await expect(page.getByText('Feijao', { exact: true })).toBeVisible();
  await page.context().setOffline(false);
  await page.getByRole('button', { name: 'Sincronizar fila' }).click();
  await expect(page.getByLabel('Com rede')).toBeVisible();
  await page.reload();
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Listas' }).click();
  await page.getByRole('button', { name: 'Feira off', exact: true }).click();
  await expect(page.getByText('Feijao', { exact: true })).toBeVisible({ timeout: 20_000 });
});

test('album de memórias no perfil e rascunho sobrevive ao rerender', async ({ page }) => {
  await signUp(page, '-memória');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Memórias' }).first().click();
  await page.getByLabel('Título', { exact: true }).fill('Primeiro dia de escola');
  await page.getByLabel('Texto').fill('Foto da entrada');
  await page.getByRole('button', { name: 'Guardar memória' }).click();
  await expect(page.getByText('Primeiro dia de escola', { exact: true })).toBeVisible();
  await page.getByLabel('Texto de Primeiro dia de escola').fill('Texto que a pessoa digitou');
  await page.getByLabel('Título', { exact: true }).fill('outra memória');
  await expect(page.getByLabel('Texto de Primeiro dia de escola')).toHaveValue('Texto que a pessoa digitou');
  await page.getByRole('button', { name: 'Salvar Primeiro dia de escola' }).click();
  await expect(page.getByLabel('Texto de Primeiro dia de escola')).toHaveValue('Texto que a pessoa digitou');
});

test('calendário mostra lembrete e baixa ics', async ({ page }) => {
  await signUp(page, '-cal');
  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const created = await page.request.post(`${apiUrl}/api/reminders`, {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    data: {
      title: 'Tomei o remédio',
      startAt: new Date().toISOString(),
      recurrence: { frequency: 'daily', interval: 1 },
    },
  });
  expect(created.ok()).toBeTruthy();
  await page.getByRole('button', { name: 'Calendário' }).first().click();
  await expect(page.getByText('Tomei o remédio').first()).toBeVisible({ timeout: 15_000 });
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Baixar arquivo de calendário' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/circulos-lembretes\.ics/);
  await expect(page.getByText(/somente leitura/)).toBeVisible();
  await expect(page.getByText(/nunca grava/)).toBeVisible();
  await expect(page.getByText(/Nesta tela web não há eventos do aparelho/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Assinar no seu calendário' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Copiar endereço' })).toBeVisible();
  await expect(page.getByText(/\/api\/calendar\/feed/)).toHaveCount(0);
  await expect(page.getByTestId('month-grid')).toBeVisible();
});

test('modelos prontos copiam lista em português', async ({ page }) => {
  await signUp(page, '-modelo');
  await page.getByRole('button', { name: 'Modelos' }).first().click();
  await expect(page.getByText('Compras da semana', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Usar Compras da semana' }).click();
  await expect(page.getByText('Lista copiada: Compras da semana. Você pode editar os itens.')).toBeVisible();
  await page.getByRole('button', { name: 'Perfil' }).first().click();
  await page.getByRole('button', { name: 'Listas' }).click();
  await expect(page.getByRole('button', { name: 'Compras da semana', exact: true })).toBeVisible();
});

test('perfil de pet no círculo', async ({ page }) => {
  await signUp(page, '-pet');
  await page.getByRole('button', { name: 'Círculos' }).click();
  const circleName = `Casa pet ${Date.now()}`;
  await page.getByLabel('Nome', { exact: true }).fill(circleName);
  await page.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(page.getByText('Quem está aqui')).toBeVisible();
  await page.getByRole('button', { name: 'Perfil' }).first().click();
  await page.getByRole('button', { name: 'Pet e grupo' }).click();
  await expect(page.getByText('Pet e grupo', { exact: true })).toBeVisible();
  await page.getByPlaceholder('ex.: Luna').fill('Luna');
  await page.getByPlaceholder('ex.: Nossa cachorra').fill('Nossa cachorra');
  await page.getByRole('button', { name: 'Só no meu perfil' }).click();
  for (let i = 0; i < 6; i++) {
    if (await page.getByRole('button', { name: `Círculo: ${circleName}` }).isVisible()) {
      break;
    }
    await page.getByRole('button', { name: /^Círculo: / }).click();
  }
  await expect(page.getByRole('button', { name: `Círculo: ${circleName}` })).toBeVisible();
  await page.getByRole('button', { name: 'Criar perfil' }).click();
  await expect(page.getByText('Luna', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Círculos' }).click();
  await page.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await expect(page.getByText('Luna · Pet')).toBeVisible();
});

test('revelação surpresa guarda recordação no perfil', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-rev-dono');
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const honradoEmail = `e2e-${Date.now()}-rev-honrado@exemplo.com`;
  const registered = await owner.request.post(`${apiUrl}/api/auth/register`, {
    data: { email: honradoEmail, password: 'senha-segura-1', name: 'Aniversariante' },
  });
  expect(registered.ok()).toBeTruthy();
  await owner.getByRole('button', { name: 'Círculos' }).click();
  const circleName = `Festa ${Date.now()}`;
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByRole('button', { name: 'Surpresa desligada' }).click();
  await owner.getByLabel('E-mail de quem vai se surpreender').fill(honradoEmail);
  await owner.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(owner.getByText('Quem está aqui')).toBeVisible();
  await owner.getByLabel('Nova entrada').fill('Feliz aniversário');
  await owner.getByRole('button', { name: 'Publicar' }).click();
  await expect(owner.getByText('Feliz aniversário').first()).toBeVisible();
  await owner.getByRole('button', { name: 'Revelar surpresa' }).click();
  await expect(owner.getByText('Resumo da revelação')).toBeVisible();
  await expect(owner.getByText('Feliz aniversário').first()).toBeVisible();

  const honradoContext = await browser.newContext();
  const honrado = await honradoContext.newPage();
  await honrado.goto('/(auth)/login');
  await honrado.getByLabel('E-mail').fill(honradoEmail);
  await honrado.getByLabel('Senha').fill('senha-segura-1');
  await honrado.getByRole('button', { name: 'Entrar' }).click();
  await skipTourIfNeeded(honrado);
  await honrado.getByRole('button', { name: 'Círculos' }).click();
  await honrado.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await expect(honrado.getByText('Resumo da revelação')).toBeVisible();
  await honrado.getByRole('button', { name: 'Guardar recordação no meu perfil' }).click();
  await expect(honrado.getByText('Recordação guardada no seu perfil. A edição é silenciosa.')).toBeVisible();
  await honrado.getByRole('button', { name: 'Perfil', exact: true }).click();
  await honrado.getByRole('button', { name: 'Memórias' }).first().click();
  await expect(honrado.getByText(`Recordação: ${circleName}`, { exact: true })).toBeVisible();
  await ownerContext.close();
  await honradoContext.close();
});

test('página de status pública', async ({ page }) => {
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const health = await page.request.get(`${apiUrl}/health`);
  expect(health.ok()).toBeTruthy();
  await page.goto(`${apiUrl}/status`);
  await expect(page.getByRole('heading', { name: 'Status do Círculos' })).toBeVisible();
  await expect(page.getByText('Telemetria')).toBeVisible();
  await expect(page.getByText('Banco')).toBeVisible();
  await expect(page.getByText('Checagem pública do serviço')).toBeVisible();
  await expect(page.locator('body')).toHaveCSS('align-items', 'center');
  await expect(page.getByText('GET /health')).toHaveCount(0);
  await expect(page.getByText(/\d{2}\/\d{2}\/\d{4} \d{2}:\d{2}/)).toBeVisible();
});

test('recap de uso no perfil só mostra fluxo usado', async ({ page }) => {
  await signUp(page, '-recap');
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Resumo de uso' }).click();
  await expect(page.getByText('Círculos · 2')).toBeVisible();
  await expect(page.getByText('Ainda não há uso para resumir.')).toHaveCount(0);
  await openTypes(page);
  await page.getByText('Vacinas', { exact: true }).click();
  await page.getByLabel('Título', { exact: true }).fill('Hepatite B');
  await page.getByLabel('Quando').fill('2024-03-10');
  await page.getByLabel('Onde').fill('UBS Centro');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Hepatite B')).toBeVisible();
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Resumo de uso' }).click();
  await expect(page.getByText('Vacinas · 1')).toBeVisible();
  await expect(page.getByText('Círculos · 2')).toBeVisible();
  await expect(page.getByText('Diário', { exact: true })).toHaveCount(0);
  await page.context().setOffline(true);
  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Resumo de uso' }).click();
  await expect(page.getByText('Vacinas · 1')).toBeVisible();
  await page.context().setOffline(false);
});

test('círculo temporário arquiva e compartilha a história', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-recap-círculo');
  await owner.getByRole('button', { name: 'Círculos' }).click();
  const circleName = `Curso recap ${Date.now()}`;
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByRole('button', { name: 'Tipo: Duradouro' }).click();
  await expect(owner.getByRole('button', { name: 'Tipo: Temporário' })).toBeVisible();
  await owner.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(owner.getByText('Temporário · Ativo')).toBeVisible();
  await owner.getByLabel('Nova entrada').fill('Terminei o modulo');
  await owner.getByRole('button', { name: 'Publicar' }).click();
  await expect(owner.getByText('Terminei o modulo').first()).toBeVisible();
  await expect(owner.getByText('Minha retrospectiva')).toBeVisible();
  await expect(owner.getByText('Minhas entradas · 1')).toBeVisible();
  await owner.getByRole('button', { name: 'Encerrar ciclo e arquivar' }).click();
  await expect(owner.getByText('Ciclo encerrado. A retrospectiva fica para consulta.', { exact: true })).toBeVisible();
  await expect(owner.getByText('Temporário · Arquivado')).toBeVisible();
  await owner.getByRole('button', { name: 'Compartilhar minha história' }).click();
  await expect(owner.getByText(/História compartilhada: \/r\//)).toBeVisible();
  const shareLine = await owner.getByText(/História compartilhada: \/r\//).innerText();
  const sharePath = shareLine.replace('História compartilhada: ', '').trim();

  const visitorContext = await browser.newContext();
  const visitor = await visitorContext.newPage();
  await visitor.goto(sharePath);
  await expect(visitor.getByText('História compartilhada')).toBeVisible();
  await expect(visitor.getByText('Terminei o modulo')).toBeVisible();
  await expect(visitor.getByText('Pessoa E2E')).toBeVisible();
  await ownerContext.close();
  await visitorContext.close();
});

test('perfil público compartilhavel com privacidade', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-público');
  await owner.getByRole('button', { name: 'Perfil' }).click();
  await expect(owner.getByText('O perfil está privado. Ninguém de fora vê nome nem foto.')).toBeVisible();
  await owner.getByRole('button', { name: 'Perfil privado' }).click();
  await owner.getByRole('button', { name: 'Salvar perfil' }).click();
  await expect(owner.getByText('Perfil atualizado.')).toBeVisible();
  await expect(owner.getByText(/Link público: \/p\//)).toBeVisible();
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
  await expect(visitor.getByText('Perfil público')).toBeVisible();
  await expect(visitor.getByText('Pessoa E2E', { exact: true })).toBeVisible();
  await expect(visitor.getByText('Quem visita vê nome, foto e descrição. Telefone, e-mail e diário ficam fora.')).toBeVisible();
  await expect(visitor.getByLabel('Telefone')).toHaveCount(0);

  await owner.getByRole('button', { name: 'Perfil público' }).click();
  await owner.getByRole('button', { name: 'Salvar perfil' }).click();
  await expect(owner.getByText('O perfil está privado. Ninguém de fora vê nome nem foto.')).toBeVisible();
  await visitor.reload();
  await expect(visitor.getByText('Perfil não encontrado.')).toBeVisible();
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

test('diário mostra exemplo de preenchimento', async ({ page }) => {
  await signUp(page, '-exemplo');
  await openTypes(page);
  await page.getByText('Diário', { exact: true }).click();
  await expect(page.getByText('Exemplo: um parágrafo do dia. Busca por palavra-chave. Sem versão.')).toBeVisible();
  await expect(page.getByPlaceholder('ex.: Corrida de manhã')).toBeVisible();
});

test('backup do perfil e gráfico de armazenamento', async ({ page }) => {
  await signUp(page, '-backup');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await expect(page.getByText('Uso de armazenamento')).toBeVisible();
  await expect(page.getByLabel(/Uso de armazenamento/)).toBeVisible();
  await expect(page.getByText(/Sem separar por tipo/)).toBeVisible();
  const download = await downloadFromButton(page, 'Exportar meu perfil');
  expect(download.suggestedFilename()).toMatch(/circulos-perfil/);
  await expect(page.getByText('Cópia do perfil baixada.')).toBeVisible();
  await page.getByRole('button', { name: 'Círculos' }).click();
  await page.getByLabel('Nome', { exact: true }).fill('Casa backup');
  await page.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(page.getByText('Quem está aqui')).toBeVisible();
  const exportCircle = page.getByRole('button', { name: 'Exportar este círculo' });
  await exportCircle.scrollIntoViewIfNeeded();
  await expect(exportCircle).toBeVisible();
  const circleFile = await downloadFromButton(page, 'Exportar este círculo');
  expect(circleFile.suggestedFilename()).toMatch(/circulos-/);
  await expect(page.getByText('Cópia do círculo baixada.')).toBeVisible();
});

test('assinatura simulada e webhook', async ({ page }) => {
  await signUp(page, '-plano');
  await page.goto('/pricing');
  await expect(page.getByText('Preços aprovados pelo Allan em 22/11/2026')).toBeVisible();
  await page.getByRole('button', { name: 'Assinar mensal', exact: true }).click();
  await expect(page.getByText('Assinatura ativa.')).toBeVisible();
  await expect(page.getByText('Ambiente de desenvolvimento. Sem cobrança real.')).toBeVisible();
  await expect(page.getByText(/sandbox/i)).toHaveCount(0);
  await expect(page.getByText('Simulated')).toHaveCount(0);
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
  await expect(page.getByText('Status: Ativa. Em dia. Assinatura ativa.')).toBeVisible();
  await expect(page.getByText('Cobrança em ambiente de desenvolvimento.')).toBeVisible();
});

test('aceitar posse oferecida no círculo sem dono', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-posse-dono');
  const memberContext = await browser.newContext();
  const member = await memberContext.newPage();
  const memberEmail = await signUp(member, '-posse-membro');
  const circleName = `Casa posse ${Date.now()}`;
  await owner.getByRole('button', { name: 'Círculos' }).click();
  await owner.getByRole('button', { name: 'Privado' }).click();
  await owner.getByLabel('Nome', { exact: true }).fill(circleName);
  await owner.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(owner.getByText(circleName).first()).toBeVisible();
  await owner.getByLabel('Convidar por e-mail').fill(memberEmail);
  await owner.getByRole('button', { name: 'Enviar convite' }).click();
  const inviteText = await owner.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Código: ', '').trim();
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const memberToken = await member.evaluate(() => localStorage.getItem('circulos.token'));
  const accepted = await member.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${memberToken}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();
  const circlesRes = await member.request.get(`${apiUrl}/api/circles`, {
    headers: { Authorization: `Bearer ${memberToken}` },
  });
  expect(circlesRes.ok()).toBeTruthy();
  const circles = (await circlesRes.json()) as { id: string; name: string | null }[];
  const found = circles.find((item) => item.name === circleName);
  expect(found?.id).toBeTruthy();
  const circleId = found!.id;
  await owner.getByRole('button', { name: 'Sair e abrir votação' }).click();
  await expect(owner.getByRole('button', { name: 'Criar círculo' })).toBeVisible();
  await expect.poll(async () => {
    const due = await member.request.post(`${apiUrl}/dev/circles/${circleId}/ownership-due`, {
      headers: { Authorization: `Bearer ${memberToken}` },
    });
    return due.status();
  }).toBe(204);
  await member.getByRole('button', { name: 'Hoje' }).click();
  await expect(member.getByText(`O círculo ${circleName} oferece a posse para você.`)).toBeVisible({
    timeout: 15_000,
  });
  await member.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await expect(member.getByText('Oferta de posse', { exact: true })).toBeVisible();
  await member.getByRole('button', { name: 'Aceitar posse' }).click();
  await expect(member.getByText('Você assumiu a posse deste círculo.')).toBeVisible();
  await expect(member.getByText('Pessoa E2E · Dono', { exact: true })).toBeVisible();
  await ownerContext.close();
  await memberContext.close();
});

test('votação de sucessão com prazo', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-voto-dono');
  const memberContext = await browser.newContext();
  const member = await memberContext.newPage();
  const memberEmail = await signUp(member, '-voto-membro');
  await owner.getByRole('button', { name: 'Círculos' }).click();
  await owner.getByRole('button', { name: 'Privado' }).click();
  await owner.getByLabel('Nome', { exact: true }).fill('Casa voto');
  await owner.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(owner.getByText('Casa voto').first()).toBeVisible();
  await owner.getByLabel('Convidar por e-mail').fill(memberEmail);
  await owner.getByRole('button', { name: 'Enviar convite' }).click();
  const inviteText = await owner.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Código: ', '').trim();
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const memberToken = await member.evaluate(() => localStorage.getItem('circulos.token'));
  const accepted = await member.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${memberToken}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();
  await owner.getByRole('button', { name: 'Sair e abrir votação' }).click();
  await member.getByRole('button', { name: 'Círculos' }).click();
  await member.getByRole('button', { name: 'Abrir Casa voto' }).click();
  await expect(member.getByText('Votação de sucessão')).toBeVisible();
  await expect(member.getByText(/Prazo de 3 dias/)).toBeVisible();
  await member.getByRole('button', { name: /Votar em/ }).first().click();
  await expect(member.getByText(/Status: (aberta|encerrada|concluída)/)).toBeVisible();
  await expect(member.getByText('O dono eleito só pode ser substituído após 30 dias da eleição.')).toBeVisible();
  await ownerContext.close();
  await memberContext.close();
});

test('busca por raio com e sem permissão', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-geo-dono');
  const seekerContext = await browser.newContext();
  const seeker = await seekerContext.newPage();
  await signUp(seeker, '-geo-busca');
  await seeker.getByRole('button', { name: 'Busca', exact: true }).first().click();
  await seeker.getByRole('button', { name: 'Buscar por perto' }).click();
  await expect(seeker.getByText('Ninguém por perto com permissão.')).toBeVisible();
  const geoName = `Geo ${Date.now()}`;
  await owner.getByRole('button', { name: 'Círculos' }).click();
  await owner.getByLabel('Nome', { exact: true }).fill(geoName);
  await owner.getByRole('button', { name: 'Não aparecer na busca por perto' }).click();
  await owner.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(owner.getByText(geoName).first()).toBeVisible();
  await seeker.getByRole('button', { name: 'Buscar por perto' }).click();
  await expect(seeker.getByText(`${geoName} · círculo`, { exact: false })).toBeVisible();
  await expect(seeker.getByText(`${geoName} · person`, { exact: false })).toHaveCount(0);
  await ownerContext.close();
  await seekerContext.close();
});

test('perfil público mostra descrição', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-bio');
  await owner.getByRole('button', { name: 'Perfil' }).click();
  await owner.getByLabel('Descrição pública').fill('Corro de manhã');
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
  await expect(visitor.getByText('Corro de manhã')).toBeVisible();
  await ownerContext.close();
  await visitorContext.close();
});

test('exclusão definitiva do círculo', async ({ page }) => {
  await signUp(page, '-apagar');
  await page.getByRole('button', { name: 'Círculos' }).click();
  await page.getByLabel('Nome', { exact: true }).fill('Apagar já');
  await page.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(page.getByText('Exclusão definitiva. Não dá para desfazer.')).toBeVisible();
  await page.getByLabel('Confirmação da exclusão').fill('excluir definitivo');
  await page.getByRole('button', { name: 'Excluir círculo de vez' }).click();
  await expect(page.getByText('Os seus')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Abrir Apagar já' })).toHaveCount(0);
});

test('permissão de pet e grupo', async ({ browser }) => {
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
  await owner.getByRole('button', { name: 'Círculos' }).click();
  await owner.getByLabel('Nome', { exact: true }).fill('Casa pet');
  await owner.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(owner.getByText('Quem está aqui')).toBeVisible();
  await owner.getByLabel('Convidar por e-mail').fill(memberEmail);
  await owner.getByRole('button', { name: 'Enviar convite' }).click();
  const inviteText = await owner.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Código: ', '').trim();
  const accepted = await member.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${memberToken}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();
  await owner.getByRole('button', { name: 'Perfil' }).first().click();
  await owner.getByRole('button', { name: 'Pet e grupo' }).click();
  await expect(owner.getByText('Pet e grupo', { exact: true })).toBeVisible();
  await owner.getByPlaceholder('ex.: Luna').fill('Luna');
  await owner.getByPlaceholder('ex.: Nossa cachorra').fill('Nossa cachorra');
  await owner.getByRole('button', { name: 'Só no meu perfil' }).click();
  for (let i = 0; i < 6; i++) {
    if (await owner.getByRole('button', { name: 'Círculo: Casa pet' }).isVisible()) {
      break;
    }
    await owner.getByRole('button', { name: /^Círculo: / }).click();
  }
  await expect(owner.getByRole('button', { name: 'Círculo: Casa pet' })).toBeVisible();
  await owner.getByRole('button', { name: 'Criar perfil' }).click();
  await expect(owner.getByText('Luna', { exact: true })).toBeVisible();
  await owner.getByPlaceholder('código da pessoa').fill(memberUser.id);
  await owner.getByRole('button', { name: 'Permissão: visualizar' }).click();
  await expect(owner.getByRole('button', { name: 'Permissão: editar' })).toBeVisible();
  await owner.getByRole('button', { name: 'Conceder editar em Luna' }).click();
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
  await member.getByRole('button', { name: 'Círculos' }).click();
  await member.getByRole('button', { name: 'Abrir Casa pet' }).click();
  await expect(member.getByText(/Luna · Pet · pode editar/)).toBeVisible();
  await member.getByRole('button', { name: 'Renomear Luna' }).click();
  await expect(member.getByText('Luna editado · Pet · pode editar')).toBeVisible();
  await ownerContext.close();
  await memberContext.close();
});

test('tags opcionais na entrada e busca', async ({ page }) => {
  await signUp(page, '-tags');
  await openTypes(page);
  await page.getByText('Diário', { exact: true }).click();
  await page.getByLabel('Título', { exact: true }).fill('Treino 5k');
  await page.getByRole('button', { name: 'Mais detalhes' }).click();
  await page.getByLabel('Tags (opcional)').fill('corrida, manhã');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Tags: corrida, manhã')).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await expect(page.getByText('O que pede atenção hoje')).toBeVisible();
  await page.getByRole('button', { name: 'Busca' }).first().click();
  await page.getByLabel('Buscar', { exact: true }).fill('corrida');
  await page.getByRole('button', { name: 'Buscar agora' }).click();
  await expect(page.getByText('Treino 5k')).toBeVisible();
});

test('a mesma entrada aparece em dois círculos sem duplicar', async ({ page }) => {
  await signUp(page, '-share-entry');
  await openTypes(page);
  await page.getByText('Diário', { exact: true }).click();
  await page.getByLabel('Título', { exact: true }).fill('Corrida 200m');
  await page.getByRole('button', { name: 'Compartilhar com Eu', exact: true }).click();
  await page.getByRole('button', { name: 'Compartilhar com Família', exact: true }).click();
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Corrida 200m')).toBeVisible();
  await page.getByRole('button', { name: 'Círculos' }).click();
  await page.getByRole('button', { name: 'Abrir Eu' }).click();
  await expect(page.getByText('Entradas compartilhadas', { exact: true })).toBeVisible();
  await expect(page.getByText('Corrida 200m').first()).toBeVisible();
  await expect(page.getByText('A mesma entrada, sem duplicar.')).toBeVisible();
  await page.getByRole('button', { name: 'Círculos' }).click();
  await page.getByRole('button', { name: 'Abrir Família' }).click();
  await expect(page.getByText('Corrida 200m').first()).toBeVisible();
  await expect(page.getByText('Entradas compartilhadas', { exact: true })).toHaveCount(1);
});

test('compartilhar atalho no círculo mostra de quem veio e o aviso do que mudou', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-atalho-dono');
  const memberContext = await browser.newContext();
  const member = await memberContext.newPage();
  const memberEmail = await signUp(member, '-atalho-filha');
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const memberToken = await member.evaluate(() => localStorage.getItem('circulos.token'));
  await owner.getByRole('button', { name: 'Círculos' }).click();
  await owner.getByLabel('Nome', { exact: true }).fill('Cuidado idoso');
  await owner.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(owner.getByText('Quem está aqui')).toBeVisible();
  await owner.getByLabel('Convidar por e-mail').fill(memberEmail);
  await owner.getByRole('button', { name: 'Enviar convite' }).click();
  const inviteText = await owner.getByText(/Convite criado/).innerText();
  const inviteToken = inviteText.replace('Convite criado. Código: ', '').trim();
  const accepted = await member.request.post(`${apiUrl}/api/circles/invites/${inviteToken}/accept`, {
    headers: { Authorization: `Bearer ${memberToken}`, 'Content-Type': 'application/json' },
    data: {},
  });
  expect(accepted.ok()).toBeTruthy();
  await owner.getByRole('button', { name: 'Hoje' }).click();
  await owner.getByLabel('Nome do atalho').fill('Tomei o remédio');
  await owner.getByRole('button', { name: 'Adicionar atalho' }).click();
  await expect(owner.getByRole('button', { name: 'Tomei o remédio', exact: true })).toBeVisible({ timeout: 15_000 });
  await owner.getByRole('button', { name: 'Compartilhar Tomei o remédio em Cuidado idoso' }).click();
  await member.reload();
  await skipTourIfNeeded(member);
  await expect(member.getByRole('button', { name: 'Tomei o remédio', exact: true })).toBeVisible({ timeout: 15_000 });
  await expect(member.getByText('De Pessoa E2E')).toBeVisible();
  await member.getByRole('button', { name: 'Tomei o remédio', exact: true }).click();
  await owner.getByRole('button', { name: 'Círculos' }).click();
  await owner.getByRole('button', { name: 'Abrir Cuidado idoso' }).click();
  await expect(owner.getByText(/disparou Tomei o remédio/)).toBeVisible();
  await owner.getByRole('button', { name: 'Hoje' }).click();
  await owner.getByRole('button', { name: 'Renomear Tomei o remédio' }).click();
  await member.reload();
  await skipTourIfNeeded(member);
  await expect(member.getByText('Nome: Tomei o remédio -> Tomei o remédio (editado)')).toBeVisible({
    timeout: 15_000,
  });
  await ownerContext.close();
  await memberContext.close();
});

test('aviso do atalho some depois de 1 segundo', async ({ page }) => {
  await signUp(page, '-flash');
  await page.getByLabel('Nome do atalho').fill('Tomei o remédio');
  await page.getByRole('button', { name: 'Adicionar atalho' }).click();
  const shortcut = page.getByRole('button', { name: 'Tomei o remédio', exact: true });
  await expect(shortcut).toBeVisible({ timeout: 15_000 });
  await shortcut.click();
  await expect(page.getByTestId('shortcut-flash')).toBeVisible();
  await expect(page.getByTestId('shortcut-flash')).toHaveCount(0, { timeout: 3_000 });
});

test('círculo temporário com data de fim arquiva sozinho', async ({ page }) => {
  await signUp(page, '-fim');
  await page.getByRole('button', { name: 'Círculos' }).click();
  const circleName = `Curso fim ${Date.now()}`;
  await page.getByLabel('Nome', { exact: true }).fill(circleName);
  await page.getByRole('button', { name: 'Tipo: Duradouro' }).click();
  await expect(page.getByRole('button', { name: 'Tipo: Temporário' })).toBeVisible();
  await page.getByLabel('Data de fim').fill('2020-01-01');
  await page.getByRole('button', { name: 'Criar círculo' }).click();
  await expect(page.getByText('Arquivo e retrospectiva ficam visíveis neste app, para qualquer plano.')).toBeVisible({
    timeout: 15_000,
  });
  await expect(page.getByText(/Temporário · Arquivado/)).toBeVisible();
  await expect(page.getByText('Minha retrospectiva')).toBeVisible();
});

test('denúncia de círculo e post fica registrada', async ({ page }) => {
  await signUp(page, '-denúncia');
  await page.getByRole('button', { name: 'Círculos' }).click();
  const circleName = `Denúncia ${Date.now()}`;
  await page.getByLabel('Nome', { exact: true }).fill(circleName);
  await page.getByRole('button', { name: 'Criar círculo' }).click();
  await page.getByLabel('Nova entrada').fill('Post denunciavel');
  await page.getByRole('button', { name: 'Publicar' }).click();
  await expect(page.getByText('Post denunciavel').first()).toBeVisible();
  await page.getByRole('button', { name: 'Denunciar círculo' }).click();
  await expect(page.getByText('Denúncia registrada.')).toBeVisible();
  await page.getByRole('button', { name: 'Denunciar post Post denunciavel' }).click();
  await expect(page.getByText('Denúncia registrada.')).toBeVisible();
});

test('cookie httpOnly na web não aparece no document.cookie', async ({ page, request }) => {
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
  await page.getByRole('button', { name: 'Lembrete por e-mail: só no app' }).click();
  await expect(page.getByRole('button', { name: 'Lembrete por e-mail: ligado' })).toBeVisible();
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const created = await page.request.post(`${apiUrl}/api/reminders`, {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    data: {
      title: 'Remédio das oito',
      startAt: new Date(Date.now() - 2 * 3600_000).toISOString(),
      recurrence: { frequency: 'none', interval: 1 },
    },
  });
  expect(created.ok()).toBeTruthy();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await expect(page.getByText('O que pede atenção hoje')).toBeVisible();
  await expect(page.getByText('Enviamos o lembrete Remédio das oito por e-mail')).toBeVisible({
    timeout: 15_000,
  });
});

test('cadastro mostra círculos prontos e exemplos para confirmar', async ({ page }) => {
  await signUp(page, '-onboarding');
  await expect(page.getByTestId('onboarding-examples')).toBeVisible();
  await expect(page.getByText('Como foi o meu dia')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Confirmar' }).first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Descartar' }).first()).toBeVisible();
  await page.getByRole('button', { name: 'Círculos' }).click();
  await expect(page.getByText('Eu', { exact: true }).first()).toBeVisible();
  await expect(page.getByText('Família').first()).toBeVisible();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await expect(page.getByTestId('on-this-day')).toBeVisible();
  await expect(page.getByText('Neste dia')).toBeVisible();
});

test('descartar exemplos deixa círculos prontos vazios', async ({ page }) => {
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
  await page.getByRole('button', { name: 'Círculos' }).click();
  await expect(page.getByRole('button', { name: 'Abrir Eu' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Abrir Família' })).toBeVisible();
  await page.getByRole('button', { name: 'Abrir Eu' }).click();
  await expect(page.getByText('Quem está aqui')).toBeVisible();
  await expect(page.getByText('Como foi o meu dia')).toHaveCount(0);
});

test('login não mostra Google desligado por padrão', async ({ page }) => {
  await page.goto('/(auth)/login');
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Entrar com Google' })).toHaveCount(0);
});

test('convite por link abre o círculo depois do cadastro', async ({ browser }) => {
  const ownerContext = await browser.newContext();
  const owner = await ownerContext.newPage();
  await signUp(owner, '-convite-link');
  await owner.getByRole('button', { name: 'Círculos' }).click();
  await owner.getByRole('button', { name: 'Abrir Família' }).click();
  await expect(owner.getByText('Quem está aqui')).toBeVisible();
  await owner.getByRole('button', { name: 'Criar link para WhatsApp' }).click();
  const shareBox = owner.getByTestId('invite-share-text');
  await expect(shareBox).toBeVisible();
  await expect(shareBox).toContainText(/Entre no círculo Família/);
  const share = await shareBox.innerText();
  const match = share.match(/\/i\/([A-Za-z0-9_-]+)/);
  expect(match).toBeTruthy();
  const inviteToken = match![1];

  const guestContext = await browser.newContext();
  const guest = await guestContext.newPage();
  await guest.goto(`/i/${inviteToken}`);
  await expect(guest.getByText('Família').first()).toBeVisible();
  await guest.getByRole('button', { name: 'Criar conta e entrar' }).click();
  await guest.getByLabel('Nome', { exact: true }).fill('Convidada');
  await guest.getByLabel('E-mail').fill(`e2e-${Date.now()}-convite@exemplo.com`);
  await guest.getByLabel('Senha').fill('senha-segura-1');
  await guest.getByRole('button', { name: 'Criar conta' }).click();
  const skip = guest.getByRole('button', { name: 'Pular tour' });
  const inside = guest.getByText('Quem está aqui');
  await expect(skip.or(inside)).toBeVisible({ timeout: 20_000 });
  if (await skip.isVisible()) {
    await skip.click();
  }
  await expect(inside).toBeVisible({ timeout: 20_000 });
  await expect(guest.getByText('Convidada', { exact: true })).toBeVisible();
});

test('privacidade visível e exportar json', async ({ page }) => {
  await signUp(page, '-privacidade');
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await expect(page.getByTestId('privacy-overview')).toBeVisible();
  await expect(page.getByText(/Registros da sua vida/)).toBeVisible();
  await expect(page.getByText(/áudio/i)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Exportar meus dados em JSON' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Enviar mensagem ao suporte' })).toBeVisible();
});

test('leitor de tela acha cadastro pelo papel de botao', async ({ page }) => {
  await page.goto('/(auth)/register');
  await expect(page.getByRole('button', { name: 'Criar conta' })).toBeVisible();
  await expect(page.getByLabel('Nome', { exact: true })).toBeVisible();
  await expect(page.getByLabel('E-mail')).toBeVisible();
});

test('busca no diário por palavra-chave', async ({ page }) => {
  await signUp(page, '-diário-busca');
  await openTypes(page);
  await page.getByText('Diário', { exact: true }).click();
  const word = `página-${Date.now()}`;
  await page.getByLabel('Título da página').fill(word);
  await page.getByLabel('Texto da página').fill('texto livre da busca');
  await page.getByRole('button', { name: 'Guardar página' }).click();
  await expect(page.getByText(word, { exact: true }).first()).toBeVisible();
  await page.getByLabel('Palavra-chave').fill(word);
  await page.getByRole('button', { name: 'Buscar no diário' }).click();
  await expect(page.getByText(word)).toHaveCount(2);
});

test('ideias de presente: editor adiciona e viewer só ve', async ({ browser }) => {
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
      title: 'Aniversário da casa',
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
  await editorPage.getByRole('button', { name: 'Círculos' }).click();
  await editorPage.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await expect(editorPage.getByText('Ideias de presente')).toBeVisible();
  await editorPage.getByLabel('Nova ideia para Aniversário da casa').fill('Planta');
  await editorPage.getByRole('button', { name: 'Adicionar ideia' }).click();
  await expect(editorPage.getByText(/Planta/)).toBeVisible();

  const viewerContext = await browser.newContext();
  const viewerPage = await viewerContext.newPage();
  await viewerPage.goto('/(auth)/login');
  await viewerPage.getByLabel('E-mail').fill(viewerEmail);
  await viewerPage.getByLabel('Senha').fill('senha-segura-1');
  await viewerPage.getByRole('button', { name: 'Entrar' }).click();
  await skipTourIfNeeded(viewerPage);
  await viewerPage.getByRole('button', { name: 'Círculos' }).click();
  await viewerPage.getByRole('button', { name: `Abrir ${circleName}` }).click();
  await expect(viewerPage.getByText('Quem só visualiza não edita ideias de presente.')).toBeVisible();
  await expect(viewerPage.getByRole('button', { name: 'Adicionar ideia' })).toHaveCount(0);
  await expect(viewerPage.getByRole('button', { name: 'Publicar' })).toHaveCount(0);
  await expect(editorPage.getByRole('button', { name: 'Publicar' })).toBeVisible();
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

test('tipo por frase, galeria, captura livre, voz, calendario, lembrete, share e migracao', async ({ page }) => {
  test.setTimeout(120_000);
  await signUp(page, '-flex');
  await openTypes(page);
  await page.getByLabel('Descrever um tipo').fill('quero anotar as vacinas do meu filho, com data, posto e lote');
  await page.getByRole('button', { name: 'Sugerir tipo' }).click();
  await expect(page.getByLabel('Nome do tipo')).toHaveValue('Vacinas');
  await expect(page.getByText('Campos sugeridos')).toBeVisible();
  await expect(page.getByText('Nome de Título')).toHaveCount(0);
  await expect(page.getByRole('button', { name: /Campo ativo: Quando/ })).toBeVisible();
  await page.getByRole('button', { name: 'Usar esta sugestão' }).click();
  await expect(page.getByRole('button', { name: 'Guardar entrada' })).toBeVisible();
  await openTypes(page);
  await page.getByText('Pet', { exact: true }).click();
  await expect(page.getByRole('button', { name: 'Guardar entrada' })).toBeVisible();

  await page.getByRole('button', { name: 'Hoje' }).click();
  await page.getByLabel('Escrever').fill('Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro');
  await page.getByRole('button', { name: 'Sugerir' }).click();
  await expect(page.getByText(/Sugerimos/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Aceitar' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Editar' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Recusar' })).toBeVisible();
  const acceptBox = await page.getByRole('button', { name: 'Aceitar' }).boundingBox();
  expect(acceptBox).toBeTruthy();
  expect(acceptBox!.y + acceptBox!.height).toBeLessThanOrEqual((page.viewportSize()?.height ?? 800) + 1);
  await page.getByRole('button', { name: 'Recusar' }).click();
  await expect(page.getByText('Nada foi guardado. Você recusou a sugestão.')).toBeVisible();
  await page.getByLabel('Escrever').fill('Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro');
  await page.getByRole('button', { name: 'Sugerir' }).click();
  await page.getByRole('button', { name: 'Aceitar' }).click();
  await expect(page.getByText('Registro guardado.')).toBeVisible();

  await page.getByTestId('voice-file').setInputFiles('e2e/fixtures/teste-voz.txt');
  await expect(page.getByText(/Transcrição:/)).toBeVisible();
  await expect(page.getByText('teste-voz.txt')).toHaveCount(0);
  await expect(page.getByText(/\.wav/i)).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Aceitar' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Editar' })).toBeVisible();
  await page.getByRole('button', { name: 'Recusar' }).click();

  await openTypes(page);
  await page.getByText('Vacinas', { exact: true }).first().click();
  await page.getByLabel('Título', { exact: true }).fill('Gripe');
  await page.getByLabel('Quando').fill('2026-11-10');
  await page.getByLabel('Onde').fill('UBS Centro');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Gripe').first()).toBeVisible();
  await page.getByRole('button', { name: 'Calendário deste tipo' }).click();
  await expect(page.getByText(/Gripe/)).toBeVisible();
  await page.getByRole('button', { name: 'Lembrar desta data' }).first().click();
  await page.getByRole('button', { name: 'Hoje' }).click();
  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await expect(page.getByText('Gripe').first()).toBeVisible();

  await openTypes(page);
  await page.getByText('Diário', { exact: true }).first().click();
  await page.getByLabel('Título', { exact: true }).fill('Corrida 200m flex');
  await page.getByRole('button', { name: 'Compartilhar com Eu', exact: true }).click();
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByText('Corrida 200m flex')).toBeVisible();
  await page.getByRole('button', { name: 'Círculos' }).click();
  await page.getByRole('button', { name: 'Abrir Eu' }).click();
  await expect(page.getByText('Entradas compartilhadas', { exact: true })).toBeVisible();
  await expect(page.getByText('Corrida 200m flex').first()).toBeVisible();

  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const area = await page.request.post(`${apiUrl}/api/life-areas`, {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    data: { name: 'Vacina antiga' },
  });
  expect(area.ok()).toBeTruthy();
  const areaBody = (await area.json()) as { id: string };
  const entry = await page.request.post(`${apiUrl}/api/life-areas/${areaBody.id}/entries`, {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    data: { title: 'Hepatite migrada', occurredOn: '2026-02-01', location: 'UBS Centro' },
  });
  expect(entry.ok()).toBeTruthy();
  await openTypes(page);
  await page.getByText('Vacina antiga', { exact: true }).click();
  await expect(page.getByText('Hepatite migrada')).toBeVisible();
});

test('documentos pessoais tem rotulo de baixar e excluir', async ({ page }) => {
  await signUp(page, '-docs-a11y');
  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const uploaded = await page.request.post(`${apiUrl}/api/documents`, {
    headers: { Authorization: `Bearer ${token}` },
    multipart: {
      file: { name: 'nota.txt', mimeType: 'text/plain', buffer: Buffer.from('texto da nota') },
    },
  });
  expect(uploaded.ok()).toBeTruthy();
  await page.getByRole('button', { name: 'Documentos' }).click();
  await expect(page.getByRole('button', { name: 'Baixar nota.txt' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Excluir nota.txt' })).toBeVisible();
  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await expect(page.getByText(/Encountered two children/)).toHaveCount(0);
  await expect(page.getByText(/Inadimplente/)).toHaveCount(0);
  await expect(page.getByText(/Plano gratuito/)).toBeVisible();
  await expect(page.getByText(/0 KB de/)).toHaveCount(0);
  await expect(page.getByText(/B de /)).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  await waitForAppChrome(page);
  await expect(page.getByRole('button', { name: 'Hoje', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Círculos', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Perfil', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Mais', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Mais', exact: true }).click();
  await page.getByRole('button', { name: 'Documentos', exact: true }).click();
  await expect(page.getByText('TAMANHOENVIADOAÇÕES')).toHaveCount(0);
  await expect(page.getByText('nota.txt')).toBeVisible();
});

test('arquivo em pt-BR, captura rápida, corrida, calendário e gráfico', async ({ page }) => {
  test.setTimeout(120_000);
  await signUp(page, '-ui-flex');
  await expect(page.getByText('Choose File')).toHaveCount(0);
  await expect(page.getByText('No file chosen')).toHaveCount(0);
  await expect(page.getByText('Nenhum arquivo escolhido')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Escolher arquivo' })).toBeVisible();

  await expect(page.getByRole('button', { name: 'Novo tipo' })).toBeVisible();
  await page.getByRole('button', { name: 'Novo tipo' }).click();
  await expect(page.getByText('Meus tipos')).toBeVisible();
  await expect(page.getByPlaceholder('ex.: vacinas do meu filho')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Sugerir tipo' })).toBeDisabled();
  await page.getByLabel('Descrever um tipo').fill('corri 5 km em 30 minutos no parque');
  await expect(page.getByRole('button', { name: 'Sugerir tipo' })).toBeEnabled();
  await page.getByRole('button', { name: 'Sugerir tipo' }).click();
  await expect(page.getByLabel('Nome do tipo')).toHaveValue('Corridas');
  await expect(page.getByText('número km')).toBeVisible();
  await expect(page.getByText('Nome de Título')).toHaveCount(0);
  await expect(page.getByText('Nome de Distância')).toHaveCount(0);
  await expect(page.getByRole('button', { name: /Campo ativo: Distância/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Campo ativo: Duração/ })).toBeVisible();
  await page.getByRole('button', { name: 'Usar esta sugestão' }).click();
  await expect(page.getByRole('button', { name: 'Guardar entrada' })).toBeVisible();
  await expect(page.getByLabel('Título', { exact: true })).toBeVisible();
  await expect(page.getByPlaceholder('ex.: 5,2')).toBeVisible();
  await expect(page.getByPlaceholder('ex.: 32')).toBeVisible();
  await expect(page.getByPlaceholder('ex.: 11,8')).toHaveCount(0);
  await expect(page.getByLabel('Número')).toHaveCount(0);
  await expect(page.getByLabel('Tags (opcional)')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Mais detalhes' })).toBeVisible();

  await openTypes(page);
  await page.getByText('Vacinas', { exact: true }).first().click();
  await expect(page.getByLabel('Título', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Quando')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Abrir calendário' })).toBeVisible();
  await expect(page.getByText('AAAA-MM-DD')).toHaveCount(0);
  await expect(page.getByText('mm/dd/yyyy')).toHaveCount(0);
  await expect(page.getByLabel('Onde')).toBeVisible();
  await expect(page.getByLabel('Número')).toHaveCount(0);
  await expect(page.getByLabel('Texto')).toHaveCount(0);
  await expect(page.getByLabel('Tags (opcional)')).toHaveCount(0);
  await page.getByRole('button', { name: 'Mais detalhes' }).click();
  await expect(page.getByLabel('Tags (opcional)')).toBeVisible();
  await page.getByRole('button', { name: 'Menos detalhes' }).click();
  await expect(page.getByLabel('Número')).toHaveCount(0);
  await expect(page.getByLabel('Tags (opcional)')).toHaveCount(0);
  await page.getByLabel('Título', { exact: true }).fill('Gripe gráfico');
  await page.getByLabel('Quando').fill('2026-11-10');
  await page.getByLabel('Onde').fill('UBS Centro');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByLabel('Título', { exact: true })).toHaveValue('');
  await page.getByRole('button', { name: 'Calendário deste tipo' }).click();
  await expect(page.getByTestId('month-grid')).toBeVisible();
  await expect(page.getByText('Gripe gráfico').first()).toBeVisible();
  await page.getByTestId('day-2026-11-28').scrollIntoViewIfNeeded();
  await expect(page.getByTestId('day-2026-11-28')).toBeVisible();
  await expect(page.getByTestId('day-2026-11-01')).toBeVisible();

  await openTypes(page);
  await page.getByText('Pet', { exact: true }).first().click();
  await expect(page.getByTestId('type-heading')).toHaveText('Pet');
  await expect(page.getByRole('button', { name: 'Compartilhar com Eu' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Compartilhar com Família' })).toHaveCount(1);
  await expect(page.getByText('Fora do círculo:')).toHaveCount(0);
  await page.getByLabel('Título', { exact: true }).fill('Consulta Luna');
  await page.getByLabel('Quando').fill('2026-11-10');
  await page.getByLabel('Peso').fill('12');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByLabel('Título', { exact: true })).toHaveValue('');
  await expect(page.getByTestId('number-chart')).toBeVisible();
  await expect(page.getByText('Ainda não há números neste tipo.')).toHaveCount(0);
  await expect(page.getByTestId('chart-bar').first()).toBeVisible();
  await expect(page.getByText('registre mais para ver a evolução')).toBeVisible();
  await expect(page.getByTestId('chart-single-value')).toContainText('12');
  await expect(page.getByTestId('chart-single-value')).toContainText('10/11/2026');
  await page.getByLabel('Título', { exact: true }).fill('Retorno Luna');
  await page.getByLabel('Quando').fill('2026-10-10');
  await page.getByLabel('Peso').fill('11,8');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByLabel('Título', { exact: true })).toHaveValue('');
  await expect(page.getByTestId('number-chart').getByText('118')).toHaveCount(0);
  await expect(page.getByTestId('number-chart').getByText(/11,8/)).toBeVisible();
  await expect(page.getByTestId('chart-axis')).toBeVisible();
  await expect(page.getByTestId('chart-axis')).not.toHaveText(/118/);
});

test('falar mostra gravação, transcreve com falso e cancela', async ({ page }) => {
  await page.addInitScript(() => {
    Object.assign(window, { circulosTestRecorder: true });
  });
  await signUp(page, '-voz-ui');
  await page.getByRole('button', { name: 'Falar' }).click();
  await expect(page.getByTestId('voice-recording')).toBeVisible();
  await expect(page.getByTestId('voice-timer')).toHaveCount(1);
  await expect(page.getByTestId('voice-timer')).toHaveText(/Gravando 00:0[01]/);
  await expect(page.getByTestId('voice-timer')).not.toHaveText('Gravando 00:00', { timeout: 4_000 });
  await expect(page.getByTestId('voice-timer')).toHaveText(/Gravando \d{2}:\d{2}/);
  await expect(page.getByTestId('voice-pulse')).toBeVisible();
  await page.getByRole('button', { name: 'Cancelar' }).click();
  await expect(page.getByTestId('voice-recording')).toHaveCount(0);
  await page.getByRole('button', { name: 'Falar' }).click();
  await expect(page.getByTestId('voice-recording')).toBeVisible();
  await page.getByRole('button', { name: 'Parar' }).click();
  await expect(page.getByText(/Sugerimos|Transcrevendo/)).toBeVisible();
  await expect(page.getByText(/Sugerimos/)).toBeVisible({ timeout: 20_000 });
  await expect(page.getByText(/Transcrição:/)).toBeVisible();
});

test('sugestão deixa Aceitar visível no telefone', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await signUp(page, '-aceitar-mob');
  const emptyWriter = await page.getByLabel('Escrever').boundingBox();
  expect(emptyWriter).toBeTruthy();
  await page.getByLabel('Escrever').fill(
    'Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro\nSegunda linha da captura para o campo crescer no telefone.',
  );
  const filledWriter = await page.getByLabel('Escrever').boundingBox();
  expect(filledWriter).toBeTruthy();
  expect(filledWriter!.height).toBeGreaterThan(emptyWriter!.height);
  expect(filledWriter!.height).toBeGreaterThan(70);
  await page.getByRole('button', { name: 'Sugerir' }).click();
  await expect(page.getByText(/Sugerimos/)).toBeVisible();
  const limit = (page.viewportSize()?.height ?? 844) - 72;
  const ys: number[] = [];
  for (const name of ['Aceitar', 'Editar', 'Recusar']) {
    const box = await page.getByRole('button', { name }).boundingBox();
    expect(box, name).toBeTruthy();
    expect(box!.y, name).toBeGreaterThanOrEqual(0);
    expect(box!.y + box!.height, name).toBeLessThanOrEqual(limit + 8);
    ys.push(Math.round(box!.y));
  }
  expect(Math.max(...ys) - Math.min(...ys)).toBeLessThan(12);
  await expect(page.getByRole('button', { name: 'Hoje', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Círculos', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Perfil', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Mais', exact: true })).toBeVisible();
});

test('404 não sobrepõe o conteúdo e a barra lateral não mostra Preços', async ({ page }) => {
  await signUp(page, '-layout');
  await page.setViewportSize({ width: 1280, height: 800 });
  const ajustes = page.getByRole('button', { name: 'Ajustes', exact: true }).first();
  const sair = page.getByRole('button', { name: 'Sair' }).first();
  const hoje = page.getByRole('button', { name: 'Hoje' }).first();
  await expect(page.getByRole('button', { name: 'Preços' })).toHaveCount(0);
  await expect(ajustes).toBeVisible();
  await expect(sair).toBeVisible();
  const ajustesBox = await ajustes.boundingBox();
  const sairBox = await sair.boundingBox();
  expect(ajustesBox).toBeTruthy();
  expect(sairBox).toBeTruthy();
  expect(ajustesBox!.y + ajustesBox!.height).toBeLessThanOrEqual(sairBox!.y + 2);

  await page.getByRole('button', { name: 'Busca' }).first().click();
  await page.mouse.wheel(0, 2400);
  const hojeDepois = await hoje.boundingBox();
  const sairDepois = await sair.boundingBox();
  expect(hojeDepois).toBeTruthy();
  expect(sairDepois).toBeTruthy();
  expect(hojeDepois!.y).toBeGreaterThanOrEqual(0);
  expect(hojeDepois!.y + hojeDepois!.height).toBeLessThanOrEqual(sairDepois!.y + 2);

  await page.goto('/xyz-nao-existe');
  const titulo = page.getByText('Página não encontrada');
  const corpo = page.getByText('Esta tela não existe.');
  await expect(titulo).toBeVisible();
  await expect(corpo).toBeVisible();
  const tituloBox = await titulo.boundingBox();
  const corpoBox = await corpo.boundingBox();
  expect(tituloBox).toBeTruthy();
  expect(corpoBox).toBeTruthy();
  expect(tituloBox!.y + tituloBox!.height).toBeLessThanOrEqual(corpoBox!.y + 2);
});

test('Tipos abre no desktop depois de passar pelo viewport mobile', async ({ page }) => {
  await signUp(page, '-vp');
  await page.setViewportSize({ width: 390, height: 844 });
  await waitForAppChrome(page);
  await expect(page.getByRole('button', { name: 'Mais', exact: true })).toBeVisible();
  await page.setViewportSize({ width: 1280, height: 800 });
  await openTypes(page);
  await expect(page.getByRole('button', { name: 'Mais', exact: true })).toHaveCount(0);
});

test('telas da rodada 18 em desktop e mobile', async ({ page }) => {
  test.setTimeout(180_000);
  const dir = '/opt/cursor/artifacts/rodada18';
  await mkdir(dir, { recursive: true });
  await page.addInitScript(() => {
    Object.assign(window, { circulosTestRecorder: true });
  });
  await signUp(page, '-shots');

  async function shoot(name: string, inApp = true): Promise<void> {
    await page.setViewportSize({ width: 1280, height: 800 });
    if (inApp) {
      await waitForAppChrome(page);
    }
    await settleLayout(page);
    await page.screenshot({ path: `${dir}/${name}-desktop.png`, fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    if (inApp) {
      await waitForAppChrome(page);
    }
    await settleLayout(page);
    await page.screenshot({ path: `${dir}/${name}-mobile.png`, fullPage: true });
    await page.setViewportSize({ width: 1280, height: 800 });
    if (inApp) {
      await waitForAppChrome(page);
    }
    await settleLayout(page);
  }

  await expect(page.getByRole('button', { name: 'Novo tipo' })).toBeVisible();
  await shoot('hoje');
  await page.getByRole('button', { name: 'Falar' }).click();
  await expect(page.getByTestId('voice-timer')).toHaveText(/Gravando 00:0[01]/);
  await expect(page.getByTestId('voice-timer')).not.toHaveText('Gravando 00:00', { timeout: 4_000 });
  await expect(page.getByTestId('voice-timer')).toHaveText(/Gravando \d{2}:\d{2}/);
  await shoot('falar');
  await page.getByRole('button', { name: 'Cancelar' }).click();

  await page.getByRole('button', { name: 'Perfil' }).click();
  await expect(page.getByText('Seus tipos')).toHaveCount(0);
  await shoot('perfil');

  await openTypes(page);
  await shoot('tipos');
  await page.getByLabel('Descrever um tipo').fill('corri 5 km em 30 minutos no parque');
  await page.getByRole('button', { name: 'Sugerir tipo' }).click();
  await expect(page.getByLabel('Nome do tipo')).toHaveValue('Corridas');
  await expect(page.getByText('número km')).toBeVisible();
  await expect(page.getByText('Nome de Distância')).toHaveCount(0);
  await shoot('sugestao-tipo');

  await page.getByText('Vacinas', { exact: true }).first().click();
  await expect(page.getByRole('button', { name: 'Guardar entrada' })).toBeVisible();
  await expect(page.getByLabel('Quando')).toBeVisible();
  await shoot('tipo-vacinas');
  await page.getByLabel('Título', { exact: true }).fill('Gripe');
  await page.getByLabel('Quando').fill('2026-11-10');
  await page.getByLabel('Onde').fill('UBS Centro');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByLabel('Título', { exact: true })).toHaveValue('');
  await page.getByRole('button', { name: 'Calendário deste tipo' }).click();
  await expect(page.getByTestId('month-grid')).toBeVisible();
  await shoot('tipo-calendario');

  await openTypes(page);
  await page.getByText('Pet', { exact: true }).first().click();
  await expect(page.getByRole('button', { name: 'Compartilhar com Eu' })).toBeVisible();
  await page.getByLabel('Título', { exact: true }).fill('Consulta');
  await page.getByLabel('Quando').fill('2026-11-10');
  await page.getByLabel('Peso').fill('12');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByLabel('Título', { exact: true })).toHaveValue('');
  await expect(page.getByTestId('number-chart')).toBeVisible();
  await expect(page.getByText('registre mais para ver a evolução')).toBeVisible();
  await expect(page.getByTestId('chart-bar').first()).toBeVisible();
  await expect(page.getByTestId('chart-single-value')).toContainText('10/11/2026');
  await page.getByTestId('number-chart').scrollIntoViewIfNeeded();
  await shoot('tipo-grafico');

  await page.getByRole('button', { name: 'Calendário', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Assinar no seu calendário' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Copiar endereço' })).toBeVisible();
  await shoot('calendario');

  await page.getByRole('button', { name: 'Ajustes', exact: true }).first().click();
  await page.getByRole('button', { name: 'Ver preços' }).click();
  await expect(page.getByText('Preços aprovados pelo Allan')).toBeVisible();
  await shoot('precos', false);
  await page.getByRole('button', { name: 'Voltar' }).click();
  await waitForAppChrome(page);
  await page.goto('/(app)/settings');
  await waitForAppChrome(page);

  await page.getByRole('button', { name: 'Escuro' }).click();
  await expect(page.getByTestId('theme-status')).toContainText('Agora: escuro');
  await shoot('ajustes-escuro');

  await page.getByRole('button', { name: 'Busca' }).first().click();
  await shoot('busca');
  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await shoot('lembretes');
  await page.goto('/xyz-nao-existe');
  await expect(page.getByText('Página não encontrada')).toBeVisible();
  await shoot('404', false);
});

test('prints da rodada 19', async ({ page }) => {
  test.skip(!process.env.E2E_PRINTS, 'gera artefatos só com E2E_PRINTS=1');
  test.setTimeout(240_000);
  const dir = '/opt/cursor/artifacts/rodada19';
  const v4 = '/opt/cursor/artifacts/circulos-telas-v4';
  await mkdir(dir, { recursive: true });
  const befores: Array<[string, string]> = [
    ['35-ajustes-backup-desktop.png', 'antes-01-ajustes-toast-desktop.png'],
    ['35-ajustes-backup-mobile.png', 'antes-01-ajustes-toast-mobile.png'],
    ['15-tipo-vacinas-timeline-desktop.png', 'antes-02-familia-chip-desktop.png'],
    ['04-hoje-desktop.png', 'antes-03-email-sidebar-desktop.png'],
    ['31-documentos-mobile.png', 'antes-04-documentos-mobile.png'],
    ['32-memorias-desktop.png', 'antes-05-memorias-acento-desktop.png'],
    ['17-tipo-pet-grafico-desktop.png', 'antes-06-grafico-peso-desktop.png'],
    ['16-tipo-vacinas-calendario-desktop.png', 'antes-07-calendario-tipo-desktop.png'],
    ['01-login-desktop.png', 'antes-08-login-hub-desktop.png'],
    ['34-faq-desktop.png', 'antes-08-faq-hub-desktop.png'],
    ['24-resumo-desktop.png', 'antes-10-resumo-documentos-desktop.png'],
    ['05-captura-texto-sugestao-mobile.png', 'antes-14-aceitar-mobile.png'],
    ['10-voz-sugestao-desktop.png', 'antes-15-wav-desktop.png'],
    ['21-circulo-familia-desktop.png', 'antes-16-convite-desktop.png'],
    ['04-hoje-mobile.png', 'antes-17-abas-mobile.png'],
  ];
  for (const [from, to] of befores) {
    const source = `${v4}/${from}`;
    if (existsSync(source)) {
      await copyFile(source, `${dir}/${to}`);
    }
  }

  await page.addInitScript(() => {
    Object.assign(window, { circulosTestRecorder: true });
  });

  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto('/(auth)/login');
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible();
  await screenshotFull(page, `${dir}/depois-08-login-desktop.png`);
  await page.setViewportSize({ width: 390, height: 844 });
  await screenshotFull(page, `${dir}/depois-08-login-mobile.png`);
  await page.setViewportSize({ width: 1280, height: 800 });

  const email = `e2e-${Date.now()}-r19@exemplo.com`;
  await page.goto('/(auth)/register');
  await page.getByLabel('Nome', { exact: true }).fill('Pessoa E2E');
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill('senha-segura-1');
  await page.getByRole('button', { name: 'Criar conta' }).click();
  await expect(page.getByRole('button', { name: 'Pular tour' })).toBeVisible({ timeout: 20_000 });
  for (let step = 1; step <= 8; step += 1) {
    await expect(page.getByText(`${step} de 8`)).toBeVisible();
    await screenshotFull(page, `${dir}/depois-tour-${step}-desktop.png`);
    await page.setViewportSize({ width: 390, height: 844 });
    await screenshotFull(page, `${dir}/depois-tour-${step}-mobile.png`);
    await page.setViewportSize({ width: 1280, height: 800 });
    if (step < 8) {
      await page.getByRole('button', { name: 'Avançar' }).click();
    }
  }
  await page.getByRole('button', { name: 'Concluir tour' }).click();
  await expect(page.getByText('O que pede atenção hoje')).toBeVisible({ timeout: 20_000 });
  await reloadAppRoute(page);
  await waitForAppChrome(page);

  await screenshotFull(page, `${dir}/depois-03-email-sidebar-desktop.png`);
  await page.getByLabel('Escrever').fill('Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro');
  await page.getByRole('button', { name: 'Sugerir' }).click();
  await expect(page.getByRole('button', { name: 'Aceitar' })).toBeVisible();
  await screenshotFull(page, `${dir}/depois-14-aceitar-desktop.png`);
  await page.setViewportSize({ width: 390, height: 844 });
  await waitForAppChrome(page);
  await screenshotFull(page, `${dir}/depois-14-aceitar-mobile.png`);
  await screenshotFull(page, `${dir}/depois-17-abas-mobile.png`);
  await page.setViewportSize({ width: 1280, height: 800 });
  await waitForAppChrome(page);
  await page.getByRole('button', { name: 'Recusar' }).click();

  await page.getByRole('button', { name: 'Falar' }).click();
  await page.getByRole('button', { name: 'Parar' }).click();
  await expect(page.getByText(/Sugerimos/)).toBeVisible({ timeout: 20_000 });
  await screenshotFull(page, `${dir}/depois-15-voz-desktop.png`);
  await page.getByRole('button', { name: 'Recusar' }).click();

  await openTypes(page);
  await page.getByText('Vacinas', { exact: true }).first().click();
  await expect(page.getByRole('button', { name: 'Compartilhar com Família' })).toHaveCount(1);
  await screenshotFull(page, `${dir}/depois-02-familia-chip-desktop.png`);
  await page.getByLabel('Título', { exact: true }).fill('Gripe');
  await page.getByLabel('Quando').fill('2026-03-10');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await page.getByRole('button', { name: 'Calendário deste tipo' }).click();
  await expect(page.getByTestId('month-grid')).toBeVisible();
  await screenshotFull(page, `${dir}/depois-07-calendario-tipo-desktop.png`);

  await openTypes(page);
  await page.getByText('Pet', { exact: true }).first().click();
  await page.getByLabel('Título', { exact: true }).fill('Consulta');
  await page.getByLabel('Quando').fill('2026-01-10');
  await page.getByLabel('Peso').fill('11,8');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await page.getByLabel('Título', { exact: true }).fill('Retorno');
  await page.getByLabel('Quando').fill('2026-02-10');
  await page.getByLabel('Peso').fill('12');
  await page.getByRole('button', { name: 'Guardar entrada' }).click();
  await expect(page.getByTestId('chart-axis')).toBeVisible();
  await page.getByTestId('number-chart').scrollIntoViewIfNeeded();
  await screenshotFull(page, `${dir}/depois-06-grafico-peso-desktop.png`);
  await screenshotFull(page, `${dir}/depois-12-placeholder-desktop.png`);

  const token = await page.evaluate(() => localStorage.getItem('circulos.token'));
  const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://127.0.0.1:5288';
  const uploaded = await page.request.post(`${apiUrl}/api/documents`, {
    headers: { Authorization: `Bearer ${token}` },
    multipart: {
      file: { name: 'carteira-vacina.txt', mimeType: 'text/plain', buffer: Buffer.from('nota da carteira') },
    },
  });
  expect(uploaded.ok()).toBeTruthy();

  await page.getByRole('button', { name: 'Documentos' }).click();
  await page.setViewportSize({ width: 390, height: 844 });
  await waitForAppChrome(page);
  await screenshotFull(page, `${dir}/depois-04-documentos-mobile.png`);
  await page.setViewportSize({ width: 1280, height: 800 });
  await waitForAppChrome(page);

  await page.getByRole('button', { name: 'Ajustes' }).first().click();
  await expect(page.getByText(/Inadimplente/)).toHaveCount(0);
  await screenshotFull(page, `${dir}/depois-01-ajustes-desktop.png`);
  await screenshotFull(page, `${dir}/depois-09-armazenamento-desktop.png`);
  await screenshotFull(page, `${dir}/depois-11-plano-desktop.png`);
  await page.getByRole('button', { name: 'Exportar meu perfil' }).scrollIntoViewIfNeeded();
  await screenshotFull(page, `${dir}/depois-backup-desktop.png`);

  await page.getByRole('button', { name: 'FAQ' }).first().click();
  await screenshotFull(page, `${dir}/depois-08-faq-desktop.png`);

  await page.getByRole('button', { name: 'Memórias' }).first().click();
  await screenshotFull(page, `${dir}/depois-05-memorias-desktop.png`);

  await page.getByRole('button', { name: 'Perfil' }).click();
  await page.getByRole('button', { name: 'Resumo de uso' }).click();
  await screenshotFull(page, `${dir}/depois-10-resumo-desktop.png`);

  await page.getByRole('button', { name: 'Círculos' }).click();
  await page.getByRole('button', { name: 'Abrir Família' }).click();
  await page.getByLabel('Convidar por e-mail').fill(`convite-${Date.now()}@exemplo.com`);
  await page.getByRole('button', { name: 'Enviar convite' }).click();
  await expect(page.getByTestId('invite-code')).toBeVisible();
  await screenshotFull(page, `${dir}/depois-16-convite-desktop.png`);

  await page.getByRole('button', { name: 'Lembretes' }).first().click();
  await page.setViewportSize({ width: 390, height: 844 });
  await waitForAppChrome(page);
  await screenshotFull(page, `${dir}/depois-13-lembretes-mobile.png`);
});


