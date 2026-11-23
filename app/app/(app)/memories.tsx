import { useCallback, useEffect, useRef, useState } from 'react';
import { ScrollView, Text } from 'react-native';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type MemoryItem } from '@/lib/api';
import { keepTypedDraft } from '@/lib/keepTypedDraft';
import { screenStyles } from '@/lib/screenStyles';

export default function MemoriesScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [items, setItems] = useState<MemoryItem[]>([]);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [occurredOn, setOccurredOn] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [skip, setSkip] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const pageSize = 20;

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    const next = await api<MemoryItem[]>(`/api/memories?skip=${skip}&take=${pageSize}`, { token });
    setHasMore(next.length === pageSize);
    setItems((current) => (skip === 0 ? next : [...current, ...next]));
  }, [token, skip]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao abrir as memorias.'));
  }, [load]);

  async function create() {
    if (!token || !title.trim()) {
      return;
    }
    await api('/api/memories', {
      method: 'POST',
      token,
      body: JSON.stringify({
        title: title.trim(),
        body: body.trim(),
        occurredOn: occurredOn.trim() || null,
      }),
    });
    setTitle('');
    setBody('');
    setOccurredOn('');
    setSkip(0);
    await load();
  }

  async function saveSilent(item: MemoryItem) {
    if (!token) {
      return;
    }
    await api(`/api/memories/${item.id}`, {
      method: 'PUT',
      token,
      body: JSON.stringify({ title: item.title, body: item.body, occurredOn: item.occurredOn }),
    });
    await load();
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Memorias</Text>
      <Text style={styles.lead}>Linha do tempo do seu perfil. Foto, texto e data. Edicao e silenciosa.</Text>
      <Feedback message={error} />
      <TextField label="Titulo" value={title} onChangeText={setTitle} placeholder="Primeiro dia de escola" />
      <TextField label="Texto" value={body} onChangeText={setBody} />
      <TextField label="Quando" value={occurredOn} onChangeText={setOccurredOn} placeholder="2018-02-05" />
      <Button label="Guardar memoria" onPress={() => void create()} disabled={!title.trim()} />
      {items.map((item) => (
        <MemoryCard key={item.id} item={item} onSave={(next) => void saveSilent(next)} />
      ))}
      {hasMore ? (
        <Button label="Ver mais memorias" variant="ghost" onPress={() => setSkip((current) => current + pageSize)} />
      ) : null}
    </ScrollView>
  );
}

function MemoryCard({ item, onSave }: { item: MemoryItem; onSave: (item: MemoryItem) => void }) {
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [body, setBody] = useState(item.body);
  const savedRef = useRef(item.body);
  useEffect(() => {
    setBody((current) => keepTypedDraft(current, item.body, savedRef.current));
    savedRef.current = item.body;
  }, [item.body]);
  return (
    <>
      <Text style={styles.item}>{item.title}</Text>
      <Text style={styles.lead}>{item.occurredOn ?? item.createdAt.slice(0, 10)}</Text>
      {item.hasPhoto ? <Text style={styles.lead}>Com foto</Text> : null}
      <TextField label={`Texto de ${item.title}`} value={body} onChangeText={setBody} />
      <Button
        label={`Salvar ${item.title}`}
        variant="ghost"
        onPress={() => onSave({ ...item, body })}
        disabled={body === item.body}
      />
    </>
  );
}
