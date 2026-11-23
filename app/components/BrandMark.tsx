import { StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';

export function BrandMark({ compact = false, inverted = false }: { compact?: boolean; inverted?: boolean }) {
  const { colors } = useTheme();
  return (
    <View style={styles.wrap}>
      <View
        style={[
          styles.mark,
          compact && styles.markCompact,
          { backgroundColor: inverted ? colors.white : colors.primary },
        ]}>
        <View
          style={[
            styles.ring,
            { borderColor: inverted ? colors.primary : colors.white },
          ]}
        />
        <View
          style={[
            styles.inner,
            { backgroundColor: inverted ? colors.primary : colors.white },
          ]}
        />
      </View>
      <View>
        <Text
          style={[
            styles.title,
            compact && styles.titleCompact,
            { color: inverted ? colors.white : colors.ink },
          ]}>
          Círculos
        </Text>
        {!compact ? (
          <Text style={[styles.subtitle, { color: inverted ? 'rgba(255,255,255,0.82)' : colors.muted }]}>
            A partir do seu perfil
          </Text>
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
    opacity: 0.7,
  },
  inner: {
    width: 10,
    height: 10,
    borderRadius: 5,
  },
  title: {
    fontSize: 22,
    fontWeight: '800',
  },
  titleCompact: {
    fontSize: 18,
  },
  subtitle: {
    marginTop: 2,
    fontSize: 13,
  },
});
