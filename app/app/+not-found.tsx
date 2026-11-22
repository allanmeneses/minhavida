import { Link, Stack } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';
import { colors } from '@/constants/theme';

export default function NotFoundScreen() {
  return (
    <>
      <Stack.Screen options={{ title: 'Pagina nao encontrada', headerShown: true }} />
      <View style={styles.container}>
        <Text style={styles.title}>Esta tela nao existe.</Text>
        <Link href="/" style={styles.link}>
          <Text style={styles.linkText}>Voltar ao inicio</Text>
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
    backgroundColor: colors.bg,
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.ink,
  },
  link: {
    marginTop: 16,
    paddingVertical: 12,
  },
  linkText: {
    color: colors.primary,
    fontWeight: '600',
  },
});
