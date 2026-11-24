import { useEffect, useState } from 'react';
import { Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { MonthGrid } from '@/components/MonthGrid';
import { Button } from '@/components/ui/Button';
import { useTheme } from '@/context/ThemeContext';
import { buildMonth, shiftMonth } from '@/lib/calendarMonth';
import { datePlaceholder, isoToDisplayDate, monthTitle, toIsoDate } from '@/lib/dates';

type Props = {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  placeholder?: string;
};

function yearMonthFrom(value: string): { year: number; month: number } {
  const iso = toIsoDate(value);
  const match = iso.match(/^(\d{4})-(\d{2})-\d{2}$/);
  if (match) {
    return { year: Number(match[1]), month: Number(match[2]) };
  }
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

export function DateField({ label, value, onChangeText, placeholder = datePlaceholder() }: Props) {
  const { colors } = useTheme();
  const iso = toIsoDate(value);
  const shown = /^\d{4}-\d{2}-\d{2}$/.test(iso) ? isoToDisplayDate(iso) : value;
  const [open, setOpen] = useState(false);
  const [focused, setFocused] = useState(false);
  const [draft, setDraft] = useState(shown);
  const initial = yearMonthFrom(value);
  const [year, setYear] = useState(initial.year);
  const [month, setMonth] = useState(initial.month);

  useEffect(() => {
    if (!focused) {
      setDraft(shown);
    }
  }, [shown, focused]);

  function move(delta: number) {
    const next = shiftMonth(year, month, delta);
    setYear(next.year);
    setMonth(next.month);
  }

  function apply(text: string) {
    setDraft(text);
    const next = toIsoDate(text);
    if (/^\d{4}-\d{2}-\d{2}$/.test(next) || text.trim() === '') {
      onChangeText(next);
    }
  }

  const picker = open ? (
    <View>
      <Button label="Mês anterior" variant="ghost" onPress={() => move(-1)} />
      <MonthGrid
        title={monthTitle(year, month)}
        days={buildMonth(year, month, [])}
        selectedDate={/^\d{4}-\d{2}-\d{2}$/.test(iso) ? iso : null}
        onSelectDate={(date) => {
          onChangeText(date);
          setOpen(false);
        }}
      />
      <Button label="Próximo mês" variant="ghost" onPress={() => move(1)} />
    </View>
  ) : null;

  return (
    <View style={styles.wrap}>
      <Text style={[styles.label, { color: colors.ink }]}>{label}</Text>
      <View style={styles.row}>
        <TextInput
          value={focused ? draft : shown}
          onChangeText={apply}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          placeholder={placeholder}
          placeholderTextColor={colors.placeholder}
          autoCapitalize="none"
          autoCorrect={false}
          accessibilityLabel={label}
          style={[
            styles.input,
            {
              borderColor: colors.line,
              backgroundColor: colors.white,
              color: colors.ink,
            },
          ]}
        />
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Abrir calendário"
          onPress={() => {
            const next = yearMonthFrom(value);
            setYear(next.year);
            setMonth(next.month);
            setOpen((current) => !current);
          }}
          style={[styles.calendarButton, { borderColor: colors.line, backgroundColor: colors.white }]}>
          <Text style={{ color: colors.ink, fontWeight: '700' }}>Calendário</Text>
        </Pressable>
      </View>
      {picker}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    gap: 6,
  },
  label: {
    fontSize: 13,
    fontWeight: '600',
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  input: {
    flex: 1,
    minHeight: Platform.OS === 'web' ? 42 : 48,
    borderWidth: 1,
    borderRadius: Platform.OS === 'web' ? 8 : 14,
    paddingHorizontal: 14,
    fontSize: Platform.OS === 'web' ? 14 : 16,
  },
  calendarButton: {
    minHeight: Platform.OS === 'web' ? 42 : 48,
    borderWidth: 1,
    borderRadius: Platform.OS === 'web' ? 8 : 14,
    paddingHorizontal: 12,
    justifyContent: 'center',
  },
});
