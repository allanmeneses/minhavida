import { useCallback, useEffect, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type ReminderItem } from '@/lib/api';
import { screenStyles } from '@/lib/screenStyles';

export default function RemindersScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [items, setItems] = useState<ReminderItem[]>([]);
  const [title, setTitle] = useState('');
  const [kind, setKind] = useState<'general' | 'birthday'>('general');
  const [frequency, setFrequency] = useState('none');
  const [personName, setPersonName] = useState('');
  const [ideas, setIdeas] = useState('');
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setItems(await api<ReminderItem[]>('/api/reminders', { token }));
  }, [token]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao listar lembretes.'));
  }, [load]);

  async function create() {
    if (!token || !title.trim()) {
      return;
    }
    const startAt = new Date(Date.now() + 3600_000).toISOString();
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
        recurrence: { frequency, interval: frequency === 'weekly' ? 2 : 1 },
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
      <Text style={styles.lead}>Recorrencia no app. Aniversario pode ter lista de ideias montada por voce.</Text>
      <Feedback message={error} />
      <TextField label="Titulo" value={title} onChangeText={setTitle} />
      <Button
        label={kind === 'birthday' ? 'Aniversario' : 'Geral'}
        variant="ghost"
        onPress={() => setKind(kind === 'birthday' ? 'general' : 'birthday')}
      />
      <Button
        label={`Recorrencia: ${frequency}`}
        variant="ghost"
        onPress={() =>
          setFrequency(frequency === 'none' ? 'daily' : frequency === 'daily' ? 'weekly' : frequency === 'weekly' ? 'yearly' : 'none')
        }
      />
      {kind === 'birthday' ? (
        <>
          <TextField label="Quem" value={personName} onChangeText={setPersonName} />
          <TextField label="Ideias de presente" value={ideas} onChangeText={setIdeas} placeholder="Livro, planta" />
        </>
      ) : null}
      <Button label="Criar lembrete" onPress={() => void create()} disabled={!title.trim()} />
      {items.map((item) => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.item}>{item.title}</Text>
          <Text style={styles.lead}>{new Date(item.nextFireAt).toLocaleString('pt-BR')}</Text>
          {item.endingSoon ? <Text style={styles.lead}>Recorrencia acabando</Text> : null}
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

