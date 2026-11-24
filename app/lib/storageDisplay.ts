export function formatStorage(bytes: number): string {
  const safe = Math.max(0, bytes);
  if (safe < 1024) {
    return `${safe} B`;
  }
  if (safe < 1024 * 1024) {
    const kilo = safe / 1024;
    const shown = kilo >= 10 ? Math.round(kilo).toString() : kilo.toLocaleString('pt-BR', { maximumFractionDigits: 1 });
    return `${shown} KB`;
  }
  const mega = safe / (1024 * 1024);
  const shown = mega >= 10 ? Math.round(mega).toString() : mega.toLocaleString('pt-BR', { maximumFractionDigits: 1 });
  return `${shown} MB`;
}

export function storageUsageLine(usedBytes: number, limitBytes: number): string {
  return `${formatStorage(usedBytes)} de ${formatStorage(limitBytes)}. Sem separar por tipo.`;
}
