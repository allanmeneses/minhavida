export type ColorPalette = {
  bg: string;
  surface: string;
  ink: string;
  muted: string;
  placeholder: string;
  primary: string;
  primaryDark: string;
  primarySoft: string;
  accent: string;
  danger: string;
  dangerSoft: string;
  line: string;
  white: string;
};

export const lightColors: ColorPalette = {
  bg: '#F4EFE8',
  surface: '#FFFBF7',
  ink: '#1F1A16',
  muted: '#6B625A',
  placeholder: '#C8BFB6',
  primary: '#1F6F66',
  primaryDark: '#16564F',
  primarySoft: '#D7EBE8',
  accent: '#C45C26',
  danger: '#B42318',
  dangerSoft: '#F8E4E2',
  line: '#E6DDD2',
  white: '#FFFFFF',
};

export const darkColors: ColorPalette = {
  bg: '#161311',
  surface: '#221E1B',
  ink: '#F4EFE8',
  muted: '#B6A99C',
  placeholder: '#6B625A',
  primary: '#7BC4BA',
  primaryDark: '#D7EBE8',
  primarySoft: '#1F3D39',
  accent: '#E08A55',
  danger: '#F0A39C',
  dangerSoft: '#3A1E1C',
  line: '#3A322C',
  white: '#221E1B',
};

export const colors = lightColors;

export const space = {
  xs: 6,
  sm: 10,
  md: 16,
  lg: 24,
  xl: 40,
};

export type ThemePreference = 'system' | 'light' | 'dark';

export const highContrastLight: ColorPalette = {
  bg: '#FFFFFF',
  surface: '#FFFFFF',
  ink: '#000000',
  muted: '#111111',
  placeholder: '#767676',
  primary: '#004D40',
  primaryDark: '#000000',
  primarySoft: '#D7EBE8',
  accent: '#7A2E00',
  danger: '#7A0000',
  dangerSoft: '#FFE8E8',
  line: '#000000',
  white: '#FFFFFF',
};

export const highContrastDark: ColorPalette = {
  bg: '#000000',
  surface: '#000000',
  ink: '#FFFFFF',
  muted: '#F2F2F2',
  placeholder: '#A0A0A0',
  primary: '#5CFFEA',
  primaryDark: '#FFFFFF',
  primarySoft: '#00332C',
  accent: '#FFB86A',
  danger: '#FF8A80',
  dangerSoft: '#3A0000',
  line: '#FFFFFF',
  white: '#000000',
};

export function paletteFor(resolved: 'light' | 'dark', highContrast: boolean): ColorPalette {
  if (highContrast) {
    return resolved === 'dark' ? highContrastDark : highContrastLight;
  }
  return resolved === 'dark' ? darkColors : lightColors;
}
