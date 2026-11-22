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
