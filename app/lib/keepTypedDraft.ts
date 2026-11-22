export function keepTypedDraft(typed: string, incoming: string, lastSaved: string): string {
  if (typed !== lastSaved) {
    return typed;
  }

  return incoming;
}
