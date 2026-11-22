export const CIRCLE_KINDS = ['lasting', 'temporary', 'daily'] as const;
export type CircleKind = (typeof CIRCLE_KINDS)[number];

export const CIRCLE_KIND_LABEL: Record<CircleKind, string> = {
  lasting: 'Duradouro',
  temporary: 'Temporario',
  daily: 'Diario',
};

export function canArchiveKind(kind: string): boolean {
  return kind === 'temporary' || kind === 'daily';
}
