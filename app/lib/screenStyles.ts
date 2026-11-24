import { StyleSheet } from 'react-native';
import type { ColorPalette } from '@/constants/theme';

export function screenStyles(colors: ColorPalette, compact = false) {
  return StyleSheet.create({
    page: {
      paddingBottom: compact ? 128 : 120,
      paddingHorizontal: compact ? 4 : 0,
      maxWidth: 720,
      width: '100%',
      alignSelf: 'center',
      gap: 12,
    },
    title: {
      fontSize: compact ? 24 : 28,
      fontWeight: '800',
      color: colors.ink,
      flexShrink: 1,
    },
    lead: {
      color: colors.muted,
      lineHeight: 22,
      flexShrink: 1,
      flexWrap: 'wrap',
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
      flexShrink: 1,
    },
    section: {
      fontWeight: '800',
      color: colors.ink,
      marginTop: 8,
      fontSize: 18,
    },
  });
}
