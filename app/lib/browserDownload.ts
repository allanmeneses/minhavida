export const BLOB_REVOKE_DELAY_MS = 10_000;

export type DelayFn = (fn: () => void, ms: number) => unknown;

export function triggerBrowserDownload(
  blob: Blob,
  fileName: string,
  delay: DelayFn = (fn, ms) => setTimeout(fn, ms),
): void {
  if (typeof document === 'undefined' || typeof URL === 'undefined') {
    throw new Error('Este aparelho não baixa arquivo aqui.');
  }

  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  delay(() => {
    anchor.remove();
    URL.revokeObjectURL(url);
  }, BLOB_REVOKE_DELAY_MS);
}
