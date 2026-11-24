export type SuggestedField = {
  id: string;
  name: string;
  kind: string;
  required: boolean;
  order: number;
};

export type LocalTypeSuggestion = {
  name: string;
  slug: string;
  gallerySlug: string | null;
  fields: SuggestedField[];
};

const GALLERY: { slug: string; name: string; strong: string[]; weak: string[] }[] = [
  { slug: 'vacinas', name: 'Vacinas', strong: ['vacina', 'hepatite', 'imuniz'], weak: ['lote', 'ubs', 'posto'] },
  { slug: 'diario', name: 'Diário', strong: ['diario', 'hoje eu'], weak: ['paragrafo'] },
  { slug: 'documento', name: 'Documento', strong: ['documento', 'carteira', 'rg', 'cnh'], weak: ['arquivo'] },
  { slug: 'remedio', name: 'Remédio', strong: ['remedio', 'comprimido', 'tomei o remedio'], weak: ['dose'] },
  { slug: 'pet', name: 'Pet', strong: ['pet', 'cachorro', 'gato', 'veterinar', 'vermifugo'], weak: [] },
  { slug: 'carro', name: 'Carro', strong: ['carro', 'revisao', 'oleo'], weak: ['quilometragem'] },
  { slug: 'livro', name: 'Livro', strong: ['livro', 'autor', 'capitulo', 'li o', 'terminei o livro'], weak: [] },
  { slug: 'compra', name: 'Compra', strong: ['compra', 'comprei', 'paguei', 'supermercado'], weak: ['reais'] },
];

const NOUNS: { name: string; keys: string[] }[] = [
  { name: 'Corridas', keys: ['corrida', 'corri', 'correndo', 'trote', 'corridas'] },
  { name: 'Gastos', keys: ['gastei', 'gasto', 'gastos', 'despesa', 'despesas'] },
  { name: 'Livros', keys: ['livro', 'livros', 'li o', 'terminei o livro', 'capitulo'] },
  { name: 'Vacinas', keys: ['vacina', 'hepatite', 'imuniz'] },
  { name: 'Pet', keys: ['pet', 'cachorro', 'gato', 'veterinar', 'vermifugo'] },
  { name: 'Carro', keys: ['carro', 'revisao', 'oleo'] },
  { name: 'Remédio', keys: ['remedio', 'comprimido'] },
  { name: 'Viagens', keys: ['viagem', 'viagens', 'voo', 'hotel', 'aeroporto', 'embarque'] },
  { name: 'Filmes', keys: ['filme', 'filmes', 'cinema', 'sessao', 'assisti'] },
];

const PLACE_STOP = new Set([
  'dia',
  'mes',
  'minuto',
  'minutos',
  'hora',
  'horas',
  'km',
  'reais',
  'real',
  'janeiro',
  'fevereiro',
  'marco',
  'abril',
  'maio',
  'junho',
  'julho',
  'agosto',
  'setembro',
  'outubro',
  'novembro',
  'dezembro',
]);

export function normalize(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '');
}

export function slugify(value: string): string {
  const slug = normalize(value)
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
  return slug || 'livre';
}

export function suggestTypeFromSentence(sentence: string): LocalTypeSuggestion {
  const text = sentence.trim();
  const normalized = normalize(text);
  const fields = fieldsFromSentence(text);
  const gallery = highConfidenceGallery(normalized);
  const named = extractAnotarName(text);
  if (gallery) {
    return {
      name: named ?? gallery.name,
      slug: gallery.slug,
      gallerySlug: gallery.slug,
      fields: mergeFields(galleryFields(gallery.slug), fields),
    };
  }

  const noun = nameFromNouns(normalized);
  return {
    name: named ?? noun ?? 'Livre',
    slug: slugify(named ?? 'livre'),
    gallerySlug: null,
    fields: fields.length > 1 ? fields : defaultFields(),
  };
}

function highConfidenceGallery(normalized: string): { slug: string; name: string } | null {
  for (const item of GALLERY) {
    const strong = item.strong.some((key) => normalized.includes(normalize(key)));
    const weakHits = item.weak.filter((key) => normalized.includes(normalize(key))).length;
    if (strong || weakHits >= 2) {
      return item;
    }
  }
  return null;
}

function extractAnotarName(text: string): string | null {
  const anotar = text.match(/anotar\s+(?:as|os|o|a)?\s*([^,]+?)(?:,| com |$)/i);
  if (anotar) {
    const name = anotar[1].trim().replace(/\s+do meu filho$/i, '');
    if (name.length >= 3 && name.length <= 40) {
      return titleCase(name);
    }
  }
  return null;
}

function nameFromNouns(normalized: string): string | null {
  for (const item of NOUNS) {
    if (item.keys.some((key) => normalized.includes(normalize(key)))) {
      return item.name;
    }
  }
  return null;
}

