import { Platform, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';
import {
  chartBars,
  chartLine,
  chartScale,
  formatChartNumber,
  MAX_BAR_WIDTH,
  SINGLE_POINT_HINT,
  type ChartPoint,
} from '@/lib/chartLayout';

type Props = {
  title: string;
  points: ChartPoint[];
};

export function NumberChart({ title, points }: Props) {
  const { colors } = useTheme();
  const bars = chartBars(points, 320, points.length === 1 ? 72 : 140);
  const line = chartLine(points);
  const scale = points.length > 0 ? chartScale(points) : null;
  const single = points.length === 1 ? points[0] : null;
  const singleDate = bars[0]?.label && bars[0].label.includes('/') ? bars[0].label : null;

  return (
    <View testID="number-chart" style={[styles.card, { backgroundColor: colors.surface, borderColor: colors.line }]}>
      <Text style={[styles.title, { color: colors.ink }]}>{title}</Text>
      {points.length === 0 ? <Text style={{ color: colors.muted }}>Ainda não há números neste tipo.</Text> : null}
      {single ? (
        <Text testID="chart-single-value" style={[styles.value, { color: colors.ink }]}>
          {formatChartNumber(single.numberValue)}
          {singleDate ? ` em ${singleDate}` : ''}
        </Text>
      ) : null}
      {single ? (
        <Text testID="chart-single-hint" style={{ color: colors.muted }}>
          {SINGLE_POINT_HINT}
        </Text>
      ) : null}
      {Platform.OS === 'web' && points.length >= 2 && line.points ? (
        <View>
          {scale ? (
            <Text testID="chart-axis" style={[styles.barLabel, { color: colors.muted }]}>
              Eixo {formatChartNumber(scale.min)} a {formatChartNumber(scale.max)}
            </Text>
          ) : null}
          <svg width={line.width} height={line.height} viewBox={`0 0 ${line.width} ${line.height}`} role="img" aria-label={title}>
            <polyline
              fill="none"
              stroke={colors.primary}
              strokeWidth="3"
              points={line.points}
            />
          </svg>
          <View style={styles.axis}>
            {bars.map((bar) => (
              <Text key={`${bar.label}-${bar.x}`} style={[styles.barLabel, { color: colors.muted }]}>
                {bar.valueLabel} · {bar.label}
              </Text>
            ))}
          </View>
        </View>
      ) : (
        <View style={[styles.bars, points.length === 1 ? styles.barsSingle : null]}>
          {bars.map((bar) => (
            <View key={`${bar.label}-${bar.x}`} style={[styles.barCol, { width: bar.width, maxWidth: MAX_BAR_WIDTH }]}>
              <Text style={[styles.barValue, { color: colors.ink }]}>{bar.valueLabel}</Text>
              <View
                testID="chart-bar"
                accessibilityLabel={`Valor ${bar.value} em ${bar.label}`}
                style={[
                  styles.bar,
                  {
                    height: bar.height,
                    width: bar.width,
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
  value: {
    fontWeight: '800',
    fontSize: 22,
  },
  bars: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: 12,
    minHeight: 140,
    paddingBottom: 4,
  },
  barsSingle: {
    justifyContent: 'flex-start',
    minHeight: 88,
  },
  barCol: {
    alignItems: 'center',
    justifyContent: 'flex-end',
    gap: 4,
  },
  bar: {
    borderRadius: 4,
    minHeight: 8,
  },
  barValue: {
    fontSize: 12,
    fontWeight: '700',
  },
  barLabel: {
    fontSize: 11,
    textAlign: 'center',
  },
  axis: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: 8,
  },
});
