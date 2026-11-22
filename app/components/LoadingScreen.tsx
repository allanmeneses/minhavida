import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';

export function LoadingScreen({ label = 'Carregando...' }: { label?: string }) {
  const { colors } = useTheme();
  return (
    <View style={[styles.wrap, { backgroundColor: colors.bg }]}>
      <ActivityIndicator color={colors.primary} size="large" />
      <Text style={{ color: colors.muted }}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 12,
  },
});
