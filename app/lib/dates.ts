export function datePlaceholder(): string {
  return 'ex.: 24/11/2026';
}

export function tagsPlaceholder(): string {
  return 'palavras-chave, separadas por vírgula';
}

export function toIsoDate(value: string): string {
  const trimmed = value.trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) {
    return trimmed;
  }

  const br = trimmed.match(/^(\d{1,2})\/(\d{1,2})\/(\d{2,4})$/);
  if (!br) {
    return trimmed;
  }

  const day = br[1].padStart(2, '0');
  const month = br[2].padStart(2, '0');
  const year = br[3].length === 2 ? `20${br[3]}` : br[3];
  return `${year}-${month}-${day}`;
}

export function isoToDisplayDate(value: string): string {
  const iso = toIsoDate(value);
  const match = iso.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!match) {
    return value;
  }

  return `${match[3]}/${match[2]}/${match[1]}`;
}

export function formatPersonDateTime(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }

  const dd = String(date.getDate()).padStart(2, '0');
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const yyyy = String(date.getFullYear());
  const hh = String(date.getHours()).padStart(2, '0');
  const min = String(date.getMinutes()).padStart(2, '0');
  return `${dd}/${mm}/${yyyy} ${hh}:${min}`;
}

export const WEEKDAY_LABELS = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'];

export const MONTH_LABELS = [
  'janeiro',
  'fevereiro',
  'março',
  'abril',
  'maio',
  'junho',
  'julho',
  'agosto',
  'setembro',
  'outubro',
  'novembro',
  'dezembro',
];

export function monthTitle(year: number, month: number): string {
  const name = MONTH_LABELS[month - 1] ?? String(month);
  const titled = name.charAt(0).toLocaleUpperCase('pt-BR') + name.slice(1);
  return `${titled} de ${year}`;
}
