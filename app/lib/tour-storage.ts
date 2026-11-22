import { Platform } from 'react-native';

const KEY = 'circulos.tourDone';

export async function readTourDone(): Promise<boolean> {
  if (Platform.OS === 'web') {
    return globalThis.localStorage?.getItem(KEY) === '1';
  }
  const store = await import('expo-secure-store');
  return (await store.getItemAsync(KEY)) === '1';
}

export async function writeTourDone(done: boolean): Promise<void> {
  const value = done ? '1' : '0';
  if (Platform.OS === 'web') {
    globalThis.localStorage?.setItem(KEY, value);
    return;
  }
  const store = await import('expo-secure-store');
  await store.setItemAsync(KEY, value);
}
