import { hasTestRecorder } from './voiceUi.ts';

export type VoiceClip = {
  uri: string;
  mimeType: string;
  fileName: string;
  blob?: Blob;
};

export type LiveRecording = {
  elapsedSeconds: () => number;
  stop: () => Promise<VoiceClip>;
  cancel: () => void;
};

export function canUseMediaRecorder(): boolean {
  return typeof MediaRecorder !== 'undefined' && typeof navigator !== 'undefined' && Boolean(navigator.mediaDevices?.getUserMedia);
}

export function startTestRecording(): LiveRecording {
  const started = Date.now();
  return {
    elapsedSeconds: () => Math.floor((Date.now() - started) / 1000),
    stop: async () => {
      const text = 'Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro';
      const blob = new Blob([text], { type: 'text/plain' });
      return {
        uri: 'test://voz.txt',
        mimeType: 'text/plain',
        fileName: 'teste-voz.txt',
        blob,
      };
    },
    cancel: () => undefined,
  };
}

export async function startWebRecording(): Promise<LiveRecording> {
  if (hasTestRecorder()) {
    return startTestRecording();
  }
  if (!canUseMediaRecorder()) {
    throw new Error('Este aparelho não grava áudio aqui.');
  }
  const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
  const mimeType = MediaRecorder.isTypeSupported('audio/webm') ? 'audio/webm' : 'audio/mp4';
  const recorder = new MediaRecorder(stream, { mimeType });
  const chunks: BlobPart[] = [];
  let settle: (blob: Blob) => void = () => undefined;
  let fail: (error: Error) => void = () => undefined;
  const done = new Promise<Blob>((resolve, reject) => {
    settle = resolve;
    fail = reject;
  });
  recorder.ondataavailable = (event) => {
    if (event.data.size > 0) {
      chunks.push(event.data);
    }
  };
  recorder.onerror = () => fail(new Error('Não foi possível gravar o áudio.'));
  recorder.onstop = () => settle(new Blob(chunks, { type: mimeType }));
  recorder.start();
  const started = Date.now();
  const stopTracks = () => stream.getTracks().forEach((track) => track.stop());
  return {
    elapsedSeconds: () => Math.floor((Date.now() - started) / 1000),
    stop: async () => {
      if (recorder.state !== 'inactive') {
        recorder.stop();
      }
      stopTracks();
      const blob = await done;
      return {
        uri: URL.createObjectURL(blob),
        mimeType: blob.type || mimeType,
        fileName: mimeType.includes('mp4') ? 'voz.m4a' : 'voz.webm',
        blob,
      };
    },
    cancel: () => {
      if (recorder.state !== 'inactive') {
        recorder.stop();
      }
      stopTracks();
    },
  };
}

export async function recordWithMediaRecorder(seconds = 4): Promise<VoiceClip> {
  const session = await startWebRecording();
  await new Promise((resolve) => setTimeout(resolve, Math.max(800, seconds * 1000)));
  return session.stop();
}

type NativeRecorder = {
  prepareToRecordAsync: () => Promise<void>;
  record: () => void;
  stop: () => Promise<void>;
  uri: string | null;
};

export async function startNativeRecording(): Promise<LiveRecording> {
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
  const started = Date.now();
  return {
    elapsedSeconds: () => Math.floor((Date.now() - started) / 1000),
    stop: async () => {
      await recorder.stop();
      const uri = recorder.uri;
      if (!uri) {
        throw new Error('Não foi possível gravar o áudio.');
      }
      return { uri, mimeType: 'audio/m4a', fileName: 'voz.m4a' };
    },
    cancel: () => {
      void recorder.stop();
    },
  };
}

export async function recordNative(): Promise<VoiceClip> {
  const session = await startNativeRecording();
  await new Promise((resolve) => setTimeout(resolve, 2500));
  return session.stop();
}

export function clipFromFile(file: File): VoiceClip {
  return {
    uri: URL.createObjectURL(file),
    mimeType: file.type || 'application/octet-stream',
    fileName: file.name || 'voz.webm',
    blob: file,
  };
}
