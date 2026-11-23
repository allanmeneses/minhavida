export type DeviceCalendarEvent = {
  id: string;
  title: string;
  startDate: string;
};

export const deviceCalendarPolicy = {
  mode: 'read-only' as const,
  writes: false,
};

export async function listDeviceEvents(): Promise<DeviceCalendarEvent[]> {
  return [];
}
