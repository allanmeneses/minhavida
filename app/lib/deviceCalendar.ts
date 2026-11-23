export type DeviceCalendarEvent = {
  id: string;
  title: string;
  startDate: string;
};

export type DeviceCalendarBridge = {
  requestPermission: () => Promise<boolean>;
  listEvents: (from: Date, to: Date) => Promise<DeviceCalendarEvent[]>;
};

export const deviceCalendarPolicy = {
  mode: 'read-only' as const,
  writes: false,
};

const emptyBridge: DeviceCalendarBridge = {
  async requestPermission() {
    return false;
  },
  async listEvents() {
    return [];
  },
};

function skipNativeCalendar(): boolean {
  if (deviceCalendarPolicy.writes) {
    return true;
  }
  if (typeof process !== 'undefined' && process.versions?.node && process.env.EXPO_OS !== 'ios' && process.env.EXPO_OS !== 'android') {
    return true;
  }
  return false;
}

async function nativeBridge(): Promise<DeviceCalendarBridge> {
  if (skipNativeCalendar()) {
    return emptyBridge;
  }
  try {
    const Calendar = await import('expo-calendar');
    return {
      async requestPermission() {
        const current = await Calendar.getCalendarPermissionsAsync();
        const permission =
          current.status === 'granted' ? current : await Calendar.requestCalendarPermissionsAsync();
        return permission.status === 'granted';
      },
      async listEvents(from, to) {
        const calendars = await Calendar.getCalendarsAsync(Calendar.EntityTypes.EVENT);
        const ids = calendars.map((item: { id: string }) => item.id);
        if (ids.length === 0) {
          return [];
        }
        const events = await Calendar.getEventsAsync(ids, from, to);
        return events.map((event: { id: string; title?: string | null; startDate: string | Date }) => ({
          id: event.id,
          title: event.title ?? 'Sem titulo',
          startDate: typeof event.startDate === 'string' ? event.startDate : event.startDate.toISOString(),
        }));
      },
    };
  } catch {
    return emptyBridge;
  }
}

export async function listDeviceEvents(bridge?: DeviceCalendarBridge): Promise<DeviceCalendarEvent[]> {
  if (deviceCalendarPolicy.writes) {
    return [];
  }
  const impl = bridge ?? (await nativeBridge());
  try {
    const allowed = await impl.requestPermission();
    if (!allowed) {
      return [];
    }
    const now = new Date();
    const from = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
    const to = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1));
    return await impl.listEvents(from, to);
  } catch {
    return [];
  }
}
