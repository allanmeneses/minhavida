import { useEffect, useState } from 'react';
import { ScrollView, Text } from 'react-native';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError } from '@/lib/api';
import { savePendingInvite } from '@/lib/pending-invite';
import { screenStyles } from '@/lib/screenStyles';

type Preview = { circleName: string; role: string; openLink: boolean; expiresAt: string };

export default function InviteLinkScreen() {
  const params = useLocalSearchParams<{ token: string | string[] }>();
  const inviteToken = Array.isArray(params.token) ? params.token[0] : params.token;
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [preview, setPreview] = useState<Preview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!inviteToken) {
      return;
    }
    savePendingInvite(inviteToken);
    void api<Preview>(`/api/circles/invites/${inviteToken}`)
      .then(setPreview)
      .catch((err) => setError(err instanceof ApiError ? err.message : 'Convite invalido.'));
  }, [inviteToken]);

  async function enter() {
    if (!inviteToken) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      if (!token) {
        savePendingInvite(inviteToken);
        router.push('/(auth)/register');
        return;
      }
      const joined = await api<{ id: string }>(`/api/circles/invites/${inviteToken}/accept`, {
        method: 'POST',
        token,
        body: JSON.stringify({}),
      });
      router.replace(`/(app)/circle/${joined.id}` as Href);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel entrar no circulo.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>Convite</Text>
      <Text style={styles.title}>{preview ? preview.circleName : 'Abrir o circulo'}</Text>
      <Text style={styles.lead}>
        Depois do cadastro voce entra direto neste circulo. O texto do convite ja serve para colar no WhatsApp.
      </Text>
      <Feedback message={error} />
      <Button
        label={token ? 'Entrar no circulo' : 'Criar conta e entrar'}
        onPress={() => void enter()}
        loading={busy}
      />
    </ScrollView>
  );
}
