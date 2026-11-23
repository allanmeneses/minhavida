import { useCallback, useEffect, useState } from 'react';
import { Pressable, ScrollView, Text } from 'react-native';
import { useRouter } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type CircleSummary } from '@/lib/api';
import { CIRCLE_KIND_LABEL, CIRCLE_KINDS, type CircleKind } from '@/lib/circleKinds';
import { nextOf } from '@/lib/listKinds';
import { screenStyles } from '@/lib/screenStyles';

export default function CirclesScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [items, setItems] = useState<CircleSummary[]>([]);
  const [found, setFound] = useState<CircleSummary[]>([]);
  const [query, setQuery] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [visibility, setVisibility] = useState<'private' | 'public'>('private');
  const [discoverable, setDiscoverable] = useState(false);
  const [kind, setKind] = useState<CircleKind>('lasting');
  const [findable, setFindable] = useState(false);
  const [surprise, setSurprise] = useState(false);
  const [surpriseEmail, setSurpriseEmail] = useState('');
  const [endsAt, setEndsAt] = useState('');
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setItems(await api<CircleSummary[]>('/api/circles', { token }));
    setFound(await api<CircleSummary[]>('/api/circles/discover', { token }));
  }, [token]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Nao foi possivel listar.'));
  }, [load]);

  async function create() {
    if (!token || !name.trim()) {
      return;
    }
    setError(null);
    try {
      const created = await api<CircleSummary>('/api/circles', {
        method: 'POST',
        token,
        body: JSON.stringify({
          name: name.trim(),
          description: description.trim(),
          visibility,
          discoverable,
          findableByLocation: findable,
          latitude: findable ? -23.55 : null,
          longitude: findable ? -46.63 : null,
          kind,
          isSurprise: surprise,
          surpriseSubjectEmail: surprise ? surpriseEmail.trim() || null : null,
          endsAt: kind !== 'lasting' && endsAt.trim() ? new Date(endsAt.trim()).toISOString() : null,
        }),
      });
      setName('');
      setDescription('');
      setSurpriseEmail('');
      router.push(`/(app)/circle/${created.id}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel criar o circulo.');
    }
  }

  async function search() {
    if (!token) {
      return;
    }
    const q = encodeURIComponent(query.trim());
    setFound(await api<CircleSummary[]>(`/api/circles/discover${q ? `?q=${q}` : ''}`, { token }));
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Circulos</Text>
      <Text style={styles.lead}>Crie o da sua vida. Entrar no de outra pessoa pede convite ou pedido.</Text>
      <Feedback message={error} />
      <TextField label="Nome" value={name} onChangeText={setName} />
      <TextField label="Descricao" value={description} onChangeText={setDescription} />
      <Button
        label={visibility === 'private' ? 'Privado' : 'Publico'}
        variant="ghost"
        onPress={() => setVisibility(visibility === 'private' ? 'public' : 'private')}
      />
      <Button
        label={discoverable ? 'Aparece na busca' : 'Oculto na busca'}
        variant="ghost"
        onPress={() => setDiscoverable(!discoverable)}
      />
      <Button
        label={findable ? 'Pode ser encontrado por perto' : 'Nao aparecer na busca por perto'}
        variant="ghost"
        onPress={() => setFindable(!findable)}
      />
      <Button
        label={`Tipo: ${CIRCLE_KIND_LABEL[kind]}`}
        variant="ghost"
        onPress={() => setKind(nextOf(CIRCLE_KINDS, kind))}
      />
      <Button
        label={surprise ? 'Surpresa ligada' : 'Surpresa desligada'}
        variant="ghost"
        onPress={() => setSurprise(!surprise)}
      />
      {surprise ? (
        <TextField label="E-mail de quem vai se surpreender" value={surpriseEmail} onChangeText={setSurpriseEmail} />
      ) : null}
      {kind !== 'lasting' ? (
        <TextField
          label="Data de fim"
          value={endsAt}
          onChangeText={setEndsAt}
          placeholder="AAAA-MM-DD"
        />
      ) : null}
      <Button label="Criar circulo" onPress={() => void create()} disabled={!name.trim()} />

      <Text style={styles.section}>Descobrir circulos</Text>
      <Text style={styles.lead}>Busca por interesse em comum. So aparece quem marcou aceitar ser encontrado.</Text>
      <TextField label="Buscar circulos" value={query} onChangeText={setQuery} placeholder="corrida, familia" />
      <Button label="Buscar" variant="ghost" onPress={() => void search()} />
      {found.map((item) => (
        <Pressable
          key={item.id}
          accessibilityRole="button"
          accessibilityLabel={`Descobrir ${item.name ?? 'circulo'}`}
          onPress={() => router.push(`/(app)/circle/${item.id}`)}
          style={styles.card}>
          <Text style={styles.item}>{item.name ?? 'Circulo'}</Text>
          <Text style={styles.lead}>
            {item.description || 'Sem descricao'} · {item.visibility}
          </Text>
        </Pressable>
      ))}

      <Text style={styles.section}>Os seus</Text>
      {items.map((item) => (
        <Pressable
          key={item.id}
          accessibilityRole="button"
          accessibilityLabel={`Abrir ${item.name ?? 'circulo'}`}
          onPress={() => router.push(`/(app)/circle/${item.id}`)}
          style={styles.card}>
          <Text style={styles.item}>{item.name ?? 'Circulo oculto'}</Text>
          <Text style={styles.lead}>
            {item.visibility} · {CIRCLE_KIND_LABEL[item.kind as CircleKind] ?? item.kind} · {item.status} · {item.memberCount} pessoa(s)
          </Text>
        </Pressable>
      ))}
    </ScrollView>
  );
}
