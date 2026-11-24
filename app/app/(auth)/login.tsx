import { useEffect, useState } from 'react';
import { KeyboardAvoidingView, Platform, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { Link, router, type Href } from 'expo-router';
import { BrandMark } from '@/components/BrandMark';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { colors } from '@/constants/theme';
import { useAuth } from '@/context/AuthContext';
import { api, ApiError, TwoFactorRequiredError } from '@/lib/api';
import { t } from '@/lib/i18n';

export default function LoginScreen() {
  const { width } = useWindowDimensions();
  const narrow = width < 768;
  const { login, verifyTwoFactor } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [challenge, setChallenge] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [recovery, setRecovery] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [googleEnabled, setGoogleEnabled] = useState(false);

  useEffect(() => {
    void api<{ enabled: boolean }>('/api/auth/google')
      .then((status) => setGoogleEnabled(status.enabled))
      .catch(() => setGoogleEnabled(false));
  }, []);

  async function onSubmit() {
    setError(null);
    setBusy(true);
    try {
      if (challenge) {
        await verifyTwoFactor(challenge, code.trim() || undefined, recovery.trim() || undefined);
      } else {
        await login(email.trim(), password);
      }
      router.replace('/(app)/home');
    } catch (err) {
      if (err instanceof TwoFactorRequiredError) {
        setChallenge(err.twoFactorToken);
        setError('Informe o código do autenticador ou um código de recuperação.');
      } else {
        setError(err instanceof ApiError ? err.message : 'Não foi possível entrar.');
      }
    } finally {
      setBusy(false);
    }
  }

  const form = (
    <View style={styles.form}>
      <TextField
        label="E-mail"
        value={email}
        onChangeText={setEmail}
        placeholder="você@exemplo.com"
        keyboardType="email-address"
        autoComplete="email"
      />
      <TextField
        label="Senha"
        value={password}
        onChangeText={setPassword}
        placeholder="Sua senha"
        secureTextEntry
        autoComplete="password"
      />
      {challenge ? (
        <>
          <TextField
            label="Código do autenticador"
            value={code}
            onChangeText={setCode}
            placeholder="000000"
            keyboardType="number-pad"
          />
          <TextField
            label="Código de recuperação"
            value={recovery}
            onChangeText={setRecovery}
            placeholder="opcional"
          />
        </>
      ) : null}
      <Feedback message={error} />
      <Button
        label={challenge ? 'Confirmar código' : 'Entrar'}
        onPress={onSubmit}
        loading={busy}
        disabled={!email || !password || (Boolean(challenge) && !code.trim() && !recovery.trim())}
        disabledHint={
          challenge || email && password
            ? undefined
            : 'Preencha e-mail e senha para entrar.'
        }
      />
      {googleEnabled ? <Button label={t('google.login')} variant="ghost" onPress={() => undefined} /> : null}
      <Text style={styles.hint}>
        <Link href="/(auth)/forgot" style={styles.link}>
          Esqueci a senha
        </Link>
      </Text>
      <Text style={styles.hint}>
        Ainda não tem conta?{' '}
        <Link href="/(auth)/register" style={styles.link}>
          Criar conta
        </Link>
      </Text>
      <Text style={styles.hint}>
        <Link href={'/termos' as Href} style={styles.link}>
          Termos de uso
        </Link>
        {'  '}
        <Link href={'/privacidade' as Href} style={styles.link}>
          Política de privacidade
        </Link>
      </Text>
    </View>
  );

  if (Platform.OS === 'web') {
    return (
      <View style={[styles.webSplit, narrow && styles.webSplitNarrow]}>
        {narrow ? null : (
          <View style={styles.webBrand}>
            <BrandMark inverted />
            <Text style={styles.webLead}>
              A casa da sua vida pessoal. Perfil, documentos, círculos e lembretes no mesmo lugar,
              no celular e no navegador.
            </Text>
          </View>
        )}
        <View style={[styles.webPanel, narrow && styles.webPanelNarrow]}>
          {narrow ? <BrandMark /> : null}
          <Text style={styles.heading}>Entrar</Text>
          <Text style={styles.lead}>Use o e-mail e a senha da sua conta.</Text>
          {form}
        </View>
      </View>
    );
  }

  return (
    <KeyboardAvoidingView style={styles.mobile} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <BrandMark />
      <Text style={styles.heading}>Entrar</Text>
      <Text style={styles.lead}>Abra o Círculos pelo seu perfil pessoal.</Text>
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
  webSplitNarrow: {
    flexDirection: 'column',
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
  webPanelNarrow: {
    paddingHorizontal: 20,
    paddingVertical: 24,
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
