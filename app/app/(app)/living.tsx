import { useCallback, useEffect, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type CircleSummary, type LivingProfileItem } from '@/lib/api';
import { examplePlaceholder } from '@/lib/placeholders';
import { screenStyles } from '@/lib/screenStyles';

const KINDS = ['person', 'pet', 'group'] as const;
const KIND_LABEL: Record<(typeof KINDS)[number], string> = {
  person: 'Pessoa',
  pet: 'Pet',
  group: 'Grupo',
};

export default function LivingScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [items, setItems] = useState<LivingProfileItem[]>([]);
  const [circles, setCircles] = useState<CircleSummary[]>([]);
  const [name, setName] = useState('');
  const [context, setContext] = useState('');
  const [kind, setKind] = useState<(typeof KINDS)[number]>('pet');
  const [circleId, setCircleId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [grantUserId, setGrantUserId] = useState('');
  const [grantAccess, setGrantAccess] = useState<'view' | 'edit'>('view');
  const [circlesLoaded, setCirclesLoaded] = useState(false);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setItems(await api<LivingProfileItem[]>('/api/living-profiles', { token }));
    setCircles(await api<CircleSummary[]>('/api/circles', { token }));
    setCirclesLoaded(true);
  }, [token]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao abrir os perfis.'));
  }, [load]);

  async function create() {
    if (!token || !name.trim()) {
      return;
    }
    await api('/api/living-profiles', {
      method: 'POST',
      token,
      body: JSON.stringify({
        name: name.trim(),
        kind,
        context: context.trim() || null,
        circleId,
      }),
    });
    setName('');
    setContext('');
    await load();
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Pet e grupo</Text>
      <Text style={styles.lead}>
        Perfil vivo de pessoa, pet ou grupo. Edita quem tem permissão de edição. Quem só visualiza só visualiza.
      </Text>
      <Feedback message={error} />
      <TextField label="Nome" value={name} onChangeText={setName} placeholder={examplePlaceholder('Luna')} />
      <TextField label="Contexto" value={context} onChangeText={setContext} placeholder={examplePlaceholder('Nossa cachorra')} />
      <Button
        label={`Tipo: ${KIND_LABEL[kind]}`}
        variant="ghost"
        onPress={() => setKind(KINDS[(KINDS.indexOf(kind) + 1) % KINDS.length])}
      />
      <Button
        label={circleId ? `Círculo: ${circles.find((item) => item.id === circleId)?.name ?? 'escolhido'}` : 'Só no meu perfil'}
        variant="ghost"
        disabled={!circlesLoaded}
        disabledHint="Aguarde os círculos carregarem para escolher."
        onPress={() => {
          if (circles.length === 0) {
            return;
          }
          if (!circleId) {
            setCircleId(circles[0].id);
            return;
          }
          const index = circles.findIndex((item) => item.id === circleId);
          setCircleId(index + 1 >= circles.length ? null : circles[index + 1].id);
        }}
      />
      <Button
        label="Criar perfil"
        onPress={() => void create()}
        disabled={!name.trim()}
        disabledHint="Informe o nome do perfil para criar."
      />
      <TextField
        label="Código da pessoa para permissão"
        value={grantUserId}
        onChangeText={setGrantUserId}
        placeholder="código da pessoa"
      />
      <Button
        label={`Permissão: ${grantAccess === 'edit' ? 'editar' : 'visualizar'}`}
        variant="ghost"
        onPress={() => setGrantAccess(grantAccess === 'edit' ? 'view' : 'edit')}
      />
      {items.map((item) => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.item}>{item.name}</Text>
          <Text style={styles.lead}>
            {KIND_LABEL[item.kind as (typeof KINDS)[number]] ?? item.kind}
            {item.context ? ` · ${item.context}` : ''}
            {item.canEdit ? ' · pode editar' : ' · só visualiza'}
          </Text>
          {item.canEdit ? (
            <Button
              label={`Conceder ${grantAccess === 'edit' ? 'editar' : 'visualizar'} em ${item.name}`}
              variant="ghost"
              onPress={() => {
                if (!token || !grantUserId.trim()) {
                  return;
                }
                void api(`/api/living-profiles/${item.id}/grants`, {
                  method: 'PUT',
                  token,
                  body: JSON.stringify({ userId: grantUserId.trim(), access: grantAccess }),
                }).then(() => load());
              }}
            />
          ) : null}
        </View>
      ))}
    </ScrollView>
  );
}
