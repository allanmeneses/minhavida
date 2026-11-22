export const LIST_KINDS = ['shopping', 'wishes', 'materials', 'other'] as const;
export type ListKind = (typeof LIST_KINDS)[number];
export const LIST_KIND_LABEL: Record<ListKind, string> = {
  shopping: 'compras',
  wishes: 'desejos',
  materials: 'materiais',
  other: 'outra',
};

export const LIST_ACCESS = ['view', 'add', 'complete', 'manage'] as const;
export type ListAccess = (typeof LIST_ACCESS)[number];
export const LIST_ACCESS_LABEL: Record<ListAccess, string> = {
  view: 'so o dono edita',
  add: 'membros adicionam',
  complete: 'membros marcam feito',
  manage: 'membros editam e excluem',
};

export function nextOf<T extends string>(values: readonly T[], current: T): T {
  const index = values.indexOf(current);
  return values[(index + 1) % values.length];
}
