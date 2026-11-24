export type PrivacyLine = {
  what: string;
  who: string;
};

export function privacyItemKey(item: PrivacyLine, index: number): string {
  return `${index}:${item.what}`;
}

export function uniquePrivacyItems(items: PrivacyLine[]): PrivacyLine[] {
  const seen = new Set<string>();
  const next: PrivacyLine[] = [];
  for (const item of items) {
    if (seen.has(item.what)) {
      continue;
    }
    seen.add(item.what);
    next.push(item);
  }
  return next;
}
