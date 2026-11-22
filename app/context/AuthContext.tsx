import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, type AuthUser, type Profile } from '@/lib/api';
import { clearToken, readToken, writeToken } from '@/lib/auth-storage';

type AuthContextValue = {
  loading: boolean;
  token: string | null;
  user: AuthUser | null;
  profile: Profile | null;
  photoRevision: number;
  login: (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshProfile: () => Promise<void>;
  setProfile: (profile: Profile) => void;
  bumpPhoto: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [loading, setLoading] = useState(true);
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<AuthUser | null>(null);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [photoRevision, setPhotoRevision] = useState(0);

  const applySession = useCallback(async (nextToken: string) => {
    const me = await api<AuthUser>('/api/auth/me', { token: nextToken });
    const nextProfile = await api<Profile>('/api/profile', { token: nextToken });
    setToken(nextToken);
    setUser(me);
    setProfile(nextProfile);
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const stored = await readToken();
        if (!stored || !active) {
          return;
        }
        await applySession(stored);
      } catch {
        await clearToken();
        if (active) {
          setToken(null);
          setUser(null);
          setProfile(null);
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    })();
    return () => {
      active = false;
    };
  }, [applySession]);

  const login = useCallback(async (email: string, password: string) => {
    const result = await api<{ token: string; user: AuthUser }>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
    await writeToken(result.token);
    await applySession(result.token);
  }, [applySession]);

  const register = useCallback(async (name: string, email: string, password: string) => {
    const result = await api<{ token: string; user: AuthUser }>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ name, email, password }),
    });
    await writeToken(result.token);
    await applySession(result.token);
  }, [applySession]);

  const logout = useCallback(async () => {
    await clearToken();
    setToken(null);
    setUser(null);
    setProfile(null);
  }, []);

  const refreshProfile = useCallback(async () => {
    if (!token) {
      return;
    }
    const nextProfile = await api<Profile>('/api/profile', { token });
    const me = await api<AuthUser>('/api/auth/me', { token });
    setProfile(nextProfile);
    setUser(me);
  }, [token]);

  const value = useMemo(
    () => ({
      loading,
      token,
      user,
      profile,
      photoRevision,
      login,
      register,
      logout,
      refreshProfile,
      setProfile,
      bumpPhoto: () => setPhotoRevision((value) => value + 1),
    }),
    [loading, token, user, profile, photoRevision, login, register, logout, refreshProfile],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error('useAuth precisa estar dentro de AuthProvider.');
  }
  return value;
}
