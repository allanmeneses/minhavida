import { Link, Stack } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';

export default function NotFoundScreen() {
  const { colors } = useTheme();
  return (
    <>
      <Stack.Screen options={{ headerShown: false, title: 'Página não encontrada' }} />
      <View style={[styles.container, { backgroundColor: colors.bg }]}>
      <Text style={[styles.kicker, { color: colors.primary }]}>Círculos</Text>
      <Text style={[styles.title, { color: colors.ink }]}>Página não encontrada</Text>
      <Text style={[styles.body, { color: colors.muted }]}>Esta tela não existe.</Text>
      <Link href="/" style={styles.link}>
        <Text style={[styles.linkText, { color: colors.primary }]}>Voltar ao início</Text>
      </Link>
      </View>
    </>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 24,
    gap: 8,
  },
  kicker: {
    fontWeight: '700',
    textTransform: 'uppercase',
    fontSize: 12,
    letterSpacing: 0.6,
  },
  title: {
    fontSize: 24,
    fontWeight: '800',
    textAlign: 'center',
  },
  body: {
    fontSize: 16,
    textAlign: 'center',
  },
  link: {
    marginTop: 16,
    paddingVertical: 12,
  },
  linkText: {
    fontWeight: '600',
  },
});
