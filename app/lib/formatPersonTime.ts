export function formatPersonTime(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR');
}
