import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { colors } from '@/constants/theme';

export function LoadingScreen({ label = 'Carregando...' }: { label?: string }) {
  return (
    <View style={styles.wrap}>
      <ActivityIndicator color={colors.primary} size="large" />
      <Text style={styles.label}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.bg,
    gap: 12,
  },
  label: {
    color: colors.muted,
  },
});
