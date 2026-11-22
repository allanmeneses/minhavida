import { useState } from 'react';
import { KeyboardAvoidingView, Platform, StyleSheet, Text, View } from 'react-native';
import { Link, router } from 'expo-router';
import { BrandMark } from '@/components/BrandMark';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { colors } from '@/constants/theme';
import { useAuth } from '@/context/AuthContext';
import { ApiError } from '@/lib/api';

export default function RegisterScreen() {
  const { register } = useAuth();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit() {
    setError(null);
    setBusy(true);
    try {
      await register(name.trim(), email.trim(), password);
      router.replace('/(app)/home');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel criar a conta.');
    } finally {
      setBusy(false);
    }
  }

  const form = (
    <View style={styles.form}>
      <TextField
        label="Nome"
        value={name}
        onChangeText={setName}
        placeholder="Como voce quer ser chamado"
        autoCapitalize="words"
        autoComplete="name"
      />
      <TextField
        label="E-mail"
        value={email}
        onChangeText={setEmail}
        placeholder="voce@exemplo.com"
        keyboardType="email-address"
        autoComplete="email"
      />
      <TextField
        label="Senha"
        value={password}
        onChangeText={setPassword}
        placeholder="Pelo menos 8 caracteres"
        secureTextEntry
        autoComplete="password"
      />
      <Feedback message={error} />
      <Button
        label="Criar conta"
        onPress={onSubmit}
        loading={busy}
        disabled={!name || !email || password.length < 8}
      />
      <Text style={styles.hint}>
        Ja tem conta?{' '}
        <Link href="/(auth)/login" style={styles.link}>
          Entrar
        </Link>
      </Text>
    </View>
  );

  if (Platform.OS === 'web') {
    return (
      <View style={styles.webSplit}>
        <View style={styles.webBrand}>
          <BrandMark inverted />
          <Text style={styles.webLead}>
            O perfil pessoal e o ponto de partida de tudo. Guarde dados, documentos, circulos e
            lembretes no celular e no navegador.
          </Text>
        </View>
        <View style={styles.webPanel}>
          <Text style={styles.heading}>Criar conta</Text>
          <Text style={styles.lead}>Cadastro simples, so com e-mail e senha.</Text>
          {form}
        </View>
      </View>
    );
  }

  return (
    <KeyboardAvoidingView style={styles.mobile} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <BrandMark />
      <Text style={styles.heading}>Criar conta</Text>
      <Text style={styles.lead}>Comece pelo seu perfil pessoal.</Text>
      {form}
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  mobile: {
    flex: 1,
    backgroundColor: colors.bg,
    padding: 24,
    justifyContent: 'center',
    gap: 16,
  },
  webSplit: {
    flex: 1,
    flexDirection: 'row',
    backgroundColor: colors.bg,
    minHeight: '100%' as unknown as number,
  },
  webBrand: {
    flex: 1,
    backgroundColor: colors.primary,
    padding: 48,
    justifyContent: 'center',
    gap: 20,
  },
  webLead: {
    color: colors.white,
    fontSize: 20,
    lineHeight: 30,
    maxWidth: 420,
  },
  webPanel: {
    flex: 1,
    justifyContent: 'center',
    paddingHorizontal: 56,
    backgroundColor: colors.surface,
    gap: 10,
  },
  heading: {
    fontSize: 28,
    fontWeight: '800',
    color: colors.ink,
  },
  lead: {
    color: colors.muted,
    marginBottom: 8,
  },
  form: {
    gap: 14,
    maxWidth: 420,
    width: '100%',
  },
  hint: {
    color: colors.muted,
    textAlign: 'center',
  },
  link: {
    color: colors.primary,
    fontWeight: '700',
  },
});
