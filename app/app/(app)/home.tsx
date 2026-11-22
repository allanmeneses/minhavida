import { useCallback, useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type DayPanel, type ShortcutItem } from '@/lib/api';
import { screenStyles } from '@/lib/screenStyles';

export default function HomeScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [panel, setPanel] = useState<DayPanel | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [flash, setFlash] = useState<string | null>(null);
  const [label, setLabel] = useState('');

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    try {
      setPanel(await api<DayPanel>('/api/home', { token }));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel abrir o dia.');
    }
  }, [token]);

  useEffect(() => {
    void load();
  }, [load]);

  async function runShortcut(item: ShortcutItem) {
    if (!token) {
      return;
    }
    setError(null);
    try {
      const result = await api<{ received: boolean; label: string }>(`/api/shortcuts/${item.id}/run`, {
        method: 'POST',
        token,
      });
      setFlash(result.label);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nao foi possivel registrar o atalho.');
    }
  }

  async function addShortcut() {
    if (!token || !label.trim()) {
      return;
    }
    await api('/api/shortcuts', {
      method: 'POST',
      token,
      body: JSON.stringify({ label: label.trim(), color: '#C45C26' }),
    });
    setLabel('');
    await load();
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>Painel do dia</Text>
      <Text style={styles.title}>O que pede atencao hoje</Text>
      {panel?.vacationUntil ? (
        <Text style={styles.lead}>Modo ferias ate {new Date(panel.vacationUntil).toLocaleDateString('pt-BR')}.</Text>
      ) : (
        <Text style={styles.lead}>
          {panel?.activeCircleCount ?? 0} circulo(s) ativo(s). Atalhos disparam na hora, sem confirmacao.
        </Text>
      )}
      <Feedback message={error} />
      <Feedback message={flash} tone="ok" />

      <View style={extra.row}>
        {panel?.shortcuts.map((item) => (
          <Pressable
            key={item.id}
            accessibilityRole="button"
            accessibilityLabel={item.label}
            onPress={() => void runShortcut(item)}
            style={[extra.shortcut, { backgroundColor: item.color }]}>
            <Text style={extra.shortcutText}>{item.label}</Text>
          </Pressable>
        ))}
      </View>

      <View style={styles.card}>
        <TextField label="Nome do atalho" value={label} onChangeText={setLabel} placeholder="Ex.: Tomei o remedio" />
        <Button label="Adicionar atalho" onPress={() => void addShortcut()} disabled={!label.trim()} />
      </View>

      <Section title="Atrasado" items={panel?.overdue ?? []} />
      <Section title="Vence hoje" items={panel?.dueToday ?? []} />
      <Section title="Em seguida" items={panel?.upcoming ?? []} />

      {panel?.plan.storageWarning ? (
        <Text style={[styles.lead, { color: colors.accent, fontWeight: '700' }]}>
          Voce ja usou {Math.round((panel.plan.storageUsedBytes / panel.plan.storageLimitBytes) * 100)}% do
          armazenamento. O plano pago sobe para 1 GB.
        </Text>
      ) : null}

      <Button label="Lembretes" variant="ghost" onPress={() => router.push('/(app)/reminders')} />
      <Button label="Ajustes" variant="ghost" onPress={() => router.push('/(app)/settings')} />
    </ScrollView>
  );
}

function Section({
  title,
  items,
}: {
  title: string;
  items: { id: string; title: string; at: string }[];
}) {
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  return (
    <View style={styles.card}>
      <Text style={styles.section}>{title}</Text>
      {items.length === 0 ? <Text style={styles.lead}>Nada por aqui.</Text> : null}
      {items.map((item) => (
        <Text key={item.id} style={styles.item}>
          {item.title}
        </Text>
      ))}
    </View>
  );
}

const extra = StyleSheet.create({
  row: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
  },
  shortcut: {
    paddingVertical: 16,
    paddingHorizontal: 18,
    borderRadius: 16,
    minWidth: 140,
  },
  shortcutText: {
    color: '#FFFFFF',
    fontWeight: '800',
  },
});
