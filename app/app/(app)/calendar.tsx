import { useCallback, useEffect, useMemo, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, downloadNamedBlob, type CalendarMonth } from '@/lib/api';
import { buildMonth, shiftMonth } from '@/lib/calendarMonth';
import { deviceCalendarPolicy, listDeviceEvents, type DeviceCalendarEvent } from '@/lib/deviceCalendar';
import { screenStyles } from '@/lib/screenStyles';

export default function CalendarScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const now = new Date();
  const [year, setYear] = useState(now.getUTCFullYear());
  const [month, setMonth] = useState(now.getUTCMonth() + 1);
  const [data, setData] = useState<CalendarMonth | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [deviceEvents, setDeviceEvents] = useState<DeviceCalendarEvent[]>([]);

  const from = `${year}-${String(month).padStart(2, '0')}-01`;
  const toDate = new Date(Date.UTC(year, month, 0));
  const to = `${year}-${String(month).padStart(2, '0')}-${String(toDate.getUTCDate()).padStart(2, '0')}`;

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setData(await api<CalendarMonth>(`/api/calendar?from=${from}&to=${to}`, { token }));
    setDeviceEvents(await listDeviceEvents());
  }, [token, from, to]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao abrir o calendario.'));
  }, [load]);

  const days = useMemo(() => buildMonth(year, month, data?.events ?? []), [year, month, data]);

  function move(delta: number) {
    const next = shiftMonth(year, month, delta);
    setYear(next.year);
    setMonth(next.month);
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Calendario</Text>
      <Text style={styles.lead}>
        Lembretes do app neste mes. Exporta .ics para baixar ou assinar. Calendario do aparelho e somente leitura: o app
        le e nunca grava evento la.
      </Text>
      {deviceCalendarPolicy.writes ? null : (
        <Text style={styles.lead}>
          No celular o app le o calendario do aparelho. Nesta tela web nao ha eventos do aparelho.
        </Text>
      )}
      {deviceEvents.map((event) => (
        <Text key={event.id} style={styles.lead}>
          Aparelho: {event.title}
        </Text>
      ))}
      <Feedback message={error} />
      <Button label="Mes anterior" variant="ghost" onPress={() => move(-1)} />
      <Text style={styles.section}>
        {String(month).padStart(2, '0')}/{year}
      </Text>
      <Button label="Proximo mes" variant="ghost" onPress={() => move(1)} />
      {days
        .filter((day) => day.inMonth)
        .map((day) => (
          <View key={day.date} style={styles.card}>
            <Text style={styles.item}>{day.date}</Text>
            {day.events.length === 0 ? <Text style={styles.lead}>Sem lembrete</Text> : null}
            {day.events.map((event) => (
              <Text key={event.id} style={styles.lead}>
                {event.title}
                {event.paused ? ' (pausado)' : ''}
              </Text>
            ))}
          </View>
        ))}
      {token ? (
        <>
          <Button
            label="Baixar .ics"
            onPress={() => void downloadNamedBlob(`/api/calendar/ics?from=${from}&to=${to}`, 'circulos-lembretes.ics', token)}
          />
          {data?.feedUrl ? <Text style={styles.lead}>Assinatura: {data.feedUrl}</Text> : null}
        </>
      ) : null}
    </ScrollView>
  );
}
