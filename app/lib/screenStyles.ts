import { StyleSheet } from 'react-native';
import type { ColorPalette } from '@/constants/theme';

export const TAB_BAR_HEIGHT = 76;
export const TAB_SAFE_GAP = 24;
export const MOBILE_SCROLL_BOTTOM = TAB_BAR_HEIGHT + TAB_SAFE_GAP;

export function screenStyles(colors: ColorPalette, compact = false) {
  return StyleSheet.create({
    page: {
      paddingBottom: MOBILE_SCROLL_BOTTOM,
      paddingHorizontal: compact ? 4 : 0,
      maxWidth: 720,
      width: '100%',
      alignSelf: 'center',
      gap: 12,
    },
    centeredPage: {
      paddingBottom: MOBILE_SCROLL_BOTTOM,
      paddingHorizontal: compact ? 4 : 0,
      maxWidth: 560,
      width: '100%',
      alignSelf: 'center',
      gap: 12,
    },
    stickyHeader: {
      position: 'sticky' as unknown as 'relative',
      top: 0,
      zIndex: 2,
      backgroundColor: colors.bg,
      paddingBottom: 8,
      gap: 4,
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
      overflow: 'visible',
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
