export type ShareCircle = {
  id: string;
  name?: string | null;
};

export function toggleCircleShare(selected: string[], circleId: string): string[] {
  if (selected.includes(circleId)) {
    return selected.filter((id) => id !== circleId);
  }
  return [...selected, circleId];
}

export function shareChipLabel(name: string, selected: boolean): string {
  const shown = name.trim() || 'círculo';
  return selected ? `Compartilhar com ${shown}, selecionado` : `Compartilhar com ${shown}`;
}

export function uniqueShareCircles(circles: ShareCircle[]): ShareCircle[] {
  const seenIds = new Set<string>();
  const seenNames = new Set<string>();
  const next: ShareCircle[] = [];
  for (const circle of circles) {
    if (seenIds.has(circle.id)) {
      continue;
    }
    const nameKey = (circle.name ?? '').trim().toLocaleLowerCase('pt-BR') || circle.id;
    if (seenNames.has(nameKey)) {
      continue;
    }
    seenIds.add(circle.id);
    seenNames.add(nameKey);
    next.push(circle);
  }
  return next;
}
