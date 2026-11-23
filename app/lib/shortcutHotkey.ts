export function isTypingTarget(target: EventTarget | null): boolean {
  if (target == null || typeof target !== 'object') {
    return false;
  }
  const el = target as { tagName?: string; isContentEditable?: boolean };
  const tag = (el.tagName ?? '').toLowerCase();
  return tag === 'input' || tag === 'textarea' || tag === 'select' || Boolean(el.isContentEditable);
}

export function shortcutIndexFromKey(key: string, typing: boolean): number | null {
  if (typing) {
    return null;
  }
  if (key.length === 1 && key >= '1' && key <= '9') {
    return Number(key) - 1;
  }
  return null;
}
