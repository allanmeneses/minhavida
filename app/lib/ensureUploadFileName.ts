const MimeExtension: Record<string, string> = {
  'image/jpeg': '.jpg',
  'image/png': '.png',
  'image/webp': '.webp',
  'text/plain': '.txt',
  'application/pdf': '.pdf',
};

export function ensureUploadFileName(
  fileName: string | null | undefined,
  mimeType: string | null | undefined,
  fallback: string,
): string {
  const trimmed = fileName?.trim();
  const name = trimmed && trimmed.length > 0 ? trimmed : fallback;
  if (name.includes('.')) {
    return name;
  }

  const ext = mimeType ? MimeExtension[mimeType.toLowerCase()] : undefined;
  return ext ? `${name}${ext}` : name;
}
