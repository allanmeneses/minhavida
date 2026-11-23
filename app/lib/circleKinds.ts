export const CIRCLE_KINDS = ['lasting', 'temporary', 'daily'] as const;
export type CircleKind = (typeof CIRCLE_KINDS)[number];

export const CIRCLE_KIND_LABEL: Record<CircleKind, string> = {
  lasting: 'Duradouro',
  temporary: 'Temporário',
  daily: 'Diário',
};

export function canArchiveKind(kind: string): boolean {
  return kind === 'temporary' || kind === 'daily';
}
