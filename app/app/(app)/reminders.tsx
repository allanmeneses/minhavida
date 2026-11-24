import { useCallback, useEffect, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { DateField } from '@/components/ui/DateField';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type CircleSummary, type ReminderItem } from '@/lib/api';
import { FREQUENCY_LABEL, displayLabel } from '@/lib/labels';
import { formatPersonTime } from '@/lib/formatPersonTime';
import { examplePlaceholder } from '@/lib/placeholders';
import { screenStyles } from '@/lib/screenStyles';

const FREQUENCIES = ['none', 'daily', 'weekly', 'monthly', 'yearly'] as const;

export default function RemindersScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [items, setItems] = useState<ReminderItem[]>([]);
  const [circles, setCircles] = useState<CircleSummary[]>([]);
  const [title, setTitle] = useState('');
  const [kind, setKind] = useState<'general' | 'birthday'>('general');
  const [frequency, setFrequency] = useState<(typeof FREQUENCIES)[number]>('none');
  const [interval, setInterval] = useState('1');
  const [daysOfWeek, setDaysOfWeek] = useState('');
  const [until, setUntil] = useState('');
  const [maxOccurrences, setMaxOccurrences] = useState('');
  const [monthlyMode, setMonthlyMode] = useState<'date' | 'position'>('date');
  const [weekOfMonth, setWeekOfMonth] = useState('3');
  const [personName, setPersonName] = useState('');
  const [ideas, setIdeas] = useState('');
  const [circleId, setCircleId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setItems(await api<ReminderItem[]>('/api/reminders', { token }));
    setCircles(await api<CircleSummary[]>('/api/circles', { token }));
  }, [token]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao listar lembretes.'));
  }, [load]);

  async function create() {
    if (!token || !title.trim()) {
      return;
    }
    const startAt = new Date(Date.now() + 3600_000).toISOString();
    const parsedInterval = Math.max(1, Number(interval) || 1);
    const days = daysOfWeek
      .split(',')
      .map((item) => Number(item.trim()))
      .filter((item) => Number.isInteger(item) && item >= 0 && item <= 6);
    await api('/api/reminders', {
      method: 'POST',
      token,
      body: JSON.stringify({
        title: title.trim(),
        kind,
        startAt,
        personName: personName.trim() || null,
        advanceDays: kind === 'birthday' ? 3 : 0,
        giftIdeas: ideas.split(',').map((item) => item.trim()).filter(Boolean),
        circleId,
        recurrence: {
          frequency,
          interval: parsedInterval,
          daysOfWeek: frequency === 'weekly' ? days : [],
          monthlyMode: frequency === 'monthly' ? monthlyMode : 'date',
          weekOfMonth: frequency === 'monthly' && monthlyMode === 'position' ? Number(weekOfMonth) || 3 : null,
          untilUtc: until.trim() ? new Date(until.trim()).toISOString() : null,
          maxOccurrences: maxOccurrences.trim() ? Number(maxOccurrences) : null,
        },
      }),
    });
    setTitle('');
    setPersonName('');
    setIdeas('');
    await load();
  }

  async function pause(id: string, resume: boolean) {
    if (!token) {
      return;
    }
    await api(`/api/reminders/${id}/${resume ? 'resume' : 'pause'}`, { method: 'POST', token });
    await load();
  }

  async function skip(id: string) {
    if (!token) {
      return;
    }
    await api(`/api/reminders/${id}/skip`, { method: 'POST', token });
    await load();
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Lembretes</Text>
      <Text style={styles.lead}>
        Recorrência no modelo de agenda: diária, semanal, mensal, anual, intervalo e término.
      </Text>
      <Button label="Ver calendário" variant="ghost" onPress={() => router.push('/(app)/calendar' as Href)} />
      <Button label="Modelos de lembrete" variant="ghost" onPress={() => router.push('/(app)/templates' as Href)} />
      <Feedback message={error} />
      <TextField label="Título" value={title} onChangeText={setTitle} />
      <Button
        label={kind === 'birthday' ? 'Aniversário' : 'Geral'}
        variant="ghost"
        onPress={() => setKind(kind === 'birthday' ? 'general' : 'birthday')}
      />
      <Button
        label={`Recorrência: ${displayLabel(frequency, FREQUENCY_LABEL)}`}
        variant="ghost"
        onPress={() => {
          const index = FREQUENCIES.indexOf(frequency);
          setFrequency(FREQUENCIES[(index + 1) % FREQUENCIES.length]);
        }}
      />
      {frequency !== 'none' ? (
        <>
          <TextField label="A cada" value={interval} onChangeText={setInterval} placeholder="1" keyboardType="number-pad" />
          {frequency === 'weekly' ? (
            <TextField
              label="Dias da semana, domingo primeiro"
              value={daysOfWeek}
              onChangeText={setDaysOfWeek}
              placeholder="1,3"
            />
          ) : null}
          {frequency === 'monthly' ? (
            <>
              <Button
                label={monthlyMode === 'date' ? 'Mensal por dia do mês' : 'Mensal por posição'}
                variant="ghost"
                onPress={() => setMonthlyMode(monthlyMode === 'date' ? 'position' : 'date')}
              />
              {monthlyMode === 'position' ? (
                <TextField
                  label="Semana do mês"
                  value={weekOfMonth}
                  onChangeText={setWeekOfMonth}
                  placeholder="3"
                  keyboardType="number-pad"
                />
              ) : null}
            </>
          ) : null}
          <DateField label="Termina em" value={until} onChangeText={setUntil} />
          <TextField label="Máximo de ocorrências" value={maxOccurrences} onChangeText={setMaxOccurrences} keyboardType="number-pad" />
        </>
      ) : null}
      {kind === 'birthday' ? (
        <>
          <TextField label="Quem" value={personName} onChangeText={setPersonName} />
          <TextField label="Ideias de presente" value={ideas} onChangeText={setIdeas} placeholder={examplePlaceholder('Livro, planta')} />
        </>
      ) : null}
      <Text style={styles.lead}>Círculo cíclico (opcional). O atalho marca que foi feito.</Text>
      <Button
        label={circleId ? circles.find((item) => item.id === circleId)?.name ?? 'Círculo' : 'Sem círculo'}
        variant="ghost"
        onPress={() => {
          if (circles.length === 0) {
            setCircleId(null);
            return;
          }
          const index = circles.findIndex((item) => item.id === circleId);
          const next = circles[(index + 1) % (circles.length + 1)];
          setCircleId(next?.id ?? null);
        }}
      />
      <Button
        label="Criar lembrete"
        onPress={() => void create()}
        disabled={!title.trim()}
        disabledHint="Informe o título do lembrete para criar."
      />
      {items.map((item) => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.item}>{item.title}</Text>
          <Text style={styles.lead}>
            {displayLabel(item.recurrence?.frequency ?? 'none', FREQUENCY_LABEL)} · {formatPersonTime(item.nextFireAt)}
          </Text>
          {item.endingSoon ? <Text style={styles.lead}>Recorrência acabando</Text> : null}
          {item.giftIdeas?.length ? <Text style={styles.lead}>{item.giftIdeas.join(', ')}</Text> : null}
          <Button
            label={item.isPaused ? 'Retomar' : 'Pausar'}
            variant="ghost"
            onPress={() => void pause(item.id, item.isPaused)}
          />
          <Button label={`Pular ${item.title}`} variant="ghost" onPress={() => void skip(item.id)} />
        </View>
      ))}
    </ScrollView>
  );
}
