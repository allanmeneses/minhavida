import { useCallback, useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { FieldChips } from '@/components/ui/FieldChips';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type LifeArea } from '@/lib/api';
import { AREA_KIND_LABEL, displayLabel } from '@/lib/labels';
import { enabledFields, fieldsToDraft, type SuggestedFieldDraft } from '@/lib/suggestFields';
import { type RecordTypeItem } from '@/lib/records';
import { screenStyles } from '@/lib/screenStyles';

export default function TypesScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [areas, setAreas] = useState<LifeArea[]>([]);
  const [types, setTypes] = useState<RecordTypeItem[]>([]);
  const [gallery, setGallery] = useState<RecordTypeItem[]>([]);
  const [sentence, setSentence] = useState('');
  const [suggestedName, setSuggestedName] = useState('');
  const [hasSuggestion, setHasSuggestion] = useState(false);
  const [suggestedDraft, setSuggestedDraft] = useState<SuggestedFieldDraft[]>([]);
  const [extraFieldName, setExtraFieldName] = useState('');
  const [newArea, setNewArea] = useState('');

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setAreas(await api<LifeArea[]>('/api/life-areas', { token }));
    setTypes(await api<RecordTypeItem[]>('/api/record-types', { token }));
    setGallery(await api<RecordTypeItem[]>('/api/record-types/gallery', { token }));
  }, [token]);

  useEffect(() => {
    void load().catch((err) => {
      setError(err instanceof ApiError ? err.message : 'Não foi possível abrir os tipos.');
    });
  }, [load]);

  async function createArea() {
    if (!token || !newArea.trim()) {
      return;
    }
    try {
      await api('/api/life-areas', { method: 'POST', token, body: JSON.stringify({ name: newArea.trim() }) });
      setNewArea('');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível criar a área.');
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.page} keyboardShouldPersistTaps="handled">
      <Text style={styles.kicker}>Registro</Text>
      <Text style={styles.title}>Meus tipos</Text>
      <Text style={styles.lead}>
        Descreva o que você quer guardar. O app sugere um nome e os campos. Você confirma, copia um exemplo ou cria do
        zero. Nada entra sozinho.
      </Text>
      <Feedback message={error} />

      <TextField
        label="Descrever um tipo"
        value={sentence}
        onChangeText={setSentence}
        placeholder="ex.: vacinas do meu filho"
      />
      <Button
        label="Sugerir tipo"
        disabled={!sentence.trim()}
        disabledHint="Escreva o que você quer guardar para sugerir o tipo."
        onPress={() => {
          if (!token || !sentence.trim()) {
            return;
          }
          void api<{ name: string; fields: { id: string; name: string; kind: string; required: boolean; order: number }[] }>(
            '/api/record-types/suggest',
            { method: 'POST', token, body: JSON.stringify({ sentence: sentence.trim() }) },
          )
            .then((payload) => {
              setSuggestedName(payload.name);
              setHasSuggestion(true);
              setSuggestedDraft(fieldsToDraft(payload.fields ?? []));
              setExtraFieldName('');
            })
            .catch((err) => {
              setError(err instanceof ApiError ? err.message : 'Não foi possível sugerir o tipo.');
            });
        }}
      />
      {hasSuggestion ? (
        <View style={styles.card}>
          <TextField
            label="Nome do tipo"
            value={suggestedName}
            onChangeText={setSuggestedName}
            placeholder="Ex.: Corridas"
          />
          <FieldChips
            fields={suggestedDraft}
            extraName={extraFieldName}
            onChange={setSuggestedDraft}
            onExtraName={setExtraFieldName}
          />
          <Button
            label="Usar esta sugestão"
            disabled={!suggestedName.trim()}
            onPress={() => {
              if (!token || !suggestedName.trim()) {
                return;
              }
              void api<RecordTypeItem>('/api/record-types', {
                method: 'POST',
                token,
                body: JSON.stringify({ name: suggestedName.trim(), fields: enabledFields(suggestedDraft) }),
              })
                .then((created) => {
                  setSentence('');
                  setSuggestedName('');
                  setHasSuggestion(false);
                  setSuggestedDraft([]);
                  router.push(`/(app)/type/${created.id}` as Href);
                })
                .catch((err) => {
                  setError(err instanceof ApiError ? err.message : 'Não foi possível criar o tipo.');
                });
            }}
          />
        </View>
      ) : null}

      <Text style={styles.section}>Seus tipos</Text>
      {types.length === 0 ? (
        <Text style={styles.lead}>Ainda não há um tipo seu. Descreva um ou copie um exemplo.</Text>
      ) : null}
      {types.map((item) => (
        <Pressable
          key={item.id}
          accessibilityRole="button"
          accessibilityLabel={item.name}
          onPress={() => router.push(`/(app)/type/${item.id}` as Href)}
          style={[extra.area, { borderBottomColor: colors.line }]}>
          <Text style={[extra.areaName, { color: colors.ink }]}>{item.name}</Text>
        </Pressable>
      ))}

      <Text style={styles.section}>Exemplos</Text>
      {gallery
        .filter((item) => !types.some((owned) => owned.name === item.name || owned.originTemplateId === item.id))
        .map((item) => (
          <Pressable
            key={item.id}
            accessibilityRole="button"
            accessibilityLabel={item.name}
            onPress={() => {
              if (!token) {
                return;
              }
              void api<RecordTypeItem>('/api/record-types/from-gallery', {
                method: 'POST',
                token,
                body: JSON.stringify({ templateId: item.id }),
              })
                .then((copied) => router.push(`/(app)/type/${copied.id}` as Href))
                .catch((err) => {
                  setError(err instanceof ApiError ? err.message : 'Não foi possível copiar o exemplo.');
                });
            }}
            style={[extra.area, { borderBottomColor: colors.line }]}>
            <Text style={[extra.areaName, { color: colors.ink }]}>{item.name}</Text>
            <Text style={styles.lead}>{item.description}</Text>
          </Pressable>
        ))}

      {areas.filter((area) => !types.some((item) => item.name === area.name)).length > 0 ? (
        <Text style={styles.section}>Áreas antigas</Text>
      ) : null}
      {areas
        .filter((area) => !types.some((item) => item.name === area.name))
        .map((area) => (
          <Pressable
            key={area.id}
            accessibilityRole="button"
            accessibilityLabel={area.name}
            onPress={() => router.push(`/(app)/life/${area.id}`)}
            style={[extra.area, { borderBottomColor: colors.line }]}>
            <Text style={[extra.areaName, { color: colors.ink }]}>{area.name}</Text>
            {displayLabel(area.kind, AREA_KIND_LABEL) && displayLabel(area.kind, AREA_KIND_LABEL) !== area.name ? (
              <Text style={styles.lead}>Tipo: {displayLabel(area.kind, AREA_KIND_LABEL)}</Text>
            ) : null}
          </Pressable>
        ))}

      <TextField label="Nova área" value={newArea} onChangeText={setNewArea} placeholder="Ex.: Corrida" />
      <Button
        label="Criar área"
        variant="ghost"
        onPress={() => void createArea()}
        disabled={!newArea.trim()}
        disabledHint="Informe o nome da área para criar."
      />
    </ScrollView>
  );
}

const extra = StyleSheet.create({
  area: {
    paddingVertical: 12,
    borderBottomWidth: 1,
    gap: 4,
  },
  areaName: {
    fontWeight: '700',
  },
});
