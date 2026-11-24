export function countNoun(count: number, singular: string, plural: string): string {
  return `${count} ${count === 1 ? singular : plural}`;
}

export function noun(count: number, singular: string, plural: string): string {
  return count === 1 ? singular : plural;
}

export function daysPhrase(days: number): string {
  return countNoun(days, 'dia', 'dias');
}
