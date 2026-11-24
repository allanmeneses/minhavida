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
