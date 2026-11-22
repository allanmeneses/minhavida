import { useCallback, useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type DayPanel, type ReminderItem, type ShortcutItem } from '@/lib/api';
import { playShortcutFeedback } from '@/lib/shortcutFeedback';
import { screenStyles } from '@/lib/screenStyles';

export default function HomeScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [panel, setPanel] = useState<DayPanel | null>(null);
  const [allShortcuts, setAllShortcuts] = useState<ShortcutItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [flash, setFlash] = useState<string | null>(null);
  const [label, setLabel] = useState('');
  const [actionKind, setActionKind] = useState<'done' | 'start' | 'note'>('done');
  const [reminders, setReminders] = useState<ReminderItem[]>([]);
  const [reminderId, setReminderId] = useState<string | null>(null);
  const [deletedShortcuts, setDeletedShortcuts] = useState<ShortcutItem[]>([]);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    try {
      setPanel(await api<DayPanel>('/api/home', { token }));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel abrir o dia.');
      return;
    }
    try {
      setAllShortcuts(await api<ShortcutItem[]>('/api/shortcuts', { token }));
      try {
        const withDeleted = await api<ShortcutItem[]>('/api/shortcuts?includeDeleted=true', { token });
        setDeletedShortcuts(withDeleted.filter((item) => item.isDeleted));
      } catch {
        setDeletedShortcuts([]);
      }
    } catch {
      // atalho nao entra no cache de leitura desta rodada
    }
    try {
      setReminders(await api<ReminderItem[]>('/api/reminders', { token }));
    } catch {
      // lembretes usam cache quando a rede some
    }
  }, [token]);

  useEffect(() => {
    void load();
    const timer = setInterval(() => {
      void load();
    }, 5000);
    const onFocus = () => {
      if (typeof document === 'undefined' || document.visibilityState === 'visible') {
        void load();
      }
    };
    if (typeof document !== 'undefined') {
      document.addEventListener('visibilitychange', onFocus);
    }
    return () => {
      clearInterval(timer);
      if (typeof document !== 'undefined') {
        document.removeEventListener('visibilitychange', onFocus);
      }
    };
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
      playShortcutFeedback();
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
      body: JSON.stringify({ label: label.trim(), actionKind, reminderId }),
    });
    setLabel('');
    setReminderId(null);
    await load();
  }

  async function deactivateShortcut(item: ShortcutItem) {
    if (!token) {
      return;
    }
    await api(`/api/shortcuts/${item.id}/deactivate`, { method: 'POST', token });
    await load();
  }

  async function activateShortcut(item: ShortcutItem) {
    if (!token) {
      return;
    }
    await api(`/api/shortcuts/${item.id}/activate`, { method: 'POST', token });
    await load();
  }

  async function deleteShortcut(item: ShortcutItem) {
    if (!token) {
      return;
    }
    await api(`/api/shortcuts/${item.id}`, { method: 'DELETE', token });
    await load();
  }

  const inactiveShortcuts = allShortcuts.filter((item) => !item.isActive);
  const circleReminders = reminders.filter((item) => item.circleId);

  async function moveShortcut(item: ShortcutItem, direction: -1 | 1) {
    if (!token || !panel) {
      return;
    }
    const active = [...panel.shortcuts];
    const index = active.findIndex((entry) => entry.id === item.id);
    const next = index + direction;
    if (index < 0 || next < 0 || next >= active.length) {
      return;
    }
    const swap = active[index];
    active[index] = active[next];
    active[next] = swap;
    await api('/api/shortcuts/order', {
      method: 'PUT',
      token,
      body: JSON.stringify({ ids: [...active.map((entry) => entry.id), ...inactiveShortcuts.map((entry) => entry.id)] }),
    });
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
          <View key={item.id} style={extra.shortcutWrap}>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={item.label}
              onPress={() => void runShortcut(item)}
              style={[extra.shortcut, { backgroundColor: item.color }]}>
              <Text style={extra.shortcutText}>{item.label}</Text>
            </Pressable>
            <Button label={`Subir ${item.label}`} variant="ghost" onPress={() => void moveShortcut(item, -1)} />
            <Button label={`Descer ${item.label}`} variant="ghost" onPress={() => void moveShortcut(item, 1)} />
            <Button label={`Desativar ${item.label}`} variant="ghost" onPress={() => void deactivateShortcut(item)} />
            <Button label={`Excluir ${item.label}`} variant="danger" onPress={() => void deleteShortcut(item)} />
          </View>
        ))}
      </View>

      {inactiveShortcuts.length > 0 ? (
        <View style={styles.card}>
          <Text style={styles.section}>Atalhos desativados</Text>
          <Text style={styles.lead}>Somem da tela principal ate reativar.</Text>
          {inactiveShortcuts.map((item) => (
            <View key={item.id}>
              <Text style={styles.item}>{item.label}</Text>
              <Button label={`Reativar ${item.label}`} variant="ghost" onPress={() => void activateShortcut(item)} />
              <Button label={`Excluir ${item.label}`} variant="danger" onPress={() => void deleteShortcut(item)} />
            </View>
          ))}
        </View>
      ) : null}

      <Button label="+" onPress={() => setLabel((current) => current)} />
      <View style={styles.card}>
        <TextField label="Nome do atalho" value={label} onChangeText={setLabel} placeholder="Ex.: Tomei o remedio" />
        <Button
          label={`Tipo: ${actionKind === 'start' ? 'iniciar' : actionKind === 'note' ? 'nota' : 'concluir'}`}
          variant="ghost"
          onPress={() =>
            setActionKind(actionKind === 'done' ? 'start' : actionKind === 'start' ? 'note' : 'done')
          }
        />
        <Button
          label={reminderId ? `Lembrete: ${circleReminders.find((item) => item.id === reminderId)?.title}` : 'Sem lembrete de circulo'}
          variant="ghost"
          onPress={() => {
            if (circleReminders.length === 0) {
              setReminderId(null);
              return;
            }
            const index = circleReminders.findIndex((item) => item.id === reminderId);
            const next = circleReminders[(index + 1) % (circleReminders.length + 1)];
            setReminderId(next?.id ?? null);
          }}
        />
        <Button label="Adicionar atalho" onPress={() => void addShortcut()} disabled={!label.trim()} />
      </View>
      {deletedShortcuts.length > 0 ? (
        <View style={styles.card}>
          <Text style={styles.section}>Atalhos excluidos</Text>
          {deletedShortcuts.map((item) => (
            <Button
              key={item.id}
              label={`Recuperar ${item.label}`}
              variant="ghost"
              onPress={() => {
                if (!token) {
                  return;
                }
                void api(`/api/shortcuts/${item.id}/restore`, { method: 'POST', token }).then(() => load());
              }}
            />
          ))}
        </View>
      ) : null}

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
      <Button label="Calendario" variant="ghost" onPress={() => router.push('/(app)/calendar' as Href)} />
      <Button label="Busca" variant="ghost" onPress={() => router.push('/(app)/search' as Href)} />
      <Button label="FAQ" variant="ghost" onPress={() => router.push('/(app)/faq' as Href)} />
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
  shortcutWrap: {
    gap: 8,
    minWidth: 140,
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
