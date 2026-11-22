import { useEffect, useState } from 'react';
import { ScrollView, Text } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type PlanInfo } from '@/lib/api';
import { screenStyles } from '@/lib/screenStyles';

export default function SettingsScreen() {
  const { token, logout } = useAuth();
  const router = useRouter();
  const { preference, setPreference, resolved, colors } = useTheme();
  const styles = screenStyles(colors);
  const [plan, setPlan] = useState<PlanInfo | null>(null);
  const [password, setPassword] = useState('');
  const [vacationDays, setVacationDays] = useState('7');
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }
    void api<PlanInfo>('/api/plan', { token }).then(setPlan).catch((err) => {
      setError(err instanceof ApiError ? err.message : 'Falha ao ler o plano.');
    });
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

  const used = plan ? Math.round((plan.storageUsedBytes / plan.storageLimitBytes) * 100) : 0;

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Ajustes</Text>
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />

      <Text style={styles.section}>Aparencia</Text>
      <Text style={styles.lead}>Agora: {resolved}. Preferencia: {preference}.</Text>
      <Button label="Seguir o aparelho" variant="ghost" onPress={() => void setPreference('system')} />
      <Button label="Claro" variant="ghost" onPress={() => void setPreference('light')} />
      <Button label="Escuro" variant="ghost" onPress={() => void setPreference('dark')} />

      <Text style={styles.section}>Plano</Text>
      <Text style={styles.lead}>
        {plan?.plan === 'paid' ? 'Pago' : 'Gratuito'}: {plan?.circleLimit ?? 'ilimitado'} circulos,{' '}
        {plan ? Math.round(plan.storageLimitBytes / (1024 * 1024)) : 0} MB. Uso {used}%.
      </Text>
      <Text style={styles.lead}>{plan?.checkout}</Text>

      <Text style={styles.section}>Ferias</Text>
      <Text style={styles.lead}>Escolha por quantos dias as notificacoes ficam suspensas.</Text>
      <TextField label="Dias de ferias" value={vacationDays} onChangeText={setVacationDays} keyboardType="number-pad" />
      <Button label="Silenciar ferias" onPress={() => void vacation()} />

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

