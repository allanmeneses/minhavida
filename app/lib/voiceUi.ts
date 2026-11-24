export type VoiceUiState = 'idle' | 'recording' | 'transcribing';

export function formatElapsed(seconds: number): string {
  const safe = Math.max(0, Math.floor(seconds));
  const mm = Math.floor(safe / 60);
  const ss = safe % 60;
  return `${String(mm).padStart(2, '0')}:${String(ss).padStart(2, '0')}`;
}

export function recordingLabel(seconds: number): string {
  return `Gravando ${formatElapsed(seconds)}`;
}

export function transcribingLabel(): string {
  return 'Transcrevendo...';
}

export function hasTestRecorder(): boolean {
  return typeof globalThis !== 'undefined' && Boolean((globalThis as { circulosTestRecorder?: boolean }).circulosTestRecorder);
}
