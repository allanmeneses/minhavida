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

export class TwoFactorRequiredError extends Error {
  constructor(readonly twoFactorToken: string) {
    super('Informe o codigo do autenticador.');
  }
}

type Options = RequestInit & { token?: string | null; skipRefresh?: boolean };

let refreshAccess: (() => Promise<string | null>) | null = null;
let readCacheHandler: ((path: string) => unknown | undefined) | null = null;
let writeCacheHandler: ((path: string, value: unknown) => void) | null = null;

export function setRefreshAccess(handler: (() => Promise<string | null>) | null): void {
  refreshAccess = handler;
}

export function setReadCache(handler: ((path: string) => unknown | undefined) | null): void {
  readCacheHandler = handler;
}

export function setWriteCache(handler: ((path: string, value: unknown) => void) | null): void {
  writeCacheHandler = handler;
}

export type AuthUser = { id: string; email: string; name: string; isSystemAdmin?: boolean };
export type AuthResponse = {
  token: string | null;
  refreshToken: string | null;
  user: AuthUser | null;
  requiresTwoFactor: boolean;
  twoFactorToken: string | null;
};
export type TwoFactorSetup = {
  secret: string;
  otpauthUri: string;
  qrPngBase64: string;
  recoveryCodes: string[];
};
export type Profile = {
  displayName: string;
  birthDate: string | null;
  phone: string | null;
  secondaryEmail: string | null;
  isPublic: boolean;
  hasPhoto: boolean;
  bio: string | null;
  findableByLocation: boolean;
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
  tags: string[];
  circleIds: string[];
};
export type CircleSummary = {
  id: string;
  name: string | null;
  description: string | null;
  visibility: string;
  discoverable: boolean;
  findableByLocation: boolean;
  isSurprise: boolean;
  revealed: boolean;
  kind: string;
  status: string;
  role: string;
  memberCount: number;
  endsAt?: string | null;
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
  findableByLocation: boolean;
  isSurprise: boolean;
  revealed: boolean;
  kind: string;
  status: string;
  ownerUserId: string | null;
  members: { userId: string; name: string; role: string }[];
  posts: { id: string; body: string; createdAt: string }[];
  log: { id: string; action: string; detail: string; createdAt: string; actorUserId: string | null }[];
  joinRequests: CircleJoinRequest[];
  sharedEntries: SharedLifeEntry[];
  endsAt?: string | null;
};
export type SharedLifeEntry = {
  id: string;
  lifeAreaId: string;
  title: string;
  body: string | null;
  numberValue: number | null;
  occurredOn: string | null;
  location: string | null;
  hasPhoto: boolean;
  tags: string[];
  authorUserId: string;
  authorName: string;
  createdAt: string;
  updatedAt: string;
};
export type SuccessionVote = {
  id: string;
  status: string;
  openedAt: string;
  deadlineAt: string;
  winnerUserId: string | null;
  memberCount: number;
  tallies: { candidateUserId: string; votes: number }[];
  myVote: string | null;
};
export type NearbyHit = {
  kind: string;
  id: string;
  name: string;
  description: string | null;
  distanceKm: number;
};
export type PlanOffer = {
  id: string;
  name: string;
  priceReais: number;
  circleLimit: number | null;
  storageLimitBytes: number;
  shortcutLimit: number;
  summary: string;
};
export type PlanCatalog = {
  status: string;
  free: PlanOffer;
  paidMonthly: PlanOffer;
  paidYearly: PlanOffer;
  sandbox: boolean;
  provider: string;
};
export type AdminStats = {
  shortcutsTriggered: number;
  entriesCreated: number;
  activeCircles: number;
};
export type AdminReport = {
  id: string;
  reporterUserId: string;
  reporterEmail: string;
  targetKind: string;
  targetId: string;
  note: string | null;
  createdAt: string;
};
export type AdminActionItem = {
  id: string;
  adminUserId: string;
  action: string;
  targetKind: string;
  targetId: string;
  detail: string;
  createdAt: string;
};
export type RecapFlow = { key: string; label: string; count: number };
export type ProfileRecap = { flows: RecapFlow[] };
export type CircleRecap = {
  circleId: string;
  name: string;
  kind: string;
  status: string;
  displayName: string;
  flows: RecapFlow[];
  myTitles: string[];
};
export type RecapShareCreated = { token: string; path: string };
export type PublicProfile = { userId: string; displayName: string; hasPhoto: boolean; description: string | null };
export type MemoryItem = {
  id: string;
  authorUserId: string;
  circleId: string | null;
  sourceCircleId: string | null;
  title: string;
  body: string;
  occurredOn: string | null;
  hasPhoto: boolean;
  createdAt: string;
  updatedAt: string;
};
export type RevealSummary = {
  circleId: string;
  name: string;
  description: string;
  revealedAt: string | null;
  messages: { postId: string; authorUserId: string | null; body: string; hasPhoto: boolean; createdAt: string }[];
  photoCount: number;
};
export type TemplateItem = {
  id: string;
  kind: string;
  name: string;
  description: string;
  useCount: number;
  rank: number;
  curated: boolean;
};
export type CalendarEvent = {
  reminderId: string;
  title: string;
  kind: string;
  at: string;
  paused: boolean;
  circleId: string | null;
};
export type CalendarMonth = {
  from: string;
  to: string;
  events: CalendarEvent[];
  feedUrl: string | null;
};
export type LivingProfileItem = {
  id: string;
  kind: string;
  name: string;
  context: string | null;
  birthDate: string | null;
  circleId: string | null;
  hasPhoto: boolean;
  canEdit: boolean;
  grants: { userId: string; name: string; access: string }[] | null;
};
export type ReminderItem = {
  id: string;
  title: string;
  kind: string;
  circleId: string | null;
  nextFireAt: string;
  isPaused: boolean;
  personName: string | null;
  advanceDays: number;
  giftIdeas: string[];
  endingSoon: boolean;
  recurrence: { frequency: string; interval: number };
};
export type ShortcutItem = {
  id: string;
  label: string;
  color: string;
  sortOrder: number;
  isActive: boolean;
  reminderId: string | null;
  actionKind: string;
  isDeleted: boolean;
  sharedByUserId: string | null;
  sharedByName: string | null;
  circleId: string | null;
};
export type ShortcutNotice = {
  id: string;
  shortcutId: string;
  detail: string;
  createdAt: string;
};
export type SearchHit = { type: string; id: string; title: string; detail: string | null };
export type SearchResponse = {
  hits: SearchHit[];
  history: { recent: string[]; frequent: string[] };
};
export type SharedListSummary = {
  id: string;
  name: string;
  kind: string;
  circleId: string | null;
  memberAccess: string;
  itemCount: number;
  doneCount: number;
};
export type SharedListItem = {
  id: string;
  text: string;
  quantity: string | null;
  note: string | null;
  isDone: boolean;
  sortOrder: number;
  addedByUserId: string | null;
  addedByName: string | null;
  doneByUserId: string | null;
  doneByName: string | null;
};
export type SharedListMemberAccess = {
  userId: string;
  name: string;
  access: string;
  overridden: boolean;
};
export type SharedListDetail = {
  id: string;
  name: string;
  kind: string;
  circleId: string | null;
  memberAccess: string;
  myAccess: string;
  canAdd: boolean;
  canComplete: boolean;
  canManageItems: boolean;
  canChangeAccess: boolean;
  members: SharedListMemberAccess[];
  items: SharedListItem[];
};
export type PlanInfo = {
  plan: string;
  circleLimit: number | null;
  circleCount: number;
  storageLimitBytes: number;
  storageUsedBytes: number;
  storageWarning: boolean;
  checkout: string;
  subscriptionStatus: string;
  inGoodStanding: boolean;
  shortcutLimit: number;
  sandbox: boolean;
  provider: string;
  subscriptionProviderId: string | null;
};
export type OnThisDayItem = {
  kind: string;
  id: string;
  title: string;
  year: number;
  occurredOn: string | null;
  hasPhoto: boolean;
};
export type MonthlySurprise = {
  year: number;
  month: number;
  memoryCount: number;
  entryCount: number;
  titles: string[];
};
export type OnboardingExample = {
  id: string;
  circleId: string | null;
  circleName: string | null;
  kind: string;
  title: string;
  body: string;
  pending: boolean;
};
export type PrivacyItem = { what: string; who: string };
export type DayPanel = {
  overdue: { id: string; title: string; kind: string; at: string; overdue: boolean }[];
  dueToday: { id: string; title: string; kind: string; at: string; overdue: boolean }[];
  upcoming: { id: string; title: string; kind: string; at: string; overdue: boolean }[];
  activeCircleCount: number;
  shortcuts: ShortcutItem[];
  vacationUntil: string | null;
  plan: PlanInfo;
  shortcutNotices: ShortcutNotice[];
  reminderEmailsSent: string[];
  emailRemindersEnabled: boolean;
  onThisDay: OnThisDayItem[];
  monthlySurprise: MonthlySurprise | null;
  routineStreakDays: number;
  onboarding: OnboardingExample[];
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
  const method = (options.method ?? 'GET').toUpperCase();
  const headerInit: Record<string, string> = {};
  if (options.token) {
    headerInit.Authorization = `Bearer ${options.token}`;
  }
  if (Platform.OS === 'web') {
    headerInit['X-Client'] = 'web';
  }
  if (options.body && !isFormDataBody(options.body) && !headerInit['Content-Type']) {
    headerInit['Content-Type'] = 'application/json';
  }

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      ...options,
      headers: headerInit,
      credentials: Platform.OS === 'web' ? 'include' : options.credentials,
    });
  } catch {
    if (method === 'GET') {
      const cached = readCacheHandler?.(path);
      if (cached !== undefined) {
        return cached as T;
      }
    }
    throw new ApiError('Sem conexao.', 0);
  }

  if (response.status === 401 && !options.skipRefresh && refreshAccess) {
    const next = await refreshAccess();
    if (next) {
      return api<T>(path, { ...options, token: next, skipRefresh: true });
    }
  }

  if (!response.ok) {
    if (method === 'GET' && response.status >= 500) {
      const cached = readCacheHandler?.(path);
      if (cached !== undefined) {
        return cached as T;
      }
    }
    throw new ApiError(await readError(response), response.status);
  }
  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const parsed = text ? (JSON.parse(text) as T) : (undefined as T);
  if (method === 'GET' && parsed !== undefined) {
    writeCacheHandler?.(path, parsed);
  }
  return parsed;
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
  if (status === 0) {
    return 'Sem conexao.';
  }
  return 'Nao foi possivel concluir o pedido.';
}

export async function downloadNamedBlob(path: string, fileName: string, token: string): Promise<void> {
  const response = await fetch(`${API_URL}${path}`, {
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
  const result = await FileSystem.downloadAsync(`${API_URL}${path}`, target, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (await Sharing.isAvailableAsync()) {
    await Sharing.shareAsync(result.uri, { dialogTitle: fileName, mimeType: response.headers.get('Content-Type') ?? undefined });
  }
}
