import { useCallback, useEffect, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type TemplateItem } from '@/lib/api';
import { screenStyles } from '@/lib/screenStyles';

export default function TemplatesScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [items, setItems] = useState<TemplateItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setItems(await api<TemplateItem[]>('/api/templates', { token }));
  }, [token]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao abrir os modelos.'));
  }, [load]);

  async function copy(item: TemplateItem) {
    if (!token) {
      return;
    }
    setOk(null);
    const result = await api<{ kind: string; id: string }>(`/api/templates/${item.id}/copy`, { method: 'POST', token });
    setOk(
      result.kind === 'list'
        ? `Lista copiada: ${item.name}. Voce pode editar os itens.`
        : `Lembrete copiado: ${item.name}. Voce pode editar a recorrencia.`,
    );
    await load();
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Modelos</Text>
      <Text style={styles.lead}>Listas e lembretes prontos, gratuitos, em portugues. Quem copia edita. O criador nao aparece.</Text>
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />
      {items.map((item) => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.item}>{item.name}</Text>
          <Text style={styles.lead}>
            {item.kind === 'list' ? 'Lista' : 'Lembrete'} · usados {item.useCount} · posto {item.rank}
          </Text>
          <Text style={styles.lead}>{item.description}</Text>
          <Button label={`Usar ${item.name}`} onPress={() => void copy(item)} />
        </View>
      ))}
    </ScrollView>
  );
}
