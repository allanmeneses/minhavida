import { StyleSheet } from 'react-native';
import type { ColorPalette } from '@/constants/theme';

export function screenStyles(colors: ColorPalette) {
  return StyleSheet.create({
    page: {
      paddingBottom: 48,
      maxWidth: 720,
      width: '100%',
      alignSelf: 'center',
      gap: 12,
    },
    title: {
      fontSize: 28,
      fontWeight: '800',
      color: colors.ink,
    },
    lead: {
      color: colors.muted,
      lineHeight: 22,
    },
    kicker: {
      color: colors.primary,
      fontWeight: '700',
      textTransform: 'uppercase',
      fontSize: 12,
      letterSpacing: 0.6,
    },
    card: {
      backgroundColor: colors.surface,
      borderRadius: 12,
      padding: 14,
      borderWidth: 1,
      borderColor: colors.line,
      gap: 8,
    },
    item: {
      fontWeight: '700',
      color: colors.ink,
    },
    section: {
      fontWeight: '800',
      color: colors.ink,
      marginTop: 8,
      fontSize: 18,
    },
  });
}
