export type RecordFieldKind = 'text' | 'number' | 'date' | 'place' | 'bool' | 'select' | 'photo' | 'file';

export type RecordField = {
  id: string;
  name: string;
  kind: RecordFieldKind | string;
  required: boolean;
  order: number;
  options?: string[] | null;
};

export type RecordTypeItem = {
  id: string;
  name: string;
  description: string;
  slug: string;
  gallery: boolean;
  originTemplateId: string | null;
  fields: RecordField[];
  quickFields: RecordField[];
  createdAt: string;
  lastUsedAt: string | null;
  recordCount: number;
  hasDate: boolean;
  hasNumber: boolean;
};

export type LifeRecordItem = {
  id: string;
  typeId: string;
  typeName: string;
  title: string;
  body: string | null;
  occurredOn: string | null;
  numberValue: number | null;
  fields: Record<string, unknown>;
  hasPhoto: boolean;
  hasFile: boolean;
  fileName: string | null;
  reminderId: string | null;
  circleIds: string[];
  createdAt: string;
  updatedAt: string;
};

export type RecordSuggestion = {
  typeId: string | null;
  isNewType: boolean;
  type: {
    name: string;
    description: string;
    slug: string;
    galleryTypeId: string | null;
    existingTypeId: string | null;
    fields: RecordField[];
  };
  fields: Record<string, string | null>;
  summary: string;
  transcript: string | null;
};

export type HomeRecordType = {
  id: string;
  name: string;
  lastUsedAt: string | null;
  recordCount: number;
};

export function quickFields(fields: RecordField[]): RecordField[] {
  const ordered = [...fields]
    .filter((field) => field.kind !== 'photo' && field.kind !== 'file')
    .sort((left, right) => left.order - right.order);
  return ordered.slice(0, 3);
}

export function extraFields(fields: RecordField[]): RecordField[] {
  const quick = new Set(quickFields(fields).map((field) => field.id));
  return fields.filter((field) => !quick.has(field.id));
}

export function suggestionDraft(suggestion: RecordSuggestion): Record<string, string> {
  const next: Record<string, string> = {};
  for (const [key, value] of Object.entries(suggestion.fields ?? {})) {
    next[key] = value ?? '';
  }
  return next;
}

export function emptyDraft(fields: RecordField[]): Record<string, string> {
  const next: Record<string, string> = {};
  for (const field of fields) {
    next[field.id] = '';
  }
  return next;
}

export function acceptPayload(
  suggestion: RecordSuggestion,
  draft: Record<string, string>,
  typeId: string | null,
): {
  typeId?: string;
  createTypeIfMissing: boolean;
  newType?: { name: string; description: string; fields: RecordField[] };
  fields: Record<string, string>;
} {
  const fields: Record<string, string> = {};
  for (const [key, value] of Object.entries(draft)) {
    if (value.trim().length > 0) {
      fields[key] = value.trim();
    }
  }
  if (typeId) {
    return { typeId, createTypeIfMissing: false, fields };
  }
  return {
    createTypeIfMissing: true,
    newType: {
      name: suggestion.type.name,
      description: suggestion.type.description,
      fields: suggestion.type.fields,
    },
    fields,
  };
}

export function recentTypeChips(types: HomeRecordType[], limit = 6): HomeRecordType[] {
  return [...types]
    .sort((left, right) => (right.lastUsedAt ?? '').localeCompare(left.lastUsedAt ?? '') || left.name.localeCompare(right.name, 'pt-BR'))
    .slice(0, limit);
}

export function kindFromSlug(slug: string | undefined): string {
  switch (slug) {
    case 'vacinas':
      return 'vaccine';
    case 'diario':
      return 'diary';
    case 'documento':
      return 'document';
    case 'lembretes':
    case 'remedio':
      return 'reminder';
    default:
      return 'custom';
  }
}

export function fieldInputType(kind: string): 'text' | 'date' | 'numeric' {
  if (kind === 'date') {
    return 'date';
  }
  if (kind === 'number') {
    return 'numeric';
  }
  return 'text';
}

export function tagsFrom(fields: Record<string, unknown> | undefined): string[] {
  const raw = fields?.tags;
  if (typeof raw === 'string') {
    return raw
      .split(',')
      .map((item) => item.trim())
      .filter((item) => item.length > 0);
  }
  if (Array.isArray(raw)) {
    return raw.map((item) => String(item).trim()).filter((item) => item.length > 0);
  }
  return [];
}

export function rejectedNotice(): string {
  return 'Nada foi guardado. Você recusou a sugestão.';
}

export function voiceNeedsNetwork(): string {
  return 'A voz precisa de rede para transcrever.';
}

export function transcriptionUnavailable(): string {
  return 'A transcrição de áudio não está disponível agora.';
}
