import type { RecordField } from './records.ts';

export type SuggestedFieldDraft = {
  id: string;
  name: string;
  kind: string;
  required: boolean;
  order: number;
  enabled: boolean;
};

export function fieldsToDraft(fields: RecordField[]): SuggestedFieldDraft[] {
  return fields.map((field, index) => ({
    id: field.id,
    name: field.name,
    kind: field.kind,
    required: field.required,
    order: field.order ?? index,
    enabled: true,
  }));
}

export function enabledFields(draft: SuggestedFieldDraft[]): RecordField[] {
  return draft
    .filter((field) => field.enabled)
    .map((field, index) => ({
      id: field.id,
      name: field.name.trim() || field.id,
      kind: field.kind,
      required: field.required,
      order: index,
    }));
}

export function toggleField(draft: SuggestedFieldDraft[], id: string): SuggestedFieldDraft[] {
  return draft.map((field) => (field.id === id ? { ...field, enabled: !field.enabled } : field));
}

export function renameField(draft: SuggestedFieldDraft[], id: string, name: string): SuggestedFieldDraft[] {
  return draft.map((field) => (field.id === id ? { ...field, name } : field));
}

export function addField(draft: SuggestedFieldDraft[], name: string, kind = 'text'): SuggestedFieldDraft[] {
  const trimmed = name.trim();
  if (!trimmed) {
    return draft;
  }

  const id = slugifyField(trimmed);
  if (draft.some((field) => field.id === id)) {
    return draft;
  }

  return [
    ...draft,
    {
      id,
      name: trimmed,
      kind,
      required: false,
      order: draft.length,
      enabled: true,
    },
  ];
}

export function fieldKindLabel(field: { id: string; kind: string }): string {
  const kind = field.kind.toLowerCase();
  const id = field.id.toLowerCase();
  if (kind === 'date') {
    return 'data';
  }
  if (kind === 'place') {
    return 'lugar';
  }
  if (kind === 'bool') {
    return 'sim/não';
  }
  if (kind === 'photo') {
    return 'foto';
  }
  if (kind === 'file') {
    return 'arquivo';
  }
  if (kind === 'number') {
    if (id === 'distancia' || id === 'km' || id.includes('km')) {
      return 'número km';
    }
    if (id === 'duracao' || id.includes('minuto')) {
      return 'número min';
    }
    if (id === 'peso' || id.includes('kg')) {
      return 'número kg';
    }
    if (id.includes('preco') || id.includes('valor') || id.includes('reais')) {
      return 'número R$';
    }
    return 'número';
  }
  return 'texto';
}

export function slugifyField(value: string): string {
  const normalized = value
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
  return normalized || 'campo';
}
