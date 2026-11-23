import ptBr from '../locales/pt-BR.json';

const catalog = ptBr as Record<string, string>;

export function t(key: keyof typeof ptBr): string {
  return catalog[key] ?? key;
}

export const locale = 'pt-BR';
