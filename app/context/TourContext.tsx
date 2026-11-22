import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { readTourDone, writeTourDone } from '@/lib/tour-storage';

type TourContextValue = {
  ready: boolean;
  done: boolean;
  finish: () => Promise<void>;
  replay: () => Promise<void>;
};

const TourContext = createContext<TourContextValue | null>(null);

export function TourProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [done, setDone] = useState(true);

  useEffect(() => {
    void readTourDone().then((value) => {
      setDone(value);
      setReady(true);
    });
  }, []);

  const finish = useCallback(async () => {
    await writeTourDone(true);
    setDone(true);
  }, []);

  const replay = useCallback(async () => {
    await writeTourDone(false);
    setDone(false);
  }, []);

  const value = useMemo(() => ({ ready, done, finish, replay }), [ready, done, finish, replay]);
  return <TourContext.Provider value={value}>{children}</TourContext.Provider>;
}

export function useTour(): TourContextValue {
  const value = useContext(TourContext);
  if (!value) {
    throw new Error('useTour precisa estar dentro de TourProvider.');
  }
  return value;
}
