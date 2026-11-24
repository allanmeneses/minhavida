import { Platform, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';
import { chartBars, chartLine, type ChartPoint } from '@/lib/chartLayout';

type Props = {
  title: string;
  points: ChartPoint[];
};

export function NumberChart({ title, points }: Props) {
  const { colors } = useTheme();
  const bars = chartBars(points);
  const line = chartLine(points);

  return (
    <View testID="number-chart" style={[styles.card, { backgroundColor: colors.surface, borderColor: colors.line }]}>
      <Text style={[styles.title, { color: colors.ink }]}>{title}</Text>
      {points.length === 0 ? <Text style={{ color: colors.muted }}>Ainda não há números neste tipo.</Text> : null}
      {Platform.OS === 'web' && points.length >= 2 && line.points ? (
        <svg width={line.width} height={line.height} viewBox={`0 0 ${line.width} ${line.height}`} role="img" aria-label={title}>
          <polyline
            fill="none"
            stroke={colors.primary}
            strokeWidth="3"
            points={line.points}
          />
        </svg>
      ) : (
        <View style={styles.bars}>
          {bars.map((bar) => (
            <View key={`${bar.label}-${bar.x}`} style={styles.barCol}>
              <View
                testID="chart-bar"
                accessibilityLabel={`Valor ${bar.value} no dia ${bar.label}`}
                style={[
                  styles.bar,
                  {
                    height: bar.height,
                    backgroundColor: colors.primary,
                  },
                ]}
              />
              <Text style={[styles.barLabel, { color: colors.muted }]}>{bar.label}</Text>
            </View>
          ))}
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    borderWidth: 1,
    borderRadius: 12,
    padding: 12,
    gap: 10,
  },
  title: {
    fontWeight: '800',
    fontSize: 16,
  },
  bars: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: 6,
    minHeight: 140,
  },
  barCol: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'flex-end',
    gap: 4,
  },
  bar: {
    width: '80%',
    minWidth: 16,
    borderRadius: 4,
    minHeight: 8,
  },
  barLabel: {
    fontSize: 11,
  },
});
