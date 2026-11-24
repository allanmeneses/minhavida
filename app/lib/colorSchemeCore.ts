export type ThemePreference = 'system' | 'light' | 'dark';

export function colorSchemeValue(preference: ThemePreference): 'light' | 'dark' | 'unspecified' {
  return preference === 'system' ? 'unspecified' : preference;
}

export function applyColorSchemeSafe(
  setColorScheme: ((value: 'light' | 'dark' | 'unspecified' | null) => void) | undefined,
  preference: ThemePreference,
): boolean {
  if (typeof setColorScheme !== 'function') {
    return false;
  }
  setColorScheme(colorSchemeValue(preference));
  return true;
}

export function webColorScheme(preference: ThemePreference): string {
  return preference === 'system' ? 'light dark' : preference;
}
