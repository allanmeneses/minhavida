export const SHORTCUT_FLASH_MS = 1000;

export function flashStillVisible(startedAt: number, now: number, durationMs = SHORTCUT_FLASH_MS): boolean {
  return now - startedAt < durationMs;
}
