const KEY = 'circulos.pendingInvite';

export function savePendingInvite(token: string): void {
  if (typeof sessionStorage === 'undefined') {
    return;
  }
  sessionStorage.setItem(KEY, token);
}

export function readPendingInvite(): string | null {
  if (typeof sessionStorage === 'undefined') {
    return null;
  }
  return sessionStorage.getItem(KEY);
}

export function clearPendingInvite(): void {
  if (typeof sessionStorage === 'undefined') {
    return;
  }
  sessionStorage.removeItem(KEY);
}
