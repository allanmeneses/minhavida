import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Link, router } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError } from '@/lib/api';

export default function ResetScreen() {
  const { colors } = useTheme();
  const [token, setToken] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      await api('/api/auth/reset-password', {
        method: 'POST',
        body: JSON.stringify({ token: token.trim(), password }),
      });
      router.replace('/(auth)/login');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível redefinir a senha.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <View style={[styles.page, { backgroundColor: colors.bg }]}>
      <Text style={[styles.title, { color: colors.ink }]}>Nova senha</Text>
      <TextField label="Código do e-mail" value={token} onChangeText={setToken} />
      <TextField label="Nova senha" value={password} onChangeText={setPassword} secureTextEntry />
      <Feedback message={error} />
      <Button
        label="Salvar senha"
        onPress={() => void submit()}
        loading={busy}
        disabled={!token.trim() || password.length < 8}
        disabledHint={
          !token.trim()
            ? 'Cole o código recebido por e-mail.'
            : 'A senha precisa ter pelo menos 8 caracteres.'
        }
      />
      <Link href="/(auth)/login" style={[styles.link, { color: colors.primary }]}>
        Voltar ao login
      </Link>
    </View>
  );
}

const styles = StyleSheet.create({
  page: {
    flex: 1,
    padding: 24,
    justifyContent: 'center',
    gap: 12,
    maxWidth: 480,
    width: '100%',
    alignSelf: 'center',
  },
  title: {
    fontSize: 28,
    fontWeight: '800',
  },
  link: {
    fontWeight: '700',
    textAlign: 'center',
  },
});
