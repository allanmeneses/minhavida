import { useCallback, useState } from 'react';
import { Pressable, ScrollView, Text } from 'react-native';
import { useFocusEffect, useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type SharedListSummary } from '@/lib/api';
import { LIST_KIND_LABEL, LIST_KINDS, nextOf, type ListKind } from '@/lib/listKinds';
import { listProgressLabel } from '@/lib/listProgress';
import { examplePlaceholder } from '@/lib/placeholders';
import { screenStyles } from '@/lib/screenStyles';

export default function ListsScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [items, setItems] = useState<SharedListSummary[]>([]);
  const [name, setName] = useState('');
  const [kind, setKind] = useState<ListKind>('shopping');
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setItems(await api<SharedListSummary[]>('/api/lists', { token }));
  }, [token]);

  useFocusEffect(
    useCallback(() => {
      void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao abrir as listas.'));
    }, [load]),
  );

  async function create() {
    if (!token || !name.trim()) {
      return;
    }
    await api('/api/lists', {
      method: 'POST',
      token,
      body: JSON.stringify({ name: name.trim(), kind }),
    });
    setName('');
    await load();
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Listas</Text>
      <Text style={styles.lead}>Privadas no seu perfil. Compras, desejos, materiais ou outra.</Text>
      <Button label="Modelos de lista" variant="ghost" onPress={() => router.push('/(app)/templates' as Href)} />
      <Feedback message={error} />
      <TextField label="Nome da lista" value={name} onChangeText={setName} placeholder={examplePlaceholder('Feira')} />
      <Button
        label={`Tipo: ${LIST_KIND_LABEL[kind]}`}
        variant="ghost"
        onPress={() => setKind(nextOf(LIST_KINDS, kind))}
      />
      <Button label="Criar lista" onPress={() => void create()} disabled={!name.trim()} />
      {items.map((item) => (
        <Pressable
          key={item.id}
          accessibilityRole="button"
          accessibilityLabel={item.name}
          onPress={() => router.push(`/(app)/list/${item.id}` as Href)}>
          <Text style={styles.item}>{item.name}</Text>
          <Text style={styles.lead}>
            {LIST_KIND_LABEL[item.kind as ListKind] ?? item.kind} · {listProgressLabel(item.doneCount, item.itemCount)}
          </Text>
        </Pressable>
      ))}
    </ScrollView>
  );
}
