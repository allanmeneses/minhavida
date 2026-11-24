import { useCallback, useEffect, useMemo, useState } from 'react';
import { Linking, ScrollView, Text, View } from 'react-native';
import { MonthGrid } from '@/components/MonthGrid';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, downloadNamedBlob, type CalendarMonth } from '@/lib/api';
import { API_URL } from '@/lib/config';
import { calendarFeedUrl, copyText } from '@/lib/copyText';
import { buildMonth, shiftMonth } from '@/lib/calendarMonth';
import { monthTitle } from '@/lib/dates';
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
  const [notice, setNotice] = useState<string | null>(null);
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
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao abrir o calendário.'));
  }, [load]);

  const days = useMemo(() => buildMonth(year, month, data?.events ?? []), [year, month, data]);
  const feed = data?.feedUrl ? calendarFeedUrl(API_URL, data.feedUrl) : null;

  function move(delta: number) {
    const next = shiftMonth(year, month, delta);
    setYear(next.year);
    setMonth(next.month);
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Calendário</Text>
      <Text style={styles.lead}>
        Lembretes do app neste mês. Exporte o arquivo ou assine no seu calendário. O calendário do aparelho é somente leitura: o app lê e nunca grava evento lá.
      </Text>
      {deviceCalendarPolicy.writes ? null : (
        <Text style={styles.lead}>
          No celular o app lê o calendário do aparelho. Nesta tela web não há eventos do aparelho.
        </Text>
      )}
      {deviceEvents.map((event) => (
        <Text key={event.id} style={styles.lead}>
          Aparelho: {event.title}
        </Text>
      ))}
      <Feedback message={error} />
      <Feedback message={notice} tone="ok" />
      <Button label="Mês anterior" variant="ghost" onPress={() => move(-1)} />
      <MonthGrid title={monthTitle(year, month)} days={days} />
      <Button label="Próximo mês" variant="ghost" onPress={() => move(1)} />
      {token ? (
        <>
          <Button
            label="Baixar arquivo de calendário"
            onPress={() => void downloadNamedBlob(`/api/calendar/ics?from=${from}&to=${to}`, 'circulos-lembretes.ics', token)}
          />
          {feed ? (
            <View>
              <Button label="Assinar no seu calendário" onPress={() => void Linking.openURL(feed)} />
              <Button
                label="Copiar endereço"
                variant="ghost"
                onPress={() => {
                  void copyText(feed).then((ok) => setNotice(ok ? 'Endereço copiado.' : 'Não foi possível copiar.'));
                }}
              />
            </View>
          ) : null}
        </>
      ) : null}
    </ScrollView>
  );
}
