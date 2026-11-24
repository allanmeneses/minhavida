export function multilineInputHeight(
  value: string,
  options?: { fontSize?: number; minHeight?: number; charsPerLine?: number },
): number {
  const fontSize = options?.fontSize ?? 14;
  const minHeight = options?.minHeight ?? 88;
  const charsPerLine = options?.charsPerLine ?? 32;
  const lineHeight = fontSize + 8;
  const padding = 20;
  let lines = 0;
  for (const part of value.split('\n')) {
    lines += Math.max(1, Math.ceil(Math.max(part.length, 1) / charsPerLine));
  }
  return Math.max(minHeight, padding + lines * lineHeight);
}
