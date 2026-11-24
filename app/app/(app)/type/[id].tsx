import { useCallback, useEffect, useState } from 'react';
import { Platform, ScrollView, Text, View } from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { useLocalSearchParams } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type CircleSummary, type SearchHit } from '@/lib/api';
import { entryExample } from '@/lib/entryExamples';
import {
  extraFields,
  kindFromSlug,
  quickFields,
  tagsFrom,
  type LifeRecordItem,
  type RecordTypeItem,
} from '@/lib/records';
import { screenStyles } from '@/lib/screenStyles';

type CalendarItem = { id: string; title: string; occurredOn: string; numberValue: number | null };
type ChartPoint = { occurredOn: string | null; numberValue: number };

export default function RecordTypeScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [type, setType] = useState<RecordTypeItem | null>(null);
  const [records, setRecords] = useState<LifeRecordItem[]>([]);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [numberValue, setNumberValue] = useState('');
  const [tags, setTags] = useState('');
  const [showDetails, setShowDetails] = useState(true);
  const [circles, setCircles] = useState<CircleSummary[]>([]);
  const [selectedCircleIds, setSelectedCircleIds] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [pageTitle, setPageTitle] = useState('');
  const [pageBody, setPageBody] = useState('');
  const [diaryQuery, setDiaryQuery] = useState('');
  const [diaryHits, setDiaryHits] = useState<SearchHit[]>([]);
  const [calendar, setCalendar] = useState<CalendarItem[] | null>(null);
  const [chart, setChart] = useState<ChartPoint[]>([]);
  const [newFieldName, setNewFieldName] = useState('');
  const example = entryExample(kindFromSlug(type?.slug));

  const load = useCallback(async () => {
    if (!token || !id) {
      return;
    }
    const next = await api<RecordTypeItem>(`/api/record-types/${id}`, { token });
    setType(next);
    setRecords(await api<LifeRecordItem[]>(`/api/record-types/${id}/records`, { token }));
    try {
      setCircles(await api<CircleSummary[]>('/api/circles', { token }));
    } catch {
      setCircles([]);
    }
    if (next.hasNumber) {
      setChart(await api<ChartPoint[]>(`/api/record-types/${id}/chart`, { token }));
    }
  }, [token, id]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao abrir o tipo.'));
  }, [load]);

  function setField(fieldId: string, value: string) {
    setDraft((current) => ({ ...current, [fieldId]: value }));
  }

  async function addRecord() {
    if (!token || !id) {
      return;
    }
    const title = (draft.title ?? draft.Título ?? '').trim();
    if (!title && !(draft.title ?? '').trim()) {
      const fallback = Object.values(draft).find((item) => item.trim());
      if (!fallback) {
        return;
      }
    }
    const fields: Record<string, string> = { ...draft };
    if (!fields.title) {
      fields.title = title || 'Sem título';
    }
    if (numberValue.trim()) {
      fields.number = numberValue.trim();
      const numberField = (type?.fields ?? []).find((item) => item.kind === 'number');
      if (numberField) {
        fields[numberField.id] = numberValue.trim();
      }
    }
    if (tags.trim()) {
      fields.tags = tags.trim();
    }
    const created = await api<LifeRecordItem>('/api/records', {
      method: 'POST',
      token,
      body: JSON.stringify({
        typeId: id,
        fields,
        circleIds: selectedCircleIds,
      }),
    });
    setDraft({});
    setNumberValue('');
    setTags('');
    setSelectedCircleIds([]);
    setRecords((current) => [created, ...current]);
    await load();
  }

  async function remind(recordId: string) {
    if (!token) {
      return;
    }
    await api(`/api/records/${recordId}/remind`, { method: 'POST', token, body: JSON.stringify({}) });
    await load();
  }

  async function addField() {
    if (!token || !id || !newFieldName.trim()) {
      return;
    }
    await api(`/api/record-types/${id}/fields`, {
      method: 'POST',
      token,
      body: JSON.stringify({ name: newFieldName.trim(), kind: 'text' }),
    });
    setNewFieldName('');
    await load();
  }

  const visibleFields = type ? (showDetails ? type.fields.filter((item) => item.kind !== 'photo' && item.kind !== 'file') : quickFields(type.fields)) : [];
  const hasExtra = type ? extraFields(type.fields).length > 0 : false;

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>{type?.name ?? 'Tipo'}</Text>
      <Text style={styles.lead}>{type?.description || example.hint}</Text>
      <Feedback message={error} />
      {visibleFields.map((field) => (
        <TextField
          key={field.id}
          label={field.name}
          value={draft[field.id] ?? ''}
          onChangeText={(value) => setField(field.id, value)}
          placeholder={
            field.id === 'title'
              ? example.titlePlaceholder
              : field.kind === 'date'
                ? example.whenPlaceholder
                : field.kind === 'place'
                  ? example.wherePlaceholder
                  : undefined
          }
        />
      ))}
      {example.showWhen && !visibleFields.some((field) => field.kind === 'date') ? (
        <TextField label="Quando" value={draft.when ?? ''} onChangeText={(value) => setField('when', value)} placeholder={example.whenPlaceholder} />
      ) : null}
      {example.showWhere && !visibleFields.some((field) => field.kind === 'place') ? (
        <TextField label="Onde" value={draft.where ?? ''} onChangeText={(value) => setField('where', value)} placeholder={example.wherePlaceholder} />
      ) : null}
      <TextField
        label="Número"
        value={numberValue}
        onChangeText={setNumberValue}
        placeholder={example.numberPlaceholder}
        keyboardType="decimal-pad"
      />
      {!visibleFields.some((field) => field.id === 'body') ? (
        <TextField label="Texto" value={draft.body ?? ''} onChangeText={(value) => setField('body', value)} placeholder={example.bodyPlaceholder} />
      ) : null}
      <TextField label="Tags (opcional)" value={tags} onChangeText={setTags} placeholder="corrida, manhã" />
      {hasExtra ? (
        <Button
          label={showDetails ? 'Menos detalhes' : 'Mais detalhes'}
          variant="ghost"
          onPress={() => setShowDetails((value) => !value)}
        />
      ) : (
        <Button label="Mais detalhes" variant="ghost" onPress={() => setShowDetails(true)} />
      )}
      {circles.length > 0 ? (
        <View>
          <Text style={styles.lead}>Mostrar a mesma entrada em círculos, sem duplicar.</Text>
          {circles.map((circle) => (
            <Button
              key={circle.id}
              label={
                selectedCircleIds.includes(circle.id)
                  ? `No círculo: ${circle.name ?? 'círculo'}`
                  : `Fora do círculo: ${circle.name ?? 'círculo'}`
              }
              variant="ghost"
              onPress={() =>
                setSelectedCircleIds((current) =>
                  current.includes(circle.id)
                    ? current.filter((item) => item !== circle.id)
                    : [...current, circle.id],
                )
              }
            />
          ))}
        </View>
      ) : null}
      {Platform.OS === 'web' ? (
        <View>
          <Text nativeID="foto-registro-label" style={styles.lead}>
            Foto da entrada
          </Text>
          <input aria-labelledby="foto-registro-label" aria-label="Foto da entrada" type="file" accept="image/*" />
        </View>
      ) : (
        <>
          <Button
            label="Galeria"
            variant="ghost"
            onPress={() => void ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'] })}
          />
          <Button label="Câmera" variant="ghost" onPress={() => void ImagePicker.requestCameraPermissionsAsync()} />
        </>
      )}
      <Button
        label="Guardar entrada"
        onPress={() => void addRecord()}
        disabled={!((draft.title ?? '').trim() || Object.values(draft).some((item) => item.trim()))}
      />
      <TextField label="Novo campo" value={newFieldName} onChangeText={setNewFieldName} placeholder="Ex.: Lote" />
      <Button label="Adicionar campo" variant="ghost" onPress={() => void addField()} disabled={!newFieldName.trim()} />
      <Button
        label="Calendário deste tipo"
        variant="ghost"
        onPress={() => {
          if (!token || !id) {
            return;
          }
          void api<CalendarItem[]>(`/api/record-types/${id}/calendar`, { token }).then(setCalendar);
        }}
      />
      {calendar ? (
        <View style={styles.card}>
          <Text style={styles.section}>Calendário</Text>
          {calendar.length === 0 ? <Text style={styles.lead}>Nada neste período.</Text> : null}
          {calendar.map((item) => (
            <Text key={item.id} style={styles.item}>
              {item.occurredOn}: {item.title}
            </Text>
          ))}
        </View>
      ) : null}
      {chart.length > 0 ? (
        <View style={styles.card}>
          <Text style={styles.section}>Gráfico</Text>
          {chart.map((point, index) => (
            <Text key={`${point.occurredOn}-${index}`} style={styles.lead}>
              {point.occurredOn}: {point.numberValue}
            </Text>
          ))}
        </View>
      ) : null}

      {type?.slug === 'diario' ? (
        <View style={styles.card}>
          <Text style={styles.section}>Página livre</Text>
          <TextField label="Título da página" value={pageTitle} onChangeText={setPageTitle} />
          <TextField label="Texto da página" value={pageBody} onChangeText={setPageBody} />
          <Button
            label="Guardar página"
            variant="ghost"
            disabled={!pageTitle.trim()}
            onPress={() => {
              if (!token || !id || !pageTitle.trim()) {
                return;
              }
              void api('/api/records', {
                method: 'POST',
                token,
                body: JSON.stringify({
                  typeId: id,
                  fields: { title: pageTitle.trim(), body: pageBody.trim() },
                }),
              }).then(() => {
                setPageTitle('');
                setPageBody('');
                return load();
              });
            }}
          />
          <TextField label="Palavra-chave" value={diaryQuery} onChangeText={setDiaryQuery} placeholder="palavra-chave" />
          <Button
            label="Buscar no diário"
            variant="ghost"
            disabled={!diaryQuery.trim()}
            onPress={() => {
              if (!token || !diaryQuery.trim()) {
                return;
              }
              void api<{ hits: SearchHit[] }>(`/api/search?q=${encodeURIComponent(diaryQuery.trim())}&type=diary`, {
                token,
              }).then((payload) => setDiaryHits(payload.hits));
            }}
          />
          {diaryHits.map((hit) => (
            <Text key={hit.id} style={styles.lead}>
              {hit.title}
            </Text>
          ))}
          <Button
            label="Exportar texto"
            variant="ghost"
            onPress={() => {
              if (!token || !id) {
                return;
              }
              void api(`/api/record-types/${id}/export`, { token });
            }}
          />
        </View>
      ) : null}

      {records.map((record) => (
        <View key={record.id} style={styles.card}>
          <Text style={styles.item}>{record.title}</Text>
          {record.numberValue !== null && record.numberValue !== undefined ? (
            <Text style={styles.lead}>Número: {record.numberValue}</Text>
          ) : null}
          {record.occurredOn ? <Text style={styles.lead}>{record.occurredOn}</Text> : null}
          {typeof record.fields?.where === 'string' ? <Text style={styles.lead}>{record.fields.where}</Text> : null}
          {record.body ? <Text style={styles.lead}>{record.body}</Text> : null}
          {tagsFrom(record.fields).length > 0 ? (
            <Text style={styles.lead}>Tags: {tagsFrom(record.fields).join(', ')}</Text>
          ) : null}
          {record.occurredOn || type?.hasDate ? (
            <Button label="Lembrar desta data" variant="ghost" onPress={() => void remind(record.id)} />
          ) : null}
        </View>
      ))}
    </ScrollView>
  );
}
