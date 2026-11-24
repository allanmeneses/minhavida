import { Animated, Platform, StyleSheet, Text, View } from 'react-native';
import { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/Button';
import { DateField } from '@/components/ui/DateField';
import { Feedback } from '@/components/ui/Feedback';
import { FilePicker } from '@/components/ui/FilePicker';
import { TextField } from '@/components/ui/TextField';
import { useOffline } from '@/context/OfflineContext';
import { useTheme } from '@/context/ThemeContext';
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
import { examplePlaceholder } from '@/lib/placeholders';
import { clipFromFile, startNativeRecording, startWebRecording, type LiveRecording } from '@/lib/voice';
import { recordingLabel, transcribingLabel, type VoiceUiState } from '@/lib/voiceUi';

type Props = {
  token: string;
  types: HomeRecordType[];
  onOpenType: (id: string) => void;
  onNewType: () => void;
  onSaved: () => Promise<void> | void;
};

export function CaptureBar({ token, types, onOpenType, onNewType, onSaved }: Props) {
  const { online, enqueueRecord } = useOffline();
  const { colors } = useTheme();
  const [text, setText] = useState('');
  const [suggestion, setSuggestion] = useState<RecordSuggestion | null>(null);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [voiceState, setVoiceState] = useState<VoiceUiState>('idle');
  const [elapsed, setElapsed] = useState(0);
  const [audioName, setAudioName] = useState<string | null>(null);
  const [editingSuggestion, setEditingSuggestion] = useState(false);
  const recordingRef = useRef<LiveRecording | null>(null);
  const pulse = useRef(new Animated.Value(1)).current;

  useEffect(() => {
    if (voiceState !== 'recording') {
      pulse.setValue(1);
      return;
    }
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, { toValue: 0.35, duration: 450, useNativeDriver: true }),
        Animated.timing(pulse, { toValue: 1, duration: 450, useNativeDriver: true }),
      ]),
    );
    loop.start();
    const timer = setInterval(() => {
      setElapsed(recordingRef.current?.elapsedSeconds() ?? 0);
    }, 1000);
    return () => {
      loop.stop();
      clearInterval(timer);
    };
  }, [pulse, voiceState]);

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
    setEditingSuggestion(false);
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
    setVoiceState('transcribing');
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
      setEditingSuggestion(false);
      setAudioName(null);
      if (next.transcript) {
        setText(next.transcript);
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : transcriptionUnavailable());
    } finally {
      setBusy(false);
      setVoiceState('idle');
    }
  }

  async function speak() {
    setError(null);
    setNotice(null);
    if (!online) {
      setError(voiceNeedsNetwork());
      return;
    }
    try {
      const session = Platform.OS === 'web' || typeof MediaRecorder !== 'undefined' ? await startWebRecording() : await startNativeRecording();
      recordingRef.current = session;
      setElapsed(0);
      setVoiceState('recording');
    } catch (err) {
      setVoiceState('idle');
      setError(err instanceof Error ? err.message : transcriptionUnavailable());
    }
  }

  async function stopRecording() {
    const session = recordingRef.current;
    recordingRef.current = null;
    if (!session) {
      setVoiceState('idle');
      return;
    }
    try {
      const clip = await session.stop();
      if (clip.blob) {
        const file = new File([clip.blob], clip.fileName, { type: clip.mimeType });
        await transcribeClip(file);
        return;
      }
      await transcribeClip({ uri: clip.uri, name: clip.fileName, mimeType: clip.mimeType });
    } catch (err) {
      setVoiceState('idle');
      setError(err instanceof Error ? err.message : transcriptionUnavailable());
    }
  }

  function cancelRecording() {
    recordingRef.current?.cancel();
    recordingRef.current = null;
    setVoiceState('idle');
    setElapsed(0);
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
      setEditingSuggestion(false);
      setAudioName(null);
      setText('');
      return;
    }
    setBusy(true);
    try {
      await api('/api/records', { method: 'POST', token, body: JSON.stringify(payload) });
      setSuggestion(null);
      setEditingSuggestion(false);
      setAudioName(null);
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
    setEditingSuggestion(false);
    setAudioName(null);
    setNotice(rejectedNotice());
  }

  const chips = recentTypeChips(types);

  return (
    <View style={styles.wrap}>
      <TextField
        label="Escrever"
        value={text}
        onChangeText={setText}
        placeholder={examplePlaceholder('O que aconteceu hoje')}
        multiline
      />
      <View style={styles.actions}>
        <View style={styles.action}>
          <Button label="Sugerir" onPress={() => void write()} disabled={!text.trim() || busy || voiceState !== 'idle'} />
        </View>
        <View style={styles.action}>
          <Button label="Falar" variant="ghost" onPress={() => void speak()} disabled={busy || voiceState !== 'idle'} />
        </View>
      </View>
      {voiceState === 'recording' ? (
        <View testID="voice-recording" style={[styles.recordBox, { borderColor: colors.line, backgroundColor: colors.surface }]}>
          <Animated.View
            testID="voice-pulse"
            style={[styles.pulse, { backgroundColor: colors.danger, opacity: pulse }]}
          />
          <Text testID="voice-timer" style={[styles.recordLabel, { color: colors.ink }]}>
            {recordingLabel(elapsed)}
          </Text>
          <Button label="Parar" onPress={() => void stopRecording()} />
          <Button label="Cancelar" variant="ghost" onPress={cancelRecording} />
        </View>
      ) : null}
      {voiceState === 'transcribing' ? (
        <Text testID="voice-transcribing" style={[styles.recordLabel, { color: colors.ink }]}>
          {transcribingLabel()}
        </Text>
      ) : null}
      {Platform.OS === 'web' && !suggestion && voiceState !== 'transcribing' ? (
        <FilePicker
          label="Enviar áudio"
          testID="voice-file"
          accessibilityLabel="Enviar áudio de teste"
          accept="audio/*,text/plain,.webm,.wav,.mp3,.m4a,.ogg,.txt"
          valueName={audioName}
          onFile={(file) => {
            setAudioName(file?.name ?? null);
            if (file) {
              void transcribeClip(clipFromFile(file).blob as File);
            }
          }}
        />
      ) : null}
      <Feedback message={error} />
      <Feedback message={notice} tone="ok" />
      {suggestion ? (
        <View testID="capture-suggestion" style={styles.suggestion}>
          <Text style={{ color: colors.ink }}>{suggestion.summary}</Text>
          {suggestion.transcript ? (
            <Text style={{ color: colors.ink }}>Transcrição: {suggestion.transcript}</Text>
          ) : null}
          <View
            testID="capture-suggestion-actions"
            style={[styles.suggestionActions, { backgroundColor: colors.bg }]}>
            <View style={styles.action}>
              <Button label="Aceitar" onPress={() => void accept()} disabled={busy} />
            </View>
            <View style={styles.action}>
              <Button
                label="Editar"
                variant="ghost"
                onPress={() => setEditingSuggestion(true)}
              />
            </View>
            <View style={styles.action}>
              <Button label="Recusar" variant="ghost" onPress={reject} />
            </View>
          </View>
          {editingSuggestion
            ? suggestion.type.fields.slice(0, 8).map((field) =>
                field.kind === 'date' ? (
                  <DateField
                    key={field.id}
                    label={field.name}
                    value={draft[field.id] ?? ''}
                    onChangeText={(value) => setDraft((current) => ({ ...current, [field.id]: value }))}
                  />
                ) : (
                  <TextField
                    key={field.id}
                    label={field.name}
                    value={draft[field.id] ?? ''}
                    onChangeText={(value) => setDraft((current) => ({ ...current, [field.id]: value }))}
                  />
                ),
              )
            : null}
        </View>
      ) : null}
      <View style={styles.chips}>
        {chips.map((item) => (
          <Button key={item.id} label={item.name} variant="ghost" onPress={() => onOpenType(item.id)} />
        ))}
        <Button label="Novo tipo" variant="ghost" onPress={onNewType} />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    gap: 12,
  },
  actions: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 12,
  },
  action: {
    flexGrow: 1,
    flexBasis: 0,
    minWidth: 0,
  },
  suggestion: {
    gap: 10,
  },
  suggestionActions: {
    flexDirection: 'row',
    flexWrap: 'nowrap',
    gap: 8,
    position: 'sticky' as unknown as 'relative',
    top: 0,
    zIndex: 3,
    paddingVertical: 8,
    width: '100%',
  },
  chips: {
    gap: 8,
  },
  recordBox: {
    borderWidth: 1,
    borderRadius: 12,
    padding: 12,
    gap: 8,
    alignItems: 'flex-start',
  },
  pulse: {
    width: 16,
    height: 16,
    borderRadius: 8,
  },
  recordLabel: {
    fontWeight: '700',
  },
});
