export type ListQueueAction = {
  id: string;
  type: 'add-item' | 'complete-item';
  listId: string;
  payload: {
    text?: string;
    quantity?: string | null;
    note?: string | null;
    itemId?: string;
  };
  createdAt: number;
};

export type ReadCache = {
  profile?: unknown;
  home?: unknown;
  reminders?: unknown;
  listsSolo?: unknown;
  listById: Record<string, unknown>;
};

export function emptyCache(): ReadCache {
  return { listById: {} };
}

export function writeCache(cache: ReadCache, path: string, value: unknown): ReadCache {
  if (path === '/api/profile') {
    return { ...cache, profile: value };
  }
  if (path === '/api/home') {
    return { ...cache, home: value };
  }
  if (path === '/api/reminders') {
    return { ...cache, reminders: value };
  }
  if (path === '/api/lists' || path.startsWith('/api/lists?')) {
    return { ...cache, listsSolo: value };
  }
  const list = path.match(/^\/api\/lists\/([^/?]+)$/);
  if (list) {
    return { ...cache, listById: { ...cache.listById, [list[1]]: value } };
  }
  return cache;
}

export function readCache<T>(cache: ReadCache, path: string): T | undefined {
  if (path === '/api/profile') {
    return cache.profile as T | undefined;
  }
  if (path === '/api/home') {
    return cache.home as T | undefined;
  }
  if (path === '/api/reminders') {
    return cache.reminders as T | undefined;
  }
  if (path === '/api/lists' || path.startsWith('/api/lists?')) {
    return cache.listsSolo as T | undefined;
  }
  const list = path.match(/^\/api\/lists\/([^/?]+)$/);
  if (list) {
    return cache.listById[list[1]] as T | undefined;
  }
  return undefined;
}

export function enqueue(queue: ListQueueAction[], action: ListQueueAction): ListQueueAction[] {
  return [...queue, action];
}

export function discard(queue: ListQueueAction[], id: string): ListQueueAction[] {
  return queue.filter((item) => item.id !== id);
}

export function nextPending(queue: ListQueueAction[]): ListQueueAction | undefined {
  return queue[0];
}

export function forbiddenWarning(status: number, message: string): string | null {
  if (status === 403) {
    return message || 'Esta lista nao permite essa acao.';
  }
  return null;
}

export function applySyncResult(
  queue: ListQueueAction[],
  action: ListQueueAction,
  status: number,
  message: string,
): { queue: ListQueueAction[]; warning: string | null } {
  const without = discard(queue, action.id);
  if (status === 403) {
    return { queue: without, warning: forbiddenWarning(status, message) };
  }
  if (status >= 200 && status < 300) {
    return { queue: without, warning: null };
  }
  return { queue, warning: null };
}
