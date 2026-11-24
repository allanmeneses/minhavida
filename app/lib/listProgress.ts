export function listProgressLabel(doneCount: number, itemCount: number): string {
  const done = Math.max(0, doneCount);
  const total = Math.max(0, itemCount);
  return `${done} de ${total} feitos`;
}
