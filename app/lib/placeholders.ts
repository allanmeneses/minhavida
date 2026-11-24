export function examplePlaceholder(sample: string): string {
  const trimmed = sample.trim();
  if (!trimmed) {
    return '';
  }
  if (/^ex\.:\s/i.test(trimmed)) {
    return trimmed;
  }
  return `ex.: ${trimmed}`;
}
