import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Appearance, useColorScheme } from 'react-native';
import { paletteFor, type ColorPalette, type ThemePreference } from '@/constants/theme';
import { readHighContrast, readThemePreference, writeHighContrast, writeThemePreference } from '@/lib/theme-storage';

type ThemeContextValue = {
  preference: ThemePreference;
  colors: ColorPalette;
  resolved: 'light' | 'dark';
  highContrast: boolean;
  setPreference: (value: ThemePreference) => Promise<void>;
  setHighContrast: (value: boolean) => Promise<void>;
};

const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const system = useColorScheme();
  const [preference, setPreferenceState] = useState<ThemePreference>('system');
  const [highContrast, setHighContrastState] = useState(false);

  useEffect(() => {
    void readThemePreference().then(setPreferenceState);
    void readHighContrast().then(setHighContrastState);
  }, []);

  const resolved: 'light' | 'dark' = preference === 'system'
    ? (system === 'dark' ? 'dark' : 'light')
    : preference;
  const colors = paletteFor(resolved, highContrast);

  const value = useMemo(
    () => ({
      preference,
      colors,
      resolved,
      highContrast,
      setPreference: async (next: ThemePreference) => {
        setPreferenceState(next);
        await writeThemePreference(next);
        Appearance.setColorScheme(next === 'system' ? 'unspecified' : next);
      },
      setHighContrast: async (next: boolean) => {
        setHighContrastState(next);
        await writeHighContrast(next);
      },
    }),
    [preference, colors, resolved, highContrast],
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
