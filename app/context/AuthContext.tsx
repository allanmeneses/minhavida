import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Platform } from 'react-native';
import {
  api,
  setRefreshAccess,
  TwoFactorRequiredError,
  type AuthResponse,
  type AuthUser,
  type Profile,
} from '@/lib/api';
import {
  clearToken,
  readRefreshToken,
  readToken,
  writeSession,
} from '@/lib/auth-storage';

type AuthContextValue = {
  loading: boolean;
  token: string | null;
  user: AuthUser | null;
  profile: Profile | null;
  photoRevision: number;
  login: (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<string>;
  verifyTwoFactor: (twoFactorToken: string, code?: string, recoveryCode?: string) => Promise<void>;
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
  const refreshRef = useRef<string | null>(null);
  const tokenRef = useRef<string | null>(null);

  const applySession = useCallback(async (nextToken: string) => {
    const me = await api<AuthUser>('/api/auth/me', { token: nextToken, skipRefresh: true });
    const nextProfile = await api<Profile>('/api/profile', { token: nextToken, skipRefresh: true });
    tokenRef.current = nextToken;
    setToken(nextToken);
    setUser(me);
    setProfile(nextProfile);
  }, []);

  const storeSession = useCallback(async (result: AuthResponse) => {
    if (!result.token) {
      throw new Error('Sessão incompleta.');
    }
    await writeSession(result.token, result.refreshToken);
    refreshRef.current = result.refreshToken;
    await applySession(result.token);
  }, [applySession]);

  useEffect(() => {
    setRefreshAccess(async () => {
      const stored = refreshRef.current ?? (await readRefreshToken());
      if (!stored && Platform.OS !== 'web') {
        return null;
      }
      try {
        const result = await api<AuthResponse>('/api/auth/refresh', {
          method: 'POST',
          body: JSON.stringify({ refreshToken: stored ?? '' }),
          skipRefresh: true,
        });
        if (!result.token) {
          return null;
        }
        await writeSession(result.token, result.refreshToken);
        refreshRef.current = result.refreshToken ?? stored;
        tokenRef.current = result.token;
        setToken(result.token);
        return result.token;
      } catch {
        await clearToken();
        refreshRef.current = null;
        tokenRef.current = null;
        setToken(null);
        setUser(null);
        setProfile(null);
        return null;
      }
    });
    return () => setRefreshAccess(null);
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const stored = await readToken();
        const refresh = await readRefreshToken();
        refreshRef.current = refresh;
        if (!stored || !active) {
          return;
        }
        await applySession(stored);
      } catch {
        const refresh = refreshRef.current ?? (await readRefreshToken());
        if (refresh || Platform.OS === 'web') {
          try {
            const result = await api<AuthResponse>('/api/auth/refresh', {
              method: 'POST',
              body: JSON.stringify({ refreshToken: refresh ?? '' }),
              skipRefresh: true,
            });
            if (result.token && active) {
              await writeSession(result.token, result.refreshToken);
              refreshRef.current = result.refreshToken ?? refresh;
              await applySession(result.token);
              return;
            }
          } catch {
            // cai no limpar sessão
          }
        }
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
    const result = await api<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
      skipRefresh: true,
    });
    if (result.requiresTwoFactor && result.twoFactorToken) {
      throw new TwoFactorRequiredError(result.twoFactorToken);
    }
    await storeSession(result);
  }, [storeSession]);

  const register = useCallback(async (name: string, email: string, password: string) => {
    const result = await api<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ name, email, password }),
      skipRefresh: true,
    });
    await storeSession(result);
    if (!result.token) {
      throw new Error('Sessão incompleta.');
    }
    return result.token;
  }, [storeSession]);

  const verifyTwoFactor = useCallback(async (twoFactorToken: string, code?: string, recoveryCode?: string) => {
    const result = await api<AuthResponse>('/api/auth/2fa/verify', {
      method: 'POST',
      body: JSON.stringify({ twoFactorToken, code, recoveryCode }),
      skipRefresh: true,
    });
    await storeSession(result);
  }, [storeSession]);

  const logout = useCallback(async () => {
    const refresh = refreshRef.current ?? (await readRefreshToken());
    if (refresh || Platform.OS === 'web') {
      try {
        await api('/api/auth/logout', {
          method: 'POST',
          body: JSON.stringify({ refreshToken: refresh ?? '' }),
          skipRefresh: true,
        });
      } catch {
        // mesmo se a rede falhar, limpa o aparelho
      }
    }
    await clearToken();
    refreshRef.current = null;
    tokenRef.current = null;
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
      verifyTwoFactor,
      logout,
      refreshProfile,
      setProfile,
      bumpPhoto: () => setPhotoRevision((value) => value + 1),
    }),
    [loading, token, user, profile, photoRevision, login, register, verifyTwoFactor, logout, refreshProfile],
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
