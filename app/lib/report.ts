import { api } from '@/lib/api';

export async function reportTarget(
  token: string,
  targetKind: 'profile' | 'circle' | 'post',
  targetId: string,
): Promise<void> {
  await api('/api/reports', {
    method: 'POST',
    token,
    body: JSON.stringify({ targetKind, targetId }),
  });
}
