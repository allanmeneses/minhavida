import { Platform } from 'react-native';
import { API_URL } from '@/lib/config';

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

type Options = RequestInit & { token?: string | null };

export type AuthUser = { id: string; email: string; name: string };
export type AuthResponse = { token: string; user: AuthUser };
export type Profile = {
  displayName: string;
  birthDate: string | null;
  phone: string | null;
  secondaryEmail: string | null;
  hasPhoto: boolean;
};
export type DocumentItem = {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
};

export async function api<T>(path: string, options: Options = {}): Promise<T> {
  const headers = new Headers(options.headers);
  if (options.token) {
    headers.set('Authorization', `Bearer ${options.token}`);
  }
  if (options.body && !(options.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(`${API_URL}${path}`, { ...options, headers });
  if (!response.ok) {
    throw new ApiError(await readError(response), response.status);
  }
  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  return text ? (JSON.parse(text) as T) : (undefined as T);
}

export async function apiBlob(path: string, token: string): Promise<Blob> {
  const response = await fetch(`${API_URL}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) {
    throw new ApiError(await readError(response), response.status);
  }
  return response.blob();
}

export async function downloadDocument(id: string, fileName: string, token: string): Promise<void> {
  const response = await fetch(`${API_URL}/api/documents/${id}/download`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) {
    throw new ApiError(await readError(response), response.status);
  }

  if (Platform.OS === 'web') {
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
    return;
  }

  const FileSystem = await import('expo-file-system/legacy');
  const Sharing = await import('expo-sharing');
  const target = `${FileSystem.cacheDirectory ?? FileSystem.documentDirectory}${fileName}`;
  const result = await FileSystem.downloadAsync(`${API_URL}/api/documents/${id}/download`, target, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (await Sharing.isAvailableAsync()) {
    await Sharing.shareAsync(result.uri, { dialogTitle: fileName, mimeType: response.headers.get('Content-Type') ?? undefined });
  }
}

async function readError(response: Response): Promise<string> {
  try {
    const data = (await response.json()) as {
      error?: string;
      title?: string;
      errors?: Record<string, string[]>;
    };
    if (data.error) {
      return data.error;
    }
    if (data.errors) {
      return Object.values(data.errors).flat().join(' ');
    }
    if (data.title) {
      return data.title;
    }
  } catch {
    // corpo nao JSON
  }

  if (response.status === 401) {
    return 'Entre novamente para continuar.';
  }
  return 'Nao foi possivel concluir o pedido.';
}
