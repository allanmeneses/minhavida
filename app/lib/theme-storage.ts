import { Platform } from 'react-native';
import type { ThemePreference } from '@/constants/theme';

const KEY = 'circulos.theme';

export async function readThemePreference(): Promise<ThemePreference> {
  const value = Platform.OS === 'web'
    ? globalThis.localStorage?.getItem(KEY)
    : await (await import('expo-secure-store')).getItemAsync(KEY);
  if (value === 'light' || value === 'dark' || value === 'system') {
    return value;
  }
  return 'system';
}

export async function writeThemePreference(value: ThemePreference): Promise<void> {
  if (Platform.OS === 'web') {
    globalThis.localStorage?.setItem(KEY, value);
    return;
  }
  await (await import('expo-secure-store')).setItemAsync(KEY, value);
}

const CONTRAST_KEY = 'circulos.contrast';

export async function readHighContrast(): Promise<boolean> {
  const value = Platform.OS === 'web'
    ? globalThis.localStorage?.getItem(CONTRAST_KEY)
    : await (await import('expo-secure-store')).getItemAsync(CONTRAST_KEY);
  return value === 'high';
}

export async function writeHighContrast(value: boolean): Promise<void> {
  const stored = value ? 'high' : 'normal';
  if (Platform.OS === 'web') {
    globalThis.localStorage?.setItem(CONTRAST_KEY, stored);
    return;
  }
  await (await import('expo-secure-store')).setItemAsync(CONTRAST_KEY, stored);
}
