export type CircleReminderOption = {
  id: string;
  title: string;
  circleId: string | null;
};

export function circleRemindersForShortcut(reminders: CircleReminderOption[]): CircleReminderOption[] {
  return reminders.filter((item) => Boolean(item.circleId));
}

export function canCycleCircleReminders(reminders: CircleReminderOption[]): boolean {
  return circleRemindersForShortcut(reminders).length > 0;
}

export function nextCircleReminderId(
  reminders: CircleReminderOption[],
  currentId: string | null,
): string | null {
  const options = circleRemindersForShortcut(reminders);
  if (options.length === 0) {
    return null;
  }
  const index = options.findIndex((item) => item.id === currentId);
  const next = options[(index + 1) % (options.length + 1)];
  return next?.id ?? null;
}

export function circleReminderButtonLabel(
  reminders: CircleReminderOption[],
  reminderId: string | null,
): string {
  if (!reminderId) {
    return 'Sem lembrete de círculo';
  }
  const title = circleRemindersForShortcut(reminders).find((item) => item.id === reminderId)?.title;
  if (!title) {
    return 'Sem lembrete de círculo';
  }
  return `Lembrete: ${title}`;
}
