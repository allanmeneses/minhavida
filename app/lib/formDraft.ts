export function hydrateIfPristine<T>(dirty: boolean, current: T, incoming: T): T {
  return dirty ? current : incoming;
}