function fieldsFromSentence(text: string): SuggestedField[] {
  const fields: SuggestedField[] = [{ id: 'title', name: 'Título', kind: 'text', required: true, order: 0 }];
  const add = (id: string, name: string, kind: string) => {
    if (fields.some((field) => field.id === id || field.name.toLowerCase() === name.toLowerCase())) {
      return;
    }
    fields.push({ id, name, kind, required: false, order: fields.length });
  };

  const units = extractUnitNumbers(text);
  if (firstDate(text) || /\b(data|quando|dia)\b/i.test(normalize(text))) {
    add('when', 'Quando', 'date');
  }
  if (units.km != null) {
    add('distancia', 'Distância', 'number');
  }
  if (units.minutes != null) {
    add('duracao', 'Duração', 'number');
  }
  if (units.money != null) {
    add('valor', 'Valor', 'number');
  }
  if (units.kg != null) {
    add('peso', 'Peso', 'number');
  }
  if (units.dose != null) {
    add('dose', 'Dose', 'number');
  }
  if (firstPlace(text) || /\b(onde|lugar|local|posto)\b/i.test(normalize(text))) {
    add('where', 'Onde', 'place');
  }
  if (firstPerson(text)) {
    add('quem', 'Quem', 'text');
  }
  if (sentenceHasBoolCue(text)) {
    add('done', 'Feito', 'bool');
  }

  return mergeFields(fields, namedFields(text));
}

function namedFields(text: string): SuggestedField[] {
  const match = text.match(/\bcom\s+(.+)$/i);
  if (!match) {
    return [];
  }

  const parts = match[1]
    .split(/\s*,\s*|\s+e\s+/i)
    .map((item) => item.trim().replace(/\.$/, ''))
    .filter((item) => item.length >= 2 && item.length <= 40);
  const fields: SuggestedField[] = [];
  for (const part of parts) {
    const kind = inferKind(part);
    let id = slugify(part);
    if (id === 'quando' || id === 'data' || id === 'dia') {
      id = 'when';
    }
    if (id === 'posto' || id === 'onde' || id === 'lugar' || id === 'local') {
      id = 'where';
    }
    if (fields.some((field) => field.id === id)) {
      continue;
    }
    const name = id === 'when' ? 'Quando' : id === 'where' ? 'Onde' : titleCase(part);
    fields.push({ id, name, kind, required: false, order: fields.length });
  }
  return fields;
}

function inferKind(name: string): string {
  const normalized = normalize(name);
  if (normalized === 'data' || normalized === 'quando' || normalized === 'dia') {
    return 'date';
  }
  if (normalized === 'posto' || normalized === 'onde' || normalized === 'lugar' || normalized === 'local') {
    return 'place';
  }
  if (
    ['peso', 'dose', 'valor', 'km', 'nota', 'numero', 'distancia', 'duracao'].includes(normalized)
  ) {
    return 'number';
  }
  if (normalized === 'tomado' || normalized === 'feito') {
    return 'bool';
  }
  return 'text';
}

function galleryFields(slug: string): SuggestedField[] {
  const commonTitle = { id: 'title', name: 'Título', kind: 'text', required: true, order: 0 };
  switch (slug) {
    case 'vacinas':
      return [
        commonTitle,
        { id: 'when', name: 'Quando', kind: 'date', required: true, order: 1 },
        { id: 'where', name: 'Onde', kind: 'place', required: false, order: 2 },
        { id: 'lote', name: 'Lote', kind: 'text', required: false, order: 3 },
      ];
    case 'carro':
      return [
        commonTitle,
        { id: 'km', name: 'Quilometragem', kind: 'number', required: false, order: 1 },
        { id: 'when', name: 'Quando', kind: 'date', required: false, order: 2 },
        { id: 'where', name: 'Onde', kind: 'place', required: false, order: 3 },
      ];
    case 'livro':
      return [
        commonTitle,
        { id: 'autor', name: 'Autor', kind: 'text', required: false, order: 1 },
        { id: 'when', name: 'Quando', kind: 'date', required: false, order: 2 },
        { id: 'nota', name: 'Nota', kind: 'number', required: false, order: 3 },
      ];
    case 'compra':
      return [
        commonTitle,
        { id: 'valor', name: 'Valor', kind: 'number', required: false, order: 1 },
        { id: 'when', name: 'Quando', kind: 'date', required: false, order: 2 },
        { id: 'where', name: 'Onde', kind: 'place', required: false, order: 3 },
      ];
    case 'pet':
      return [
        commonTitle,
        { id: 'when', name: 'Quando', kind: 'date', required: false, order: 1 },
        { id: 'peso', name: 'Peso', kind: 'number', required: false, order: 2 },
        { id: 'where', name: 'Onde', kind: 'place', required: false, order: 3 },
      ];
    case 'remedio':
      return [
        commonTitle,
        { id: 'when', name: 'Quando', kind: 'date', required: false, order: 1 },
        { id: 'dose', name: 'Dose', kind: 'number', required: false, order: 2 },
        { id: 'done', name: 'Tomado', kind: 'bool', required: false, order: 3 },
      ];
    default:
      return defaultFields();
  }
}

