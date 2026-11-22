import { useCallback, useEffect, useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type SearchResponse } from '@/lib/api';
import { screenStyles } from '@/lib/screenStyles';

const FILTERS = ['', 'entry', 'circle', 'birthday', 'diary'] as const;

export default function SearchScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<(typeof FILTERS)[number]>('');
  const [result, setResult] = useState<SearchResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadHistory = useCallback(async () => {
    if (!token) {
      return;
    }
    const history = await api<SearchResponse['history']>('/api/search/history', { token });
    setResult({ hits: [], history });
  }, [token]);

  useEffect(() => {
    void loadHistory().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha na busca.'));
  }, [loadHistory]);

  async function run(term = query) {
    if (!token || !term.trim()) {
      return;
    }
    setError(null);
    try {
      const type = filter ? `&type=${filter}` : '';
      setResult(await api<SearchResponse>(`/api/search?q=${encodeURIComponent(term.trim())}${type}`, { token }));
      setQuery(term);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Falha na busca.');
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Busca</Text>
      <Text style={styles.lead}>Entradas, circulos, aniversarios e o diario, juntos. Filtro opcional por tipo.</Text>
      <Feedback message={error} />
      <TextField label="Buscar" value={query} onChangeText={setQuery} placeholder="medicacao" />
      <Button
        label={filter ? `Filtro: ${filter}` : 'Filtro: tudo'}
        variant="ghost"
        onPress={() => setFilter(FILTERS[(FILTERS.indexOf(filter) + 1) % FILTERS.length])}
      />
      <Button label="Buscar agora" onPress={() => void run()} disabled={!query.trim()} />

      <Text style={styles.section}>Mais usadas</Text>
      {result?.history.frequent.map((term) => (
        <Pressable key={`f-${term}`} onPress={() => void run(term)}>
          <Text style={styles.item}>{term}</Text>
        </Pressable>
      ))}
      <Text style={styles.section}>Recentes</Text>
      {result?.history.recent.map((term) => (
        <Pressable key={`r-${term}`} onPress={() => void run(term)}>
          <Text style={styles.lead}>{term}</Text>
        </Pressable>
      ))}

      <Text style={styles.section}>Resultados</Text>
      {result?.hits.map((hit) => (
        <View key={`${hit.type}-${hit.id}`} style={styles.card}>
          <Text style={styles.item}>{hit.title}</Text>
          <Text style={styles.lead}>{hit.type}</Text>
          {hit.type === 'circle' ? (
            <Button label="Abrir circulo" variant="ghost" onPress={() => router.push(`/(app)/circle/${hit.id}`)} />
          ) : null}
        </View>
      ))}
    </ScrollView>
  );
}
