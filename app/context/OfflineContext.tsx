import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { api, ApiError, setReadCache, setWriteCache } from '@/lib/api';
import {
  applySyncResult,
  emptyCache,
  enqueue,
  nextPending,
  readCache,
  writeCache,
  type ListQueueAction,
  type ReadCache,
} from '@/lib/offline';
import { useAuth } from '@/context/AuthContext';

type OfflineContextValue = {
  online: boolean;
  warning: string | null;
  pending: number;
  enqueueAdd: (listId: string, payload: { text: string; quantity?: string | null; note?: string | null }) => ListQueueAction;
  enqueueComplete: (listId: string, itemId: string) => ListQueueAction;
  syncQueue: () => Promise<void>;
  clearWarning: () => void;
};

const OfflineContext = createContext<OfflineContextValue | null>(null);

function currentOnline(): boolean {
  if (typeof navigator === 'undefined') {
    return true;
  }
  return navigator.onLine;
}

export function OfflineProvider({ children }: { children: ReactNode }) {
  const { token } = useAuth();
  const [online, setOnline] = useState(currentOnline);
  const [warning, setWarning] = useState<string | null>(null);
  const [queue, setQueue] = useState<ListQueueAction[]>([]);
  const cacheRef = useRef<ReadCache>(emptyCache());
  const queueRef = useRef<ListQueueAction[]>([]);
  const tokenRef = useRef(token);
  const flushing = useRef(false);

  useEffect(() => {
    queueRef.current = queue;
  }, [queue]);

  useEffect(() => {
    tokenRef.current = token;
    if (!token) {
      cacheRef.current = emptyCache();
      setQueue([]);
      queueRef.current = [];
    }
  }, [token]);

  useEffect(() => {
    setReadCache((path) => readCache(cacheRef.current, path));
    setWriteCache((path, value) => {
      cacheRef.current = writeCache(cacheRef.current, path, value);
    });
    return () => {
      setReadCache(null);
      setWriteCache(null);
    };
  }, []);

  const syncQueue = useCallback(async () => {
    if (flushing.current || !tokenRef.current) {
      return;
    }
    flushing.current = true;
    try {
      let current = queueRef.current;
      while (current.length > 0) {
        const action = nextPending(current);
        if (!action) {
          break;
        }
        try {
          if (action.type === 'add-item') {
            await api(`/api/lists/${action.listId}/items`, {
              method: 'POST',
              token: tokenRef.current,
              body: JSON.stringify({
                text: action.payload.text,
                quantity: action.payload.quantity ?? null,
                note: action.payload.note ?? null,
              }),
            });
            const next = applySyncResult(current, action, 201, '');
            current = next.queue;
            setQueue(current);
            queueRef.current = current;
          } else {
            await api(`/api/lists/${action.listId}/items/${action.payload.itemId}/done`, {
              method: 'POST',
              token: tokenRef.current,
            });
            const next = applySyncResult(current, action, 200, '');
            current = next.queue;
            setQueue(current);
            queueRef.current = current;
          }
        } catch (err) {
          const status = err instanceof ApiError ? err.status : 0;
          const message = err instanceof Error ? err.message : 'Esta lista não permite essa ação.';
          if (status === 0) {
            return;
          }
          const next = applySyncResult(current, action, status, message);
          current = next.queue;
          setQueue(current);
          queueRef.current = current;
          if (next.warning) {
            setWarning(next.warning);
          }
        }
      }
    } finally {
      flushing.current = false;
    }
  }, []);

  useEffect(() => {
    const onOnline = () => {
      setOnline(true);
      void syncQueue();
    };
    const onOffline = () => setOnline(false);
    if (typeof window !== 'undefined') {
      window.addEventListener('online', onOnline);
      window.addEventListener('offline', onOffline);
    }
    return () => {
      if (typeof window !== 'undefined') {
        window.removeEventListener('online', onOnline);
        window.removeEventListener('offline', onOffline);
      }
    };
  }, [syncQueue]);

  const enqueueAdd = useCallback(
    (listId: string, payload: { text: string; quantity?: string | null; note?: string | null }) => {
      const action: ListQueueAction = {
        id: `local-${Date.now()}-${Math.random().toString(16).slice(2)}`,
        type: 'add-item',
        listId,
        payload,
        createdAt: Date.now(),
      };
      setQueue((current) => {
        const next = enqueue(current, action);
        queueRef.current = next;
        return next;
      });
      return action;
    },
    [],
  );

  const enqueueComplete = useCallback((listId: string, itemId: string) => {
    const action: ListQueueAction = {
      id: `local-${Date.now()}-${Math.random().toString(16).slice(2)}`,
      type: 'complete-item',
      listId,
      payload: { itemId },
      createdAt: Date.now(),
    };
    setQueue((current) => {
      const next = enqueue(current, action);
      queueRef.current = next;
      return next;
    });
    return action;
  }, []);

  const value = useMemo(
    () => ({
      online,
      warning,
      pending: queue.length,
      enqueueAdd,
      enqueueComplete,
      syncQueue,
      clearWarning: () => setWarning(null),
    }),
    [online, warning, queue.length, enqueueAdd, enqueueComplete, syncQueue],
  );

  return <OfflineContext.Provider value={value}>{children}</OfflineContext.Provider>;
}

export function useOffline(): OfflineContextValue {
  const value = useContext(OfflineContext);
  if (!value) {
    throw new Error('useOffline precisa estar dentro de OfflineProvider.');
  }
  return value;
}
