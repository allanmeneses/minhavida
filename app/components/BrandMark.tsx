import { StyleSheet, Text, View } from 'react-native';
import { colors } from '@/constants/theme';

export function BrandMark({ compact = false, inverted = false }: { compact?: boolean; inverted?: boolean }) {
  return (
    <View style={styles.wrap}>
      <View style={[styles.mark, compact && styles.markCompact, inverted && styles.markInverted]}>
        <View style={[styles.ring, inverted && styles.ringInverted]} />
        <View style={[styles.inner, inverted && styles.innerInverted]} />
      </View>
      <View>
        <Text style={[styles.title, compact && styles.titleCompact, inverted && styles.titleInverted]}>Circulos</Text>
        {!compact ? (
          <Text style={[styles.subtitle, inverted && styles.subtitleInverted]}>A partir do seu perfil</Text>
        ) : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  mark: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.primary,
    alignItems: 'center',
    justifyContent: 'center',
  },
  markCompact: {
    width: 32,
    height: 32,
    borderRadius: 16,
  },
  ring: {
    position: 'absolute',
    width: '68%',
    height: '68%',
    borderRadius: 999,
    borderWidth: 2,
    borderColor: colors.white,
    opacity: 0.7,
  },
  inner: {
    width: 10,
    height: 10,
    borderRadius: 5,
    backgroundColor: colors.white,
  },
  title: {
    fontSize: 22,
    fontWeight: '800',
    color: colors.ink,
  },
  titleCompact: {
    fontSize: 18,
  },
  subtitle: {
    marginTop: 2,
    color: colors.muted,
    fontSize: 13,
  },
  markInverted: {
    backgroundColor: colors.white,
  },
  ringInverted: {
    borderColor: colors.primary,
  },
  innerInverted: {
    backgroundColor: colors.primary,
  },
  titleInverted: {
    color: colors.white,
  },
  subtitleInverted: {
    color: 'rgba(255,255,255,0.82)',
  },
});
