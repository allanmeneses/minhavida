import { useCallback, useEffect, useRef, useState } from 'react';
import { Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { CaptureBar } from '@/components/CaptureBar';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type CircleSummary, type DayPanel, type ReminderItem, type ShortcutItem } from '@/lib/api';
import { t } from '@/lib/i18n';
import { countNoun } from '@/lib/plural';
import { type HomeRecordType } from '@/lib/records';
import { playShortcutFeedback } from '@/lib/shortcutFeedback';
import { SHORTCUT_FLASH_MS } from '@/lib/shortcutFlash';
import { isTypingTarget, shortcutIndexFromKey } from '@/lib/shortcutHotkey';
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
  const [flashAt, setFlashAt] = useState(0);
  const [label, setLabel] = useState('');
  const [actionKind, setActionKind] = useState<'done' | 'start' | 'note'>('done');
  const [reminders, setReminders] = useState<ReminderItem[]>([]);
  const [reminderId, setReminderId] = useState<string | null>(null);
  const [deletedShortcuts, setDeletedShortcuts] = useState<ShortcutItem[]>([]);
  const [circles, setCircles] = useState<CircleSummary[]>([]);
  const [emailsSent, setEmailsSent] = useState<string[]>([]);
  const [showRest, setShowRest] = useState(false);
  const [dragId, setDragId] = useState<string | null>(null);
  const [recordTypes, setRecordTypes] = useState<HomeRecordType[]>([]);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    try {
      const next = await api<DayPanel>('/api/home', { token });
      setPanel(next);
      setRecordTypes(next.recordTypes ?? []);
      if ((next.reminderEmailsSent ?? []).length > 0) {
        setEmailsSent(next.reminderEmailsSent);
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível abrir o dia.');
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
      // atalho não entra no cache de leitura desta rodada
    }
    try {
      setReminders(await api<ReminderItem[]>('/api/reminders', { token }));
    } catch {
      // lembretes usam cache quando a rede some
    }
    try {
      setCircles(await api<CircleSummary[]>('/api/circles', { token }));
    } catch {
      setCircles([]);
    }
    try {
      const types = await api<HomeRecordType[]>('/api/record-types', { token });
      setRecordTypes(types.map((item) => ({
        id: item.id,
        name: item.name,
        lastUsedAt: item.lastUsedAt ?? null,
        recordCount: item.recordCount ?? 0,
      })));
    } catch {
      // tipos entram depois da primeira leitura
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

  useEffect(() => {
    if (!flash) {
      return;
    }
    const timer = setTimeout(() => {
      setFlash(null);
    }, SHORTCUT_FLASH_MS);
    return () => clearTimeout(timer);
  }, [flash, flashAt]);

  const runShortcut = useCallback(async (item: ShortcutItem) => {
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
      setFlashAt(Date.now());
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível registrar o atalho.');
    }
  }, [token, load]);

  const shortcutsRef = useRef<ShortcutItem[]>([]);
  shortcutsRef.current = panel?.shortcuts ?? [];
  const runShortcutRef = useRef(runShortcut);
  runShortcutRef.current = runShortcut;

  useEffect(() => {
    if (Platform.OS !== 'web' || typeof window === 'undefined') {
      return;
    }
    const onKey = (event: { key: string; target: EventTarget | null; preventDefault: () => void }) => {
      const index = shortcutIndexFromKey(event.key, isTypingTarget(event.target));
      if (index == null) {
        return;
      }
      const item = shortcutsRef.current[index];
      if (!item) {
        return;
      }
      event.preventDefault();
      void runShortcutRef.current(item);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

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

  async function decideExample(id: string, confirm: boolean) {
    if (!token) {
      return;
    }
    await api(`/api/onboarding/${id}`, {
      method: 'POST',
      token,
      body: JSON.stringify({ confirm }),
    });
    await load();
  }

  async function ackMonthly() {
    if (!token) {
      return;
    }
    await api('/api/recap/monthly/ack', { method: 'POST', token });
    await load();
  }

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

  async function dropShortcut(targetId: string) {
    if (!token || !panel || !dragId || dragId === targetId) {
      return;
    }
    const active = [...panel.shortcuts];
    const from = active.findIndex((entry) => entry.id === dragId);
    const to = active.findIndex((entry) => entry.id === targetId);
    if (from < 0 || to < 0) {
      return;
    }
    const [moved] = active.splice(from, 1);
    active.splice(to, 0, moved);
    await api('/api/shortcuts/order', {
      method: 'PUT',
      token,
      body: JSON.stringify({ ids: [...active.map((entry) => entry.id), ...inactiveShortcuts.map((entry) => entry.id)] }),
    });
    setDragId(null);
    await load();
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>{t('home.kicker')}</Text>
      <Text accessibilityRole="header" style={styles.title}>
        {t('home.title')}
      </Text>
      {panel?.vacationUntil ? (
        <Text style={styles.lead}>Modo férias até {new Date(panel.vacationUntil).toLocaleDateString('pt-BR')}.</Text>
      ) : (
        <Text style={styles.lead}>
          {countNoun(panel?.activeCircleCount ?? 0, 'círculo ativo', 'círculos ativos')}. Atalhos disparam na hora, sem
          confirmação.
        </Text>
      )}
      <Feedback message={error} />
      <Feedback message={flash} tone="ok" testID="shortcut-flash" />
      {emailsSent.map((title) => (
        <Feedback key={title} message={`Enviamos o lembrete ${title} por e-mail`} tone="ok" />
      ))}
      {(panel?.shortcutNotices ?? []).map((notice) => (
        <Feedback key={notice.id} message={notice.detail} tone="ok" />
      ))}
      {(panel?.ownershipOffers ?? []).map((offer) => (
        <View key={offer.circleId} style={styles.card}>
          <Text style={styles.lead}>O círculo {offer.circleName} oferece a posse para você.</Text>
          <Button
            label={`Abrir ${offer.circleName}`}
            variant="ghost"
            onPress={() => router.push(`/(app)/circle/${offer.circleId}` as Href)}
          />
        </View>
      ))}

      {token ? (
        <View style={styles.card}>
          <Text style={styles.section}>Captura livre</Text>
          <CaptureBar
            token={token}
            types={recordTypes}
            onOpenType={(typeId) => router.push(`/(app)/type/${typeId}` as Href)}
            onNewType={() => router.push('/(app)/types' as Href)}
            onSaved={() => load()}
          />
        </View>
      ) : null}

      {(panel?.onboarding ?? []).length > 0 ? (
        <View style={styles.card} testID="onboarding-examples">
          <Text style={styles.section}>Comece por aqui</Text>
          <Text style={styles.lead}>Círculos Eu e Família já estão prontos. Confirme ou descarte os exemplos.</Text>
          {(panel?.onboarding ?? []).map((item) => (
            <View key={item.id} style={extra.onboarding}>
              <Text style={styles.item}>{item.title}</Text>
              <Text style={styles.lead}>
                {item.circleName} · {item.body}
              </Text>
              <Button label={t('onboarding.confirm')} onPress={() => void decideExample(item.id, true)} />
              <Button label={t('onboarding.discard')} variant="ghost" onPress={() => void decideExample(item.id, false)} />
            </View>
          ))}
        </View>
      ) : null}

      <Section title="Atrasado" items={panel?.overdue ?? []} />
      <Section title="Vence hoje" items={panel?.dueToday ?? []} />

      {(panel?.onThisDay ?? []).length > 0 ? (
        <View style={styles.card} testID="on-this-day">
          <Text style={styles.section}>{t('home.onThisDay')}</Text>
          {(panel?.onThisDay ?? []).map((item) => (
            <Text key={item.id} style={styles.item}>
              {item.year}: {item.title}
            </Text>
          ))}
        </View>
      ) : (
        <View style={styles.card} testID="on-this-day">
          <Text style={styles.section}>{t('home.onThisDay')}</Text>
          <Text style={styles.lead}>Quando houver uma memória desta data em outro ano, ela aparece aqui.</Text>
        </View>
      )}

      {panel?.routineStreakDays ? (
        <Text style={styles.lead} testID="routine-streak">
          {t('home.streak')}: {panel.routineStreakDays}
        </Text>
      ) : null}

      {panel?.monthlySurprise ? (
        <View style={styles.card} testID="monthly-surprise">
          <Text style={styles.section}>Retrospectiva de {panel.monthlySurprise.month}/{panel.monthlySurprise.year}</Text>
          <Text style={styles.lead}>
            {countNoun(panel.monthlySurprise.memoryCount, 'memória', 'memórias')} e{' '}
            {countNoun(panel.monthlySurprise.entryCount, 'entrada', 'entradas')} no mês passado.
          </Text>
          {panel.monthlySurprise.titles.map((title) => (
            <Text key={title} style={styles.item}>
              {title}
            </Text>
          ))}
          <Button label="Ok, vi a retrospectiva" variant="ghost" onPress={() => void ackMonthly()} />
        </View>
      ) : null}

      <View style={extra.row}>
        {panel?.shortcuts.map((item) => (
          <View key={item.id} style={extra.shortcutWrap}>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={item.label}
              {...(Platform.OS === 'web'
                ? ({
                    draggable: true,
                    onDragStart: () => setDragId(item.id),
                    onDragOver: (event: { preventDefault: () => void }) => event.preventDefault(),
                    onDrop: () => void dropShortcut(item.id),
                  } as object)
                : {})}
              onPress={() => void runShortcut(item)}
              style={[extra.shortcut, { backgroundColor: item.color }]}>
              <Text style={extra.shortcutText}>{item.label}</Text>
            </Pressable>
            {item.sharedByName ? <Text style={extra.shortcutFrom}>De {item.sharedByName}</Text> : null}
            {item.sharedByUserId ? null : (
              <>
                <Button label={`Subir ${item.label}`} variant="ghost" onPress={() => void moveShortcut(item, -1)} />
                <Button label={`Descer ${item.label}`} variant="ghost" onPress={() => void moveShortcut(item, 1)} />
                <Button label={`Desativar ${item.label}`} variant="ghost" onPress={() => void deactivateShortcut(item)} />
                <Button
                  label={`Renomear ${item.label}`}
                  variant="ghost"
                  onPress={() => {
                    if (!token) {
                      return;
                    }
                    void api(`/api/shortcuts/${item.id}`, {
                      method: 'PUT',
                      token,
                      body: JSON.stringify({
                        label: `${item.label} (editado)`,
                        color: item.color,
                        actionKind: item.actionKind,
                        reminderId: item.reminderId,
                        sortOrder: item.sortOrder,
                      }),
                    }).then(() => load());
                  }}
                />
                {circles.map((circle) => (
                  <Button
                    key={`${item.id}-${circle.id}`}
                    label={`Compartilhar ${item.label} em ${circle.name ?? 'círculo'}`}
                    variant="ghost"
                    onPress={() => {
                      if (!token) {
                        return;
                      }
                      void api(`/api/shortcuts/${item.id}/share`, {
                        method: 'POST',
                        token,
                        body: JSON.stringify({ circleId: circle.id }),
                      }).then(() => load());
                    }}
                  />
                ))}
                <Button label={`Excluir ${item.label}`} variant="danger" onPress={() => void deleteShortcut(item)} />
              </>
            )}
          </View>
        ))}
      </View>

      {inactiveShortcuts.length > 0 ? (
        <View style={styles.card}>
          <Text style={styles.section}>Atalhos desativados</Text>
          <Text style={styles.lead}>Somem da tela principal até reativar.</Text>
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
        <TextField label="Nome do atalho" value={label} onChangeText={setLabel} placeholder="Ex.: Tomei o remédio" />
        <Button
          label={`Tipo: ${actionKind === 'start' ? 'iniciar' : actionKind === 'note' ? 'nota' : 'concluir'}`}
          variant="ghost"
          onPress={() =>
            setActionKind(actionKind === 'done' ? 'start' : actionKind === 'start' ? 'note' : 'done')
          }
        />
        <Button
          label={reminderId ? `Lembrete: ${circleReminders.find((item) => item.id === reminderId)?.title}` : 'Sem lembrete de círculo'}
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
        <Button
          label="Adicionar atalho"
          onPress={() => void addShortcut()}
          disabled={!label.trim()}
          disabledHint="Informe o nome do atalho para adicionar."
        />
      </View>
      {deletedShortcuts.length > 0 ? (
        <View style={styles.card}>
          <Text style={styles.section}>Atalhos excluídos</Text>
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
      <Button label="Lembretes" variant="ghost" onPress={() => router.push('/(app)/reminders')} />
      <Button label="Calendário" variant="ghost" onPress={() => router.push('/(app)/calendar' as Href)} />
      <Button label="Busca" variant="ghost" onPress={() => router.push('/(app)/search' as Href)} />
      <Button label="FAQ" variant="ghost" onPress={() => router.push('/(app)/faq' as Href)} />
      <Button label="Ajustes" variant="ghost" onPress={() => router.push('/(app)/settings')} />
      <Button
        label={showRest ? t('home.hideRest') : t('home.rest')}
        variant="ghost"
        onPress={() => setShowRest((value) => !value)}
      />
      {showRest ? (
        <>
          <Section title="Em seguida" items={panel?.upcoming ?? []} />
          {panel?.plan.storageWarning ? (
            <Text style={[styles.lead, { color: colors.accent, fontWeight: '700' }]}>
              Você já usou {Math.round((panel.plan.storageUsedBytes / panel.plan.storageLimitBytes) * 100)}% do
              armazenamento. O plano pago sobe para 1 GB.
            </Text>
          ) : null}
          <Button
            label={t('home.album')}
            variant="ghost"
            onPress={() => router.push('/(app)/memories' as Href)}
          />
        </>
      ) : null}
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
  shortcutFrom: {
    fontSize: 13,
    fontWeight: '600',
  },
  onboarding: {
    gap: 8,
    marginBottom: 12,
  },
});
