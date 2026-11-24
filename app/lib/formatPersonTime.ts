import { formatPersonDateTime } from './dates.ts';

export function formatPersonTime(iso: string): string {
  return formatPersonDateTime(iso);
}
