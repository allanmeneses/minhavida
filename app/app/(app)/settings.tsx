import { useEffect, useState } from 'react';
import { Image, Linking, ScrollView, Text, View } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { useTour } from '@/context/TourContext';
import { api, ApiError, downloadNamedBlob, type PlanInfo, type TwoFactorSetup } from '@/lib/api';
import { API_URL } from '@/lib/config';
import { screenStyles } from '@/lib/screenStyles';

export default function SettingsScreen() {
  const { token, logout } = useAuth();
  const router = useRouter();
  const tour = useTour();
  const { preference, setPreference, resolved, colors, highContrast, setHighContrast } = useTheme();
  const styles = screenStyles(colors);
  const [plan, setPlan] = useState<PlanInfo | null>(null);
  const [password, setPassword] = useState('');
  const [vacationDays, setVacationDays] = useState('7');
  const [twoFactor, setTwoFactor] = useState(false);
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null);
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }
    void api<PlanInfo>('/api/plan', { token }).then(setPlan).catch((err) => {
      setError(err instanceof ApiError ? err.message : 'Falha ao ler o plano.');
    });
    void api<{ enabled: boolean }>('/api/auth/2fa', { token })
      .then((status) => setTwoFactor(status.enabled))
      .catch(() => undefined);
  }, [token]);

  async function vacation() {
    if (!token) {
      return;
    }
    const days = Math.max(1, Number(vacationDays.replace(',', '.')) || 7);
    const until = new Date(Date.now() + days * 24 * 3600_000).toISOString();
    await api('/api/reminders/vacation', { method: 'PUT', token, body: JSON.stringify({ until }) });
    setOk(`Modo ferias ligado por ${days} dia(s). Lembretes ficam guardados.`);
  }

  async function requestDeletion() {
    if (!token || !password) {
      return;
    }
    setError(null);
    try {
      await api('/api/account/deletion-requests', {
        method: 'POST',
        token,
        body: JSON.stringify({ password }),
      });
      setOk('Enviamos um e-mail para confirmar a exclusao.');
      setPassword('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel pedir a exclusao.');
    }
  }

  async function startTwoFactor() {
    if (!token) {
      return;
    }
    setError(null);
    try {
      setSetup(await api<TwoFactorSetup>('/api/auth/2fa/setup', { method: 'POST', token }));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel iniciar o segundo fator.');
    }
  }

  async function enableTwoFactor() {
    if (!token || !code.trim()) {
      return;
    }
    try {
      await api('/api/auth/2fa/enable', { method: 'POST', token, body: JSON.stringify({ code: code.trim() }) });
      setTwoFactor(true);
      setOk('Segundo fator ativo. Guarde os codigos de recuperacao.');
      setCode('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Codigo do autenticador invalido.');
    }
  }

  async function disableTwoFactor() {
    if (!token || !password || !code.trim()) {
      return;
    }
    try {
      await api('/api/auth/2fa/disable', {
        method: 'POST',
        token,
        body: JSON.stringify({ password, code: code.trim() }),
      });
      setTwoFactor(false);
      setSetup(null);
      setOk('Segundo fator desativado.');
      setCode('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel desativar o segundo fator.');
    }
  }

  async function exportProfile() {
    if (!token) {
      return;
    }
    await downloadNamedBlob('/api/export/me', 'circulos-perfil.zip', token);
    setOk('Backup do perfil baixado.');
  }

  const used = plan ? Math.round((plan.storageUsedBytes / plan.storageLimitBytes) * 100) : 0;
  const bar = Math.max(0, Math.min(100, used));

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Ajustes</Text>
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />

      <Text style={styles.section}>Aparencia</Text>
      <Text style={styles.lead}>Agora: {resolved}. Preferencia: {preference}.</Text>
      <Text style={styles.lead}>Contraste: {highContrast ? 'alto' : 'normal'}.</Text>
      <Button label="Seguir o aparelho" variant="ghost" onPress={() => void setPreference('system')} />
      <Button label="Claro" variant="ghost" onPress={() => void setPreference('light')} />
      <Button label="Escuro" variant="ghost" onPress={() => void setPreference('dark')} />
      <Button
        label={highContrast ? 'Contraste normal' : 'Contraste alto'}
        variant="ghost"
        onPress={() => void setHighContrast(!highContrast)}
      />

      <Text style={styles.section}>Plano</Text>
      <Text style={styles.lead}>
        {plan?.plan === 'paid' ? 'Pago' : 'Gratuito'}: {plan?.circleLimit ?? 'ilimitado'} circulos,{' '}
        {plan ? Math.round(plan.storageLimitBytes / (1024 * 1024)) : 0} MB. Uso {used}%.
      </Text>
      <Text style={styles.lead}>{plan?.checkout}</Text>
      <Text style={styles.section}>Uso de armazenamento</Text>
      <View
        accessibilityLabel={`Uso de armazenamento ${used} por cento`}
        style={{ height: 12, borderRadius: 6, backgroundColor: colors.line, overflow: 'hidden' }}>
        <View style={{ width: `${bar}%`, height: 12, backgroundColor: colors.primary }} />
      </View>
      <Text style={styles.lead}>
        {plan ? Math.round(plan.storageUsedBytes / 1024) : 0} KB de{' '}
        {plan ? Math.round(plan.storageLimitBytes / (1024 * 1024)) : 0} MB. Sem separar por tipo.
      </Text>

      <Text style={styles.section}>Backup</Text>
      <Button label="Exportar meu perfil" onPress={() => void exportProfile()} />

      <Text style={styles.section}>Segundo fator</Text>
      <Text style={styles.lead}>
        {twoFactor ? 'Ativo. O login pede o codigo do autenticador.' : 'Opcional. Ative com QR no autenticador.'}
      </Text>
      {!twoFactor && !setup ? <Button label="Ativar segundo fator" onPress={() => void startTwoFactor()} /> : null}
      {setup && !twoFactor ? (
        <>
          <Image
            accessibilityLabel="QR do autenticador"
            source={{ uri: `data:image/png;base64,${setup.qrPngBase64}` }}
            style={{ width: 180, height: 180 }}
          />
          <Text style={styles.lead}>Segredo: {setup.secret}</Text>
          <Text style={styles.lead}>Codigos de recuperacao: {setup.recoveryCodes.join(', ')}</Text>
          <TextField label="Codigo do autenticador" value={code} onChangeText={setCode} keyboardType="number-pad" />
          <Button label="Confirmar segundo fator" onPress={() => void enableTwoFactor()} disabled={!code.trim()} />
        </>
      ) : null}
      {setup && twoFactor ? (
        <Text style={styles.lead}>Codigos de recuperacao: {setup.recoveryCodes.join(', ')}</Text>
      ) : null}
      {twoFactor ? (
        <>
          <TextField label="Codigo do autenticador" value={code} onChangeText={setCode} keyboardType="number-pad" />
          <TextField label="Senha para desativar" value={password} onChangeText={setPassword} secureTextEntry />
          <Button
            label="Desativar segundo fator"
            variant="danger"
            onPress={() => void disableTwoFactor()}
            disabled={!password || !code.trim()}
          />
        </>
      ) : null}

      <Text style={styles.section}>Ferias</Text>
      <Text style={styles.lead}>Escolha por quantos dias as notificacoes ficam suspensas.</Text>
      <TextField label="Dias de ferias" value={vacationDays} onChangeText={setVacationDays} keyboardType="number-pad" />
      <Button label="Silenciar ferias" onPress={() => void vacation()} />

      <Text style={styles.section}>Ajuda</Text>
      <Button label="FAQ" variant="ghost" onPress={() => router.push('/(app)/faq' as Href)} />
      <Button label="Status do sistema" variant="ghost" onPress={() => void Linking.openURL(`${API_URL}/status`)} />
      <Button label="Ver tour de novo" variant="ghost" onPress={() => void tour.replay()} />

      <Text style={styles.section}>Documentos legais</Text>
      <Button label="Termos de uso" variant="ghost" onPress={() => router.push('/termos' as Href)} />
      <Button label="Politica de privacidade" variant="ghost" onPress={() => router.push('/privacidade' as Href)} />

      <Text style={styles.section}>Excluir conta</Text>
      <Text style={styles.lead}>Pede senha e confirmacao por e-mail. O rastro do circulo fica anonimo.</Text>
      <TextField label="Senha" value={password} onChangeText={setPassword} secureTextEntry />
      <Button label="Pedir exclusao" variant="danger" onPress={() => void requestDeletion()} disabled={!password} />
      <Button label="Sair" variant="ghost" onPress={() => void logout()} />
    </ScrollView>
  );
}