function defaultFields(): SuggestedField[] {
  return [
    { id: 'title', name: 'Título', kind: 'text', required: true, order: 0 },
    { id: 'when', name: 'Quando', kind: 'date', required: false, order: 1 },
    { id: 'body', name: 'Texto', kind: 'text', required: false, order: 2 },
  ];
}

function mergeFields(base: SuggestedField[], extra: SuggestedField[]): SuggestedField[] {
  const next = [...base];
  for (const field of extra) {
    if (field.id === 'title') {
      continue;
    }
    if (next.some((item) => item.id === field.id || item.name.toLowerCase() === field.name.toLowerCase())) {
      continue;
    }
    next.push({ ...field, order: next.length });
  }
  return next;
}

export function extractUnitNumbers(text: string): {
  km: number | null;
  minutes: number | null;
  money: number | null;
  kg: number | null;
  dose: number | null;
} {
  const parse = (raw: string | undefined): number | null => {
    if (!raw) {
      return null;
    }
    const value = Number(raw.replace(',', '.'));
    return Number.isFinite(value) ? value : null;
  };
  const pick = (pattern: RegExp): number | null => parse(text.match(pattern)?.[1]);
  let km = pick(/(\d+(?:[.,]\d+)?)\s*(km|quilometros?)\b/i);
  const mil = text.match(/(\d+)\s*mil\s*(km|quilometros?)\b/i);
  if (mil) {
    km = Number(mil[1]) * 1000;
  }
  const hours = pick(/(\d+(?:[.,]\d+)?)\s*(horas?)\b/i);
  let minutes = pick(/(\d+(?:[.,]\d+)?)\s*(minutos?|min)\b/i);
  if (hours != null && minutes == null) {
    minutes = hours * 60;
  }
  return {
    km,
    minutes,
    money: pick(/R\$\s*(\d+(?:[.,]\d+)?)/i) ?? pick(/(\d+(?:[.,]\d+)?)\s*(reais)\b/i),
    kg: pick(/(\d+(?:[.,]\d+)?)\s*(kg|quilos?)\b/i),
    dose: pick(/(\d+(?:[.,]\d+)?)\s*(mg|ml|comprimidos?|doses?)\b/i),
  };
}

export function firstDate(text: string): string | null {
  const iso = text.match(/\d{4}-\d{2}-\d{2}/);
  if (iso) {
    return iso[0];
  }
  const br = text.match(/\d{1,2}\/\d{1,2}\/\d{2,4}/);
  if (br) {
    const [day, month, year] = br[0].split('/');
    const fullYear = year.length === 2 ? `20${year}` : year;
    return `${fullYear}-${month.padStart(2, '0')}-${day.padStart(2, '0')}`;
  }
  const named = text.match(/(\d{1,2})\s+de\s+([A-Za-zçáéíóúãõ]+)\s+de\s+(\d{4})/i);
  if (named) {
    const months: Record<string, string> = {
      janeiro: '01',
      fevereiro: '02',
      marco: '03',
      março: '03',
      abril: '04',
      maio: '05',
      junho: '06',
      julho: '07',
      agosto: '08',
      setembro: '09',
      outubro: '10',
      novembro: '11',
      dezembro: '12',
    };
    const month = months[normalize(named[2])];
    if (month) {
      return `${named[3]}-${month}-${named[1].padStart(2, '0')}`;
    }
  }
  return null;
}

export function firstPlace(text: string): string | null {
  const ubs = text.match(/\b(UBS\s+[\wÁ-ú]+|posto\s+[\wÁ-ú]+)/i);
  if (ubs) {
    return ubs[1].trim();
  }
  const match = text.match(/\b(?:na|no|em)\s+(?!dia\b|mes\b|mês\b)([\wÁ-ú]{3,})/i);
  if (!match) {
    return null;
  }
  const value = match[1].trim();
  if (PLACE_STOP.has(normalize(value))) {
    return null;
  }
  return value;
}

export function firstPerson(text: string): string | null {
  const match = text.match(/\b(?:com a|com o|para a|para o|do meu|da minha)\s+([A-Za-zÁ-ú]{3,})/i);
  if (!match) {
    return null;
  }
  const value = match[1].trim();
  if (PLACE_STOP.has(normalize(value))) {
    return null;
  }
  return titleCase(value);
}

function sentenceHasBoolCue(text: string): boolean {
  const normalized = normalize(text);
  return ['tomado', 'feito', 'ja fiz', 'pago', 'lido'].some((item) => normalized.includes(item));
}

function titleCase(value: string): string {
  return value
    .toLocaleLowerCase('pt-BR')
    .replace(/(^|\s)\S/g, (chunk) => chunk.toLocaleUpperCase('pt-BR'));
}
