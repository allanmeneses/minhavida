import { useCallback, useEffect, useState } from 'react';
import { ScrollView, Text } from 'react-native';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type ProfileRecap } from '@/lib/api';
import { screenStyles } from '@/lib/screenStyles';

export default function RecapScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [recap, setRecap] = useState<ProfileRecap | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setRecap(await api<ProfileRecap>('/api/recap/me', { token }));
  }, [token]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Não foi possível abrir o recap.'));
  }, [load]);

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Resumo de uso</Text>
      <Text style={styles.lead}>
        O recap só mostra o que você já usou. Outros fluxos aparecem quando você passa a usa-los.
      </Text>
      <Feedback message={error} />
      {recap && recap.flows.length === 0 ? (
        <Text style={styles.lead}>Ainda não há uso para resumir.</Text>
      ) : null}
      {recap?.flows.map((flow) => (
        <Text key={flow.key} style={styles.item}>
          {flow.label} · {flow.count}
        </Text>
      ))}
      <Button label="Atualizar recap" variant="ghost" onPress={() => void load()} />
    </ScrollView>
  );
}
