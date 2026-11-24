export type VoiceClip = {
  uri: string;
  mimeType: string;
  fileName: string;
  blob?: Blob;
};

export function canUseMediaRecorder(): boolean {
  return typeof MediaRecorder !== 'undefined' && typeof navigator !== 'undefined' && Boolean(navigator.mediaDevices?.getUserMedia);
}

export async function recordWithMediaRecorder(seconds = 4): Promise<VoiceClip> {
  if (!canUseMediaRecorder()) {
    throw new Error('Este aparelho não grava áudio aqui.');
  }
  const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
  const mimeType = MediaRecorder.isTypeSupported('audio/webm') ? 'audio/webm' : 'audio/mp4';
  const recorder = new MediaRecorder(stream, { mimeType });
  const chunks: BlobPart[] = [];
  const done = new Promise<Blob>((resolve, reject) => {
    recorder.ondataavailable = (event) => {
      if (event.data.size > 0) {
        chunks.push(event.data);
      }
    };
    recorder.onerror = () => reject(new Error('Não foi possível gravar o áudio.'));
    recorder.onstop = () => resolve(new Blob(chunks, { type: mimeType }));
  });
  recorder.start();
  await new Promise((resolve) => setTimeout(resolve, Math.max(800, seconds * 1000)));
  if (recorder.state !== 'inactive') {
    recorder.stop();
  }
  stream.getTracks().forEach((track) => track.stop());
  const blob = await done;
  return {
    uri: URL.createObjectURL(blob),
    mimeType: blob.type || mimeType,
    fileName: mimeType.includes('mp4') ? 'voz.m4a' : 'voz.webm',
    blob,
  };
}

type NativeRecorder = {
  prepareToRecordAsync: () => Promise<void>;
  record: () => void;
  stop: () => Promise<void>;
  uri: string | null;
};

export async function recordNative(): Promise<VoiceClip> {
  const audio = await import('expo-audio');
  const permission = await audio.requestRecordingPermissionsAsync();
  if (!permission.granted) {
    throw new Error('Precisamos do microfone para gravar o áudio.');
  }
  const ctor = (
    audio as unknown as {
      AudioRecorder?: new (preset: unknown) => NativeRecorder;
    }
  ).AudioRecorder;
  if (!ctor) {
    throw new Error('Este aparelho não grava áudio aqui.');
  }
  const recorder = new ctor(audio.RecordingPresets.HIGH_QUALITY);
  await recorder.prepareToRecordAsync();
  recorder.record();
  await new Promise((resolve) => setTimeout(resolve, 2500));
  await recorder.stop();
  const uri = recorder.uri;
  if (!uri) {
    throw new Error('Não foi possível gravar o áudio.');
  }
  return { uri, mimeType: 'audio/m4a', fileName: 'voz.m4a' };
}

export function clipFromFile(file: File): VoiceClip {
  return {
    uri: URL.createObjectURL(file),
    mimeType: file.type || 'application/octet-stream',
    fileName: file.name || 'voz.webm',
    blob: file,
  };
}
