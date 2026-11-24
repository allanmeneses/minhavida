import { writeFile } from 'node:fs/promises';
import type { Page } from '@playwright/test';

export function printsConnectionString(): string {
  const current =
    process.env.ConnectionStrings__Default
    ?? process.env.CONNECTION_STRING
    ?? 'Host=127.0.0.1;Port=5432;Database=circulos;Username=circulos;Password=circulos';
  if (process.env.PRINTS_ISOLATED === '1') {
    return current.replace(/Database=[^;]+/i, 'Database=circulos_prints');
  }
  return current;
}

async function settle(page: Page): Promise<void> {
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      }),
  );
}

async function scrollInner(page: Page, ratio: number): Promise<void> {
  await page.evaluate((value) => {
    const scrollable: HTMLElement[] = [];
    for (const node of Array.from(document.querySelectorAll('div'))) {
      const style = window.getComputedStyle(node);
      if (
        (style.overflowY === 'auto' || style.overflowY === 'scroll' || style.overflow === 'auto')
        && node.scrollHeight > node.clientHeight + 24
      ) {
        scrollable.push(node);
      }
    }
    scrollable.sort((a, b) => b.scrollHeight - a.scrollHeight);
    const target = scrollable[0];
    if (target) {
      const max = Math.max(0, target.scrollHeight - target.clientHeight);
      target.scrollTop = Math.round(max * value);
    }
    const docMax = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
    window.scrollTo(0, Math.round(docMax * value));
  }, ratio);
}

export async function screenshotSteps(page: Page, path: string): Promise<void> {
  await settle(page);
  await scrollInner(page, 0);
  const top = await page.screenshot({ fullPage: true });
  await writeFile(path, top);
  await scrollInner(page, 0.5);
  await settle(page);
  const mid = await page.screenshot({ fullPage: true });
  if (!top.equals(mid)) {
    await writeFile(path.replace(/\.png$/, '-meio.png'), mid);
  }
  await scrollInner(page, 1);
  await settle(page);
  const bot = await page.screenshot({ fullPage: true });
  if (!mid.equals(bot) && !top.equals(bot)) {
    await writeFile(path.replace(/\.png$/, '-baixo.png'), bot);
  }
  await scrollInner(page, 0);
}

export async function reloadAppRoute(page: Page): Promise<void> {
  await page.reload({ waitUntil: 'domcontentloaded' });
}
