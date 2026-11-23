import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Link } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useTheme } from '@/context/ThemeContext';
import { api } from '@/lib/api';

export default function ForgotScreen() {
  const { colors } = useTheme();
  const [email, setEmail] = useState('');
  const [ok, setOk] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      await api('/api/auth/forgot-password', {
        method: 'POST',
        body: JSON.stringify({ email: email.trim() }),
      });
      setOk('Se existir conta neste e-mail, enviamos o código.');
    } catch {
      setError('Não foi possível enviar o e-mail.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <View style={[styles.page, { backgroundColor: colors.bg }]}>
      <Text style={[styles.title, { color: colors.ink }]}>Esqueci a senha</Text>
      <Text style={[styles.lead, { color: colors.muted }]}>Recuperação só por e-mail. Segundo fator fica para depois.</Text>
      <TextField label="E-mail" value={email} onChangeText={setEmail} keyboardType="email-address" autoComplete="email" />
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />
      <Button
        label="Enviar código"
        onPress={() => void submit()}
        loading={busy}
        disabled={!email.trim()}
        disabledHint="Informe o e-mail da conta para enviar o código."
      />
      <Link href="/(auth)/reset" style={[styles.link, { color: colors.primary }]}>
        Já tenho o código
      </Link>
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
  lead: {},
  link: {
    fontWeight: '700',
    textAlign: 'center',
  },
});
