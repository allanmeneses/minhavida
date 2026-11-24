import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';
import type { DayCell } from '@/lib/calendarMonth';
import { WEEKDAY_LABELS } from '@/lib/dates';

type Props = {
  title: string;
  days: DayCell[];
  selectedDate?: string | null;
  onSelectDate?: (date: string) => void;
};

export function MonthGrid({ title, days, selectedDate, onSelectDate }: Props) {
  const { colors } = useTheme();
  const padded = [...days];
  while (padded.length % 7 !== 0) {
    padded.push({ date: '', inMonth: false, events: [] });
  }

  return (
    <View testID="month-grid" style={[styles.card, { backgroundColor: colors.surface, borderColor: colors.line }]}>
      <Text style={[styles.title, { color: colors.ink }]}>{title}</Text>
      <View style={styles.week}>
        {WEEKDAY_LABELS.map((label, index) => (
          <Text key={`${label}-${index}`} style={[styles.weekday, { color: colors.muted }]}>
            {label}
          </Text>
        ))}
      </View>
      <View style={styles.grid}>
        {padded.map((day, index) => {
          const number = day.date ? Number(day.date.slice(8, 10)) : '';
          const selected = Boolean(day.date && selectedDate === day.date);
          const cell = (
            <View
              testID={day.inMonth ? `day-${day.date}` : undefined}
              style={[
                styles.cell,
                {
                  borderColor: selected ? colors.primary : colors.line,
                  backgroundColor: selected ? colors.primarySoft : day.inMonth ? colors.white : 'transparent',
                },
              ]}>
              <Text style={[styles.number, { color: day.inMonth ? colors.ink : colors.muted }]}>{number}</Text>
              {day.events.slice(0, 2).map((event) => (
                <Text key={event.id} numberOfLines={1} style={[styles.event, { color: colors.primaryDark }]}>
                  {event.title}
                </Text>
              ))}
              {day.events.length > 2 ? (
                <Text style={[styles.more, { color: colors.muted }]}>+{day.events.length - 2}</Text>
              ) : null}
            </View>
          );
          if (!onSelectDate || !day.inMonth || !day.date) {
            return (
              <View key={day.date || `empty-${index}`} style={styles.cellWrap}>
                {cell}
              </View>
            );
          }
          return (
            <Pressable
              key={day.date}
              accessibilityRole="button"
              accessibilityLabel={`Dia ${number}`}
              onPress={() => onSelectDate(day.date)}
              style={styles.cellWrap}>
              {cell}
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    borderWidth: 1,
    borderRadius: 12,
    padding: 12,
    gap: 8,
  },
  title: {
    fontWeight: '800',
    fontSize: 16,
  },
  week: {
    flexDirection: 'row',
  },
  weekday: {
    flex: 1,
    textAlign: 'center',
    fontWeight: '700',
    fontSize: 12,
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
  },
  cellWrap: {
    width: '14.285%',
  },
  cell: {
    minHeight: 64,
    borderWidth: 1,
    padding: 4,
    gap: 2,
  },
  number: {
    fontWeight: '700',
    fontSize: 12,
  },
  event: {
    fontSize: 10,
  },
  more: {
    fontSize: 10,
  },
});
