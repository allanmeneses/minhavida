import { useCallback, useEffect, useState } from 'react';
import { Platform, ScrollView, Text, View } from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { useLocalSearchParams } from 'expo-router';
import { LazyPhoto } from '@/components/LazyPhoto';
import { Button } from '@/components/ui/Button';
import { CircleShareChips } from '@/components/ui/CircleShareChips';
import { Feedback } from '@/components/ui/Feedback';
import { DateField } from '@/components/ui/DateField';
import { FilePicker } from '@/components/ui/FilePicker';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import {
  api,
  apiBlob,
  ApiError,
  downloadDocument,
  uploadNativeFile,
  type CircleSummary,
  type DocumentItem,
  type LifeArea,
  type LifeEntry,
  type SearchHit,
} from '@/lib/api';
import { entryExample } from '@/lib/entryExamples';
import { isoToDisplayDate, tagsPlaceholder } from '@/lib/dates';
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
  const [numberValue, setNumberValue] = useState('');
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [nativePhoto, setNativePhoto] = useState<ImagePicker.ImagePickerAsset | null>(null);
  const [tags, setTags] = useState('');
  const [circles, setCircles] = useState<CircleSummary[]>([]);
  const [selectedCircleIds, setSelectedCircleIds] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [pageTitle, setPageTitle] = useState('');
  const [pageBody, setPageBody] = useState('');
  const [pages, setPages] = useState<{ id: string; title: string; body: string }[]>([]);
  const [diaryQuery, setDiaryQuery] = useState('');
  const [diaryHits, setDiaryHits] = useState<SearchHit[]>([]);
  const example = entryExample(area?.kind);

  const load = useCallback(async () => {
    if (!token || !id) {
      return;
    }
    const areas = await api<LifeArea[]>('/api/life-areas', { token });
    setArea(areas.find((item) => item.id === id) ?? null);
    setEntries(await api<LifeEntry[]>(`/api/life-areas/${id}/entries?skip=0&take=20`, { token }));
    try {
      setCircles(await api<CircleSummary[]>('/api/circles', { token }));
    } catch {
      setCircles([]);
    }
    if (areas.find((item) => item.id === id)?.kind === 'document') {
      setDocuments(await api<DocumentItem[]>('/api/documents', { token }));
    }
    if (areas.find((item) => item.id === id)?.kind === 'diary') {
      try {
        setPages(await api(`/api/life-areas/${id}/pages`, { token }));
      } catch {
        setPages([]);
      }
    }
  }, [token, id]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao abrir a área.'));
  }, [load]);

  async function sendBrowserFile(entryId: string, file: File) {
    if (!token || !id) {
      return;
    }
    const form = new FormData();
    form.append('file', file, file.name);
    await api(`/api/life-areas/${id}/entries/${entryId}/photo`, { method: 'POST', token, body: form });
  }

  async function sendNativePhoto(entryId: string, asset: ImagePicker.ImagePickerAsset) {
    if (!token || !id) {
      return;
    }
    if (Platform.OS === 'web' && asset.file) {
      const form = new FormData();
      form.append('file', asset.file, asset.fileName ?? 'entrada.jpg');
      await api(`/api/life-areas/${id}/entries/${entryId}/photo`, { method: 'POST', token, body: form });
      return;
    }
    await uploadNativeFile(
      `/api/life-areas/${id}/entries/${entryId}/photo`,
      token,
      { uri: asset.uri, name: asset.fileName, mimeType: asset.mimeType ?? 'image/jpeg' },
      'entrada.jpg',
    );
  }

  async function addEntry() {
    if (!token || !id || !title.trim()) {
      return;
    }
    const parsedNumber = numberValue.trim() ? Number(numberValue.replace(',', '.')) : null;
    const parsedTags = tags
      .split(',')
      .map((item) => item.trim())
      .filter((item) => item.length > 0);
    const created = await api<LifeEntry>(`/api/life-areas/${id}/entries`, {
      method: 'POST',
      token,
      body: JSON.stringify({
        title: title.trim(),
        body: body.trim() || null,
        location: location.trim() || null,
        occurredOn: occurredOn.trim() || null,
        numberValue: parsedNumber !== null && Number.isFinite(parsedNumber) ? parsedNumber : null,
        tags: parsedTags,
        circleIds: selectedCircleIds,
      }),
    });
    if (photoFile) {
      await sendBrowserFile(created.id, photoFile);
    } else if (nativePhoto) {
      await sendNativePhoto(created.id, nativePhoto);
    }
    setTitle('');
    setBody('');
    setLocation('');
    setOccurredOn('');
    setNumberValue('');
    setTags('');
    setPhotoFile(null);
    setNativePhoto(null);
    await load();
  }

  async function pickLibrary() {
    const picked = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], quality: 0.85 });
    if (!picked.canceled && picked.assets[0]) {
      setNativePhoto(picked.assets[0]);
    }
  }

  async function pickCamera() {
    const permission = await ImagePicker.requestCameraPermissionsAsync();
    if (!permission.granted) {
      setError('Precisamos da câmera para tirar a foto.');
      return;
    }
    const picked = await ImagePicker.launchCameraAsync({ mediaTypes: ['images'], quality: 0.85 });
    if (!picked.canceled && picked.assets[0]) {
      setNativePhoto(picked.assets[0]);
    }
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
      anchor.download = `${area?.name ?? 'diário'}.txt`;
      anchor.click();
      URL.revokeObjectURL(url);
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>{area?.name ?? 'Área'}</Text>
      <Text style={styles.lead}>{example.hint}</Text>
      <Feedback message={error} />
      <TextField label="Título" value={title} onChangeText={setTitle} placeholder={example.titlePlaceholder} />
      {example.showWhen ? (
        <DateField label="Quando" value={occurredOn} onChangeText={setOccurredOn} />
      ) : null}
      {example.showWhere ? (
        <TextField label="Onde" value={location} onChangeText={setLocation} placeholder={example.wherePlaceholder} />
      ) : null}
      <TextField
        label="Número"
        value={numberValue}
        onChangeText={setNumberValue}
        placeholder={example.numberPlaceholder}
        keyboardType="decimal-pad"
      />
      <TextField label="Texto" value={body} onChangeText={setBody} placeholder={example.bodyPlaceholder} />
      <TextField
        label="Tags (opcional)"
        value={tags}
        onChangeText={setTags}
        placeholder={tagsPlaceholder()}
      />
      <CircleShareChips circles={circles} selectedIds={selectedCircleIds} onChange={setSelectedCircleIds} />
      {Platform.OS === 'web' ? (
        <FilePicker
          label="Foto da entrada"
          accessibilityLabel="Foto da entrada"
          accept="image/*"
          valueName={photoFile?.name ?? null}
          onFile={(file) => setPhotoFile(file)}
        />
      ) : (
        <>
          <Button label="Galeria" variant="ghost" onPress={() => void pickLibrary()} />
          <Button label="Câmera" variant="ghost" onPress={() => void pickCamera()} />
        </>
      )}
      {photoFile || nativePhoto ? <Text style={styles.lead}>Foto pronta para enviar com a entrada.</Text> : null}
      <Button label="Guardar entrada" onPress={() => void addEntry()} disabled={!title.trim()} />
      {area?.kind === 'diary' ? (
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
              void api(`/api/life-areas/${id}/pages`, {
                method: 'POST',
                token,
                body: JSON.stringify({ title: pageTitle.trim(), body: pageBody.trim() }),
              }).then(() => {
                setPageTitle('');
                setPageBody('');
                return load();
              });
            }}
          />
          {pages.map((page) => (
            <Text key={page.id} style={styles.item}>
              {page.title}
            </Text>
          ))}
          <TextField
            label="Palavra-chave"
            value={diaryQuery}
            onChangeText={setDiaryQuery}
            placeholder="palavra-chave"
          />
          <Button
            label="Buscar no diário"
            variant="ghost"
            disabled={!diaryQuery.trim()}
            onPress={() => {
              if (!token || !id || !diaryQuery.trim()) {
                return;
              }
              void api<SearchHit[]>(
                `/api/life-areas/${id}/search?q=${encodeURIComponent(diaryQuery.trim())}`,
                { token },
              ).then(setDiaryHits);
            }}
          />
          {diaryHits.map((hit) => (
            <Text key={hit.id} style={styles.lead}>
              {hit.title}
            </Text>
          ))}
          <Button label="Exportar texto" variant="ghost" onPress={() => void exportDiary()} />
        </View>
      ) : null}

      {entries.map((entry) => (
        <View key={entry.id} style={styles.card}>
          <Text style={styles.item}>{entry.title}</Text>
          {entry.numberValue !== null && entry.numberValue !== undefined ? (
            <Text style={styles.lead}>Número: {entry.numberValue}</Text>
          ) : null}
          {entry.occurredOn ? <Text style={styles.lead}>{isoToDisplayDate(entry.occurredOn)}</Text> : null}
          {entry.location ? <Text style={styles.lead}>{entry.location}</Text> : null}
          {entry.body ? <Text style={styles.lead}>{entry.body}</Text> : null}
          {(entry.tags ?? []).length > 0 ? (
            <Text style={styles.lead}>Tags: {(entry.tags ?? []).join(', ')}</Text>
          ) : null}
          {entry.hasPhoto && token && id ? <EntryPhoto areaId={id} entryId={entry.id} token={token} /> : null}
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

function EntryPhoto({ areaId, entryId, token }: { areaId: string; entryId: string; token: string }) {
  const [uri, setUri] = useState<string | null>(null);
  useEffect(() => {
    let objectUrl: string | null = null;
    let cancelled = false;
    (async () => {
      try {
        const blob = await apiBlob(`/api/life-areas/${areaId}/entries/${entryId}/photo`, token);
        objectUrl = URL.createObjectURL(blob);
        if (!cancelled) {
          setUri(objectUrl);
        } else {
          URL.revokeObjectURL(objectUrl);
        }
      } catch {
        if (!cancelled) {
          setUri(null);
        }
      }
    })();
    return () => {
      cancelled = true;
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [areaId, entryId, token]);
  if (!uri) {
    return <Text>Foto</Text>;
  }
  return <LazyPhoto uri={uri} alt="Foto da entrada salva" size={160} />;
}
