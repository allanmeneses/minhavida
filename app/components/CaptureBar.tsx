import { Platform, Text, View } from 'react-native';
import { useState } from 'react';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useOffline } from '@/context/OfflineContext';
import { api, ApiError, uploadNativeFile } from '@/lib/api';
import {
  acceptPayload,
  recentTypeChips,
  rejectedNotice,
  suggestionDraft,
  transcriptionUnavailable,
  voiceNeedsNetwork,
  type HomeRecordType,
  type RecordSuggestion,
} from '@/lib/records';
import { canUseMediaRecorder, clipFromFile, recordNative, recordWithMediaRecorder } from '@/lib/voice';

type Props = {
  token: string;
  types: HomeRecordType[];
  onOpenType: (id: string) => void;
  onSaved: () => Promise<void> | void;
};

export function CaptureBar({ token, types, onOpenType, onSaved }: Props) {
  const { online, enqueueRecord } = useOffline();
  const [text, setText] = useState('');
  const [suggestion, setSuggestion] = useState<RecordSuggestion | null>(null);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function suggestFrom(value: string, transcript?: string | null) {
    setError(null);
    setNotice(null);
    const next = await api<RecordSuggestion>('/api/records/suggest', {
      method: 'POST',
      token,
      body: JSON.stringify({ text: value }),
    });
    if (transcript) {
      next.transcript = transcript;
    }
    setSuggestion(next);
    setDraft(suggestionDraft(next));
  }

  async function write() {
    if (!text.trim()) {
      return;
    }
    setBusy(true);
    try {
      await suggestFrom(text.trim());
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível sugerir um tipo.');
    } finally {
      setBusy(false);
    }
  }

  async function transcribeClip(file: File | { uri: string; name: string; mimeType: string }) {
    if (!online) {
      setError(voiceNeedsNetwork());
      return;
    }
    setBusy(true);
    setError(null);
    try {
      let next: RecordSuggestion;
      if ('uri' in file && !('size' in file)) {
        next = await uploadNativeFile<RecordSuggestion>(
          '/api/records/transcribe',
          token,
          { uri: file.uri, name: file.name, mimeType: file.mimeType },
          file.name,
        );
      } else {
        const form = new FormData();
        form.append('file', file as File, (file as File).name);
        next = await api<RecordSuggestion>('/api/records/transcribe', { method: 'POST', token, body: form });
      }
      setSuggestion(next);
      setDraft(suggestionDraft(next));
      if (next.transcript) {
        setText(next.transcript);
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : transcriptionUnavailable());
    } finally {
      setBusy(false);
    }
  }

  async function speak() {
    setError(null);
    if (!online) {
      setError(voiceNeedsNetwork());
      return;
    }
    try {
      const clip =
        Platform.OS === 'web' || canUseMediaRecorder()
          ? await recordWithMediaRecorder(2)
          : await recordNative();
      if (clip.blob) {
        const file = new File([clip.blob], clip.fileName, { type: clip.mimeType });
        await transcribeClip(file);
        return;
      }
      await transcribeClip({ uri: clip.uri, name: clip.fileName, mimeType: clip.mimeType });
    } catch (err) {
      setError(err instanceof Error ? err.message : transcriptionUnavailable());
    }
  }

  async function accept() {
    if (!suggestion) {
      return;
    }
    let typeId = suggestion.typeId;
    if (!typeId && suggestion.type.galleryTypeId && online) {
      const copied = await api<{ id: string }>('/api/record-types/from-gallery', {
        method: 'POST',
        token,
        body: JSON.stringify({ templateId: suggestion.type.galleryTypeId }),
      });
      typeId = copied.id;
    }
    const payload = acceptPayload(suggestion, draft, typeId);
    if (!online) {
      enqueueRecord(payload);
      setNotice('Texto na fila. Sobe quando a rede voltar.');
      setSuggestion(null);
      setText('');
      return;
    }
    setBusy(true);
    try {
      await api('/api/records', { method: 'POST', token, body: JSON.stringify(payload) });
      setSuggestion(null);
      setText('');
      setNotice('Registro guardado.');
      await onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível guardar.');
    } finally {
      setBusy(false);
    }
  }

  function reject() {
    setSuggestion(null);
    setNotice(rejectedNotice());
  }

  const chips = recentTypeChips(types);

  return (
    <View>
      <TextField label="Escrever" value={text} onChangeText={setText} placeholder="O que aconteceu hoje" />
      <Button label="Sugerir" onPress={() => void write()} disabled={!text.trim() || busy} />
      <Button label="Falar" variant="ghost" onPress={() => void speak()} disabled={busy} />
      {Platform.OS === 'web' ? (
        <input
          data-testid="voice-file"
          aria-label="Enviar áudio de teste"
          type="file"
          accept="audio/*,text/plain,.webm,.wav,.mp3,.m4a,.ogg,.txt"
          onChange={(event) => {
            const file = event.currentTarget.files?.[0];
            if (file) {
              void transcribeClip(clipFromFile(file).blob as File);
            }
          }}
        />
      ) : null}
      <Feedback message={error} />
      <Feedback message={notice} tone="ok" />
      {suggestion ? (
        <View>
          <Text>{suggestion.summary}</Text>
          {suggestion.transcript ? <Text>Transcrição: {suggestion.transcript}</Text> : null}
          {suggestion.type.fields.slice(0, 8).map((field) => (
            <TextField
              key={field.id}
              label={field.name}
              value={draft[field.id] ?? ''}
              onChangeText={(value) => setDraft((current) => ({ ...current, [field.id]: value }))}
            />
          ))}
          <Button label="Aceitar" onPress={() => void accept()} disabled={busy} />
          <Button label="Recusar" variant="ghost" onPress={reject} />
        </View>
      ) : null}
      <View>
        {chips.map((item) => (
          <Button key={item.id} label={item.name} variant="ghost" onPress={() => onOpenType(item.id)} />
        ))}
      </View>
    </View>
  );
}
