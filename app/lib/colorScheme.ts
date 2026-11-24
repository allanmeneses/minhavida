import { Appearance, Platform } from 'react-native';
import type { ThemePreference } from '../constants/theme.ts';
import { applyColorSchemeSafe, webColorScheme } from './colorSchemeCore.ts';

type AppearanceApi = {
  setColorScheme?: (value: 'light' | 'dark' | 'unspecified' | null) => void;
};

export function applyColorScheme(preference: ThemePreference): void {
  const appearance = Appearance as AppearanceApi;
  applyColorSchemeSafe(appearance.setColorScheme, preference);

  if (Platform.OS !== 'web' || typeof document === 'undefined') {
    return;
  }

  const root = document.documentElement;
  root.style.colorScheme = webColorScheme(preference);
  if (preference === 'system') {
    delete root.dataset.theme;
  } else {
    root.dataset.theme = preference;
  }
}

export function applyDocumentColors(background: string, ink: string): void {
  if (Platform.OS !== 'web' || typeof document === 'undefined') {
    return;
  }

  document.body.style.backgroundColor = background;
  document.body.style.color = ink;
}
