export type ColorPalette = {
  bg: string;
  surface: string;
  ink: string;
  muted: string;
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
