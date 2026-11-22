import { Platform } from 'react-native';
import { API_URL } from '@/lib/config';
import { ensureUploadFileName } from '@/lib/ensureUploadFileName';

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
  isPublic: boolean;
  hasPhoto: boolean;
};
export type LifeArea = {
  id: string;
  name: string;
  kind: string;
  sortOrder: number;
  suggested: boolean;
};
export type LifeEntry = {
  id: string;
  lifeAreaId: string;
  title: string;
  body: string | null;
  numberValue: number | null;
  occurredOn: string | null;
  location: string | null;
  hasPhoto: boolean;
  createdAt: string;
  updatedAt: string;
};
export type CircleSummary = {
  id: string;
  name: string | null;
  description: string | null;
  visibility: string;
  discoverable: boolean;
  isSurprise: boolean;
  revealed: boolean;
  kind: string;
  status: string;
  role: string;
  memberCount: number;
};
export type CircleJoinRequest = {
  id: string;
  userId: string;
  name: string;
  status: string;
  createdAt: string;
};
export type CircleDetail = {
  id: string;
  name: string | null;
  description: string | null;
  visibility: string;
  discoverable: boolean;
  ownerUserId: string | null;
  members: { userId: string; name: string; role: string }[];
  posts: { id: string; body: string; createdAt: string }[];
  log: { id: string; action: string; detail: string; createdAt: string; actorUserId: string | null }[];
  joinRequests: CircleJoinRequest[];
};
export type ReminderItem = {
  id: string;
  title: string;
  kind: string;
  nextFireAt: string;
  isPaused: boolean;
  personName: string | null;
  advanceDays: number;
  giftIdeas: string[];
  endingSoon: boolean;
};
export type ShortcutItem = {
  id: string;
  label: string;
  color: string;
  sortOrder: number;
  isActive: boolean;
};
export type PlanInfo = {
  plan: string;
  circleLimit: number | null;
  circleCount: number;
  storageLimitBytes: number;
  storageUsedBytes: number;
  storageWarning: boolean;
  checkout: string;
};
export type DayPanel = {
  overdue: { id: string; title: string; kind: string; at: string; overdue: boolean }[];
  dueToday: { id: string; title: string; kind: string; at: string; overdue: boolean }[];
  upcoming: { id: string; title: string; kind: string; at: string; overdue: boolean }[];
  activeCircleCount: number;
  shortcuts: ShortcutItem[];
  vacationUntil: string | null;
  plan: PlanInfo;
};
export type DocumentItem = {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
};

function isFormDataBody(body: unknown): boolean {
  if (typeof FormData !== 'undefined' && body instanceof FormData) {
    return true;
  }
  return typeof body === 'object' && body !== null && typeof (body as FormData).append === 'function';
}

export async function api<T>(path: string, options: Options = {}): Promise<T> {
  const headerInit: Record<string, string> = {};
  if (options.token) {
    headerInit.Authorization = `Bearer ${options.token}`;
  }
  if (options.body && !isFormDataBody(options.body) && !headerInit['Content-Type']) {
    headerInit['Content-Type'] = 'application/json';
  }

  const response = await fetch(`${API_URL}${path}`, { ...options, headers: headerInit });
  if (!response.ok) {
    throw new ApiError(await readError(response), response.status);
  }
  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  return text ? (JSON.parse(text) as T) : (undefined as T);
}

export type NativeUploadFile = {
  uri: string;
  name?: string | null;
  mimeType?: string | null;
};

export async function uploadNativeFile<T>(
  path: string,
  token: string,
  file: NativeUploadFile,
  fallbackName: string,
): Promise<T> {
  const fileName = ensureUploadFileName(file.name, file.mimeType, fallbackName);
  const mimeType = file.mimeType ?? 'application/octet-stream';
  const form = new FormData();
  form.append('file', {
    uri: file.uri,
    name: fileName,
    type: mimeType,
  } as unknown as Blob);

  const body = await new Promise<string>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', `${API_URL}${path}`);
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.onload = () => {
      if (xhr.status === 204) {
        resolve('');
        return;
      }
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(xhr.responseText);
        return;
      }
      reject(new ApiError(messageFromErrorBody(xhr.responseText, xhr.status), xhr.status));
    };
    xhr.onerror = () => reject(new Error('Nao foi possivel concluir o pedido.'));
    xhr.send(form);
  });

  return body ? (JSON.parse(body) as T) : (undefined as T);
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
    return messageFromErrorBody(await response.text(), response.status);
  } catch {
    return messageFromErrorBody('', response.status);
  }
}

function messageFromErrorBody(body: string, status: number): string {
  try {
    const data = JSON.parse(body) as {
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

  if (status === 401) {
    return 'Entre novamente para continuar.';
  }
  return 'Nao foi possivel concluir o pedido.';
}
