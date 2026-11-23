import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

const ACCESS_KEY = 'circulos.token';
const REFRESH_KEY = 'circulos.refresh';

async function read(key: string): Promise<string | null> {
  if (Platform.OS === 'web') {
    return globalThis.localStorage?.getItem(key) ?? null;
  }
  return SecureStore.getItemAsync(key);
}

async function write(key: string, value: string): Promise<void> {
  if (Platform.OS === 'web') {
    globalThis.localStorage?.setItem(key, value);
    return;
  }
  await SecureStore.setItemAsync(key, value);
}

async function remove(key: string): Promise<void> {
  if (Platform.OS === 'web') {
    globalThis.localStorage?.removeItem(key);
    return;
  }
  await SecureStore.deleteItemAsync(key);
}

export async function readToken(): Promise<string | null> {
  return read(ACCESS_KEY);
}

export async function writeToken(token: string): Promise<void> {
  await write(ACCESS_KEY, token);
}

export async function readRefreshToken(): Promise<string | null> {
  return read(REFRESH_KEY);
}

export async function writeRefreshToken(token: string): Promise<void> {
  await write(REFRESH_KEY, token);
}

export async function writeSession(access: string, refresh: string | null): Promise<void> {
  await writeToken(access);
  if (Platform.OS === 'web') {
    await remove(REFRESH_KEY);
    return;
  }
  if (refresh) {
    await writeRefreshToken(refresh);
  }
}

export async function clearToken(): Promise<void> {
  await remove(ACCESS_KEY);
  await remove(REFRESH_KEY);
}
