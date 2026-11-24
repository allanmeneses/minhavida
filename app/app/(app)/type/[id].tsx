import { useCallback, useEffect, useMemo, useState } from 'react';
import { Platform, ScrollView, Text, View } from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { useLocalSearchParams } from 'expo-router';
import { MonthGrid } from '@/components/MonthGrid';
import { NumberChart } from '@/components/NumberChart';
import { Button } from '@/components/ui/Button';
import { CircleShareChips } from '@/components/ui/CircleShareChips';
import { DateField } from '@/components/ui/DateField';
import { Feedback } from '@/components/ui/Feedback';
import { FilePicker } from '@/components/ui/FilePicker';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type CircleSummary, type SearchHit } from '@/lib/api';
import { buildRecordMonth, shiftMonth } from '@/lib/calendarMonth';
import { isoToDisplayDate, monthTitle, tagsPlaceholder } from '@/lib/dates';
import { entryExample } from '@/lib/entryExamples';
import {
  extraFields,
  kindFromSlug,
  quickFields,
  tagsFrom,
  type LifeRecordItem,
  type RecordField,
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
  const [showDetails, setShowDetails] = useState(false);
  const [circles, setCircles] = useState<CircleSummary[]>([]);
  const [selectedCircleIds, setSelectedCircleIds] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [pageTitle, setPageTitle] = useState('');
  const [pageBody, setPageBody] = useState('');
  const [diaryQuery, setDiaryQuery] = useState('');
  const [diaryHits, setDiaryHits] = useState<SearchHit[]>([]);
  const [calendar, setCalendar] = useState<CalendarItem[] | null>(null);
  const now = new Date();
  const [calYear, setCalYear] = useState(now.getUTCFullYear());
  const [calMonth, setCalMonth] = useState(now.getUTCMonth() + 1);
  const [chart, setChart] = useState<ChartPoint[]>([]);
  const [newFieldName, setNewFieldName] = useState('');
  const [photoName, setPhotoName] = useState<string | null>(null);
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

  const quick = type ? quickFields(type.fields) : [];
  const extra = type ? extraFields(type.fields).filter((item) => item.kind !== 'photo' && item.kind !== 'file') : [];
  const visibleFields = showDetails
    ? [...quick, ...extra.filter((field) => !quick.some((item) => item.id === field.id))]
    : quick;
  const hasMore = extra.length > 0 || Boolean(type && !quick.some((field) => field.kind === 'number')) || Boolean(type && !quick.some((field) => field.id === 'body'));

  const calendarDays = useMemo(
    () => buildRecordMonth(calYear, calMonth, calendar ?? []),
    [calYear, calMonth, calendar],
  );

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>{type?.name ?? 'Tipo'}</Text>
      <Text style={styles.lead}>{type?.description || example.hint}</Text>
      <Feedback message={error} />
      {visibleFields.map((field) => (
        <FieldInput
          key={field.id}
          field={field}
          value={draft[field.id] ?? ''}
          example={example}
          onChange={(value) => setField(field.id, value)}
        />
      ))}
      {showDetails && example.showWhen && !visibleFields.some((field) => field.kind === 'date') ? (
        <DateField label="Quando" value={draft.when ?? ''} onChangeText={(value) => setField('when', value)} />
      ) : null}
      {showDetails && example.showWhere && !visibleFields.some((field) => field.kind === 'place') ? (
        <TextField label="Onde" value={draft.where ?? ''} onChangeText={(value) => setField('where', value)} placeholder={example.wherePlaceholder} />
      ) : null}
      {showDetails && !visibleFields.some((field) => field.kind === 'number') ? (
        <TextField
          label="Número"
          value={numberValue}
          onChangeText={setNumberValue}
          placeholder={example.numberPlaceholder}
          keyboardType="decimal-pad"
        />
      ) : null}
      {showDetails && !visibleFields.some((field) => field.id === 'body') ? (
        <TextField label="Texto" value={draft.body ?? ''} onChangeText={(value) => setField('body', value)} placeholder={example.bodyPlaceholder} />
      ) : null}
      {showDetails ? (
        <TextField label="Tags (opcional)" value={tags} onChangeText={setTags} placeholder={tagsPlaceholder()} />
      ) : null}
      {hasMore ? (
        <Button
          label={showDetails ? 'Menos detalhes' : 'Mais detalhes'}
          variant="ghost"
          onPress={() => setShowDetails((value) => !value)}
        />
      ) : null}
      <CircleShareChips circles={circles} selectedIds={selectedCircleIds} onChange={setSelectedCircleIds} />
      {showDetails && Platform.OS === 'web' ? (
        <FilePicker
          label="Foto da entrada"
          accessibilityLabel="Foto da entrada"
          accept="image/*"
          valueName={photoName}
          onFile={(file) => setPhotoName(file?.name ?? null)}
        />
      ) : null}
      {showDetails && Platform.OS !== 'web' ? (
        <>
          <Button
            label="Galeria"
            variant="ghost"
            onPress={() => void ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'] })}
          />
          <Button label="Câmera" variant="ghost" onPress={() => void ImagePicker.requestCameraPermissionsAsync()} />
        </>
      ) : null}
      <Button
        label="Guardar entrada"
        onPress={() => void addRecord()}
        disabled={!((draft.title ?? '').trim() || Object.values(draft).some((item) => item.trim()))}
      />
      {showDetails ? (
        <>
          <TextField label="Novo campo" value={newFieldName} onChangeText={setNewFieldName} placeholder="Ex.: Lote" />
          <Button label="Adicionar campo" variant="ghost" onPress={() => void addField()} disabled={!newFieldName.trim()} />
        </>
      ) : null}
      <Button
        label="Calendário deste tipo"
        variant="ghost"
        onPress={() => {
          if (!token || !id) {
            return;
          }
          void api<CalendarItem[]>(`/api/record-types/${id}/calendar`, { token }).then((items) => {
            setCalendar(items);
            const first = items[0]?.occurredOn;
            if (first && /^\d{4}-\d{2}-\d{2}$/.test(first)) {
              setCalYear(Number(first.slice(0, 4)));
              setCalMonth(Number(first.slice(5, 7)));
            }
          });
        }}
      />
      {calendar ? (
        <View>
          <Button
            label="Mês anterior"
            variant="ghost"
            onPress={() => {
              const next = shiftMonth(calYear, calMonth, -1);
              setCalYear(next.year);
              setCalMonth(next.month);
            }}
          />
          <MonthGrid title={monthTitle(calYear, calMonth)} days={calendarDays} />
          <Button
            label="Próximo mês"
            variant="ghost"
            onPress={() => {
              const next = shiftMonth(calYear, calMonth, 1);
              setCalYear(next.year);
              setCalMonth(next.month);
            }}
          />
        </View>
      ) : null}
      {type?.hasNumber ? <NumberChart title="Gráfico" points={chart} /> : null}

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
          {record.occurredOn ? <Text style={styles.lead}>{isoToDisplayDate(record.occurredOn)}</Text> : null}
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

function FieldInput({
  field,
  value,
  example,
  onChange,
}: {
  field: RecordField;
  value: string;
  example: ReturnType<typeof entryExample>;
  onChange: (value: string) => void;
}) {
  if (field.kind === 'date') {
    return <DateField label={field.name} value={value} onChangeText={onChange} />;
  }
  return (
    <TextField
      label={field.name}
      value={value}
      onChangeText={onChange}
      placeholder={
        field.id === 'title'
          ? example.titlePlaceholder
          : field.kind === 'place'
            ? example.wherePlaceholder
            : field.kind === 'number'
              ? example.numberPlaceholder
              : undefined
      }
      keyboardType={field.kind === 'number' ? 'decimal-pad' : undefined}
    />
  );
}
