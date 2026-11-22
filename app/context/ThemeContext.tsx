import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Appearance, useColorScheme } from 'react-native';
import { darkColors, lightColors, type ColorPalette, type ThemePreference } from '@/constants/theme';
import { readThemePreference, writeThemePreference } from '@/lib/theme-storage';

type ThemeContextValue = {
  preference: ThemePreference;
  colors: ColorPalette;
  resolved: 'light' | 'dark';
  setPreference: (value: ThemePreference) => Promise<void>;
};

const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const system = useColorScheme();
  const [preference, setPreferenceState] = useState<ThemePreference>('system');

  useEffect(() => {
    void readThemePreference().then(setPreferenceState);
  }, []);

  const resolved: 'light' | 'dark' = preference === 'system'
    ? (system === 'dark' ? 'dark' : 'light')
    : preference;
  const colors = resolved === 'dark' ? darkColors : lightColors;

  const value = useMemo(
    () => ({
      preference,
      colors,
      resolved,
      setPreference: async (next: ThemePreference) => {
        setPreferenceState(next);
        await writeThemePreference(next);
        Appearance.setColorScheme(next === 'system' ? 'unspecified' : next);
      },
    }),
    [preference, colors, resolved],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const value = useContext(ThemeContext);
  if (!value) {
    throw new Error('useTheme precisa estar dentro de ThemeProvider.');
  }
  return value;
}
