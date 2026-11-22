import { useCallback, useEffect, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, downloadDocument, type DocumentItem, type LifeArea, type LifeEntry } from '@/lib/api';
import { screenStyles } from '@/lib/screenStyles';

export default function LifeAreaScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [area, setArea] = useState<LifeArea | null>(null);
  const [entries, setEntries] = useState<LifeEntry[]>([]);
  const [documents, setDocuments] = useState<DocumentItem[]>([]);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [location, setLocation] = useState('');
  const [occurredOn, setOccurredOn] = useState('');
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token || !id) {
      return;
    }
    const areas = await api<LifeArea[]>('/api/life-areas', { token });
    setArea(areas.find((item) => item.id === id) ?? null);
    setEntries(await api<LifeEntry[]>(`/api/life-areas/${id}/entries`, { token }));
    if (areas.find((item) => item.id === id)?.kind === 'document') {
      setDocuments(await api<DocumentItem[]>('/api/documents', { token }));
    }
  }, [token, id]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao abrir a area.'));
  }, [load]);

  async function addEntry() {
    if (!token || !id || !title.trim()) {
      return;
    }
    await api(`/api/life-areas/${id}/entries`, {
      method: 'POST',
      token,
      body: JSON.stringify({
        title: title.trim(),
        body: body.trim() || null,
        location: location.trim() || null,
        occurredOn: occurredOn.trim() || null,
      }),
    });
    setTitle('');
    setBody('');
    setLocation('');
    setOccurredOn('');
    await load();
  }

  async function exportDiary() {
    if (!token || !id) {
      return;
    }
    const response = await fetch(`${(await import('@/lib/config')).API_URL}/api/life-areas/${id}/export`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    const text = await response.text();
    if (typeof document !== 'undefined') {
      const blob = new Blob([text], { type: 'text/plain' });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `${area?.name ?? 'diario'}.txt`;
      anchor.click();
      URL.revokeObjectURL(url);
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>{area?.name ?? 'Area'}</Text>
      <Text style={styles.lead}>
        {area?.kind === 'vaccine' ? 'Exemplo: vacina, quando e onde.' : 'Texto, numero ou data. Sem video.'}
      </Text>
      <Feedback message={error} />
      <TextField label="Titulo" value={title} onChangeText={setTitle} placeholder="Hepatite B" />
      {area?.kind === 'vaccine' ? (
        <>
          <TextField label="Quando" value={occurredOn} onChangeText={setOccurredOn} placeholder="AAAA-MM-DD" />
          <TextField label="Onde" value={location} onChangeText={setLocation} placeholder="UBS Centro" />
        </>
      ) : null}
      <TextField label="Texto" value={body} onChangeText={setBody} placeholder="Opcional" />
      <Button label="Guardar entrada" onPress={() => void addEntry()} disabled={!title.trim()} />
      {area?.kind === 'diary' ? <Button label="Exportar texto" variant="ghost" onPress={() => void exportDiary()} /> : null}

      {entries.map((entry) => (
        <View key={entry.id} style={styles.card}>
          <Text style={styles.item}>{entry.title}</Text>
          {entry.occurredOn ? <Text style={styles.lead}>{entry.occurredOn}</Text> : null}
          {entry.location ? <Text style={styles.lead}>{entry.location}</Text> : null}
          {entry.body ? <Text style={styles.lead}>{entry.body}</Text> : null}
        </View>
      ))}

      {documents.map((doc) => (
        <View key={doc.id} style={styles.card}>
          <Text style={styles.item}>{doc.fileName}</Text>
          <Button label="Baixar" variant="ghost" onPress={() => token && void downloadDocument(doc.id, doc.fileName, token)} />
        </View>
      ))}
    </ScrollView>
  );
}

