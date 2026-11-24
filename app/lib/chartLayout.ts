import { isoToDisplayDate } from './dates.ts';

export const MAX_BAR_WIDTH = 40;
export const SINGLE_POINT_HINT = 'registre mais para ver a evolução';

export type ChartPoint = { occurredOn: string | null; numberValue: number };

export type ChartBar = {
  label: string;
  value: number;
  valueLabel: string;
  height: number;
  x: number;
  width: number;
};

export type ChartLine = {
  points: string;
  width: number;
  height: number;
};

export function formatChartNumber(value: number): string {
  if (Number.isInteger(value)) {
    return String(value);
  }
  const rounded = Math.round(value * 100) / 100;
  return String(rounded).replace('.', ',');
}

export function chartScale(points: ChartPoint[]): { min: number; max: number } {
  const values = points.map((point) => point.numberValue);
  const dataMin = Math.min(...values);
  const dataMax = Math.max(...values);
  if (dataMin === dataMax) {
    const pad = Math.max(Math.abs(dataMax) * 0.1, 1);
    return { min: dataMin - pad, max: dataMax + pad };
  }
  const pad = (dataMax - dataMin) * 0.08;
  return { min: dataMin - pad, max: dataMax + pad };
}

export function chartBars(points: ChartPoint[], width = 320, height = 140): ChartBar[] {
  if (points.length === 0) {
    return [];
  }

  const { min, max } = chartScale(points);
  const span = max - min || 1;
  const gap = 12;
  const raw = (width - gap * (points.length + 1)) / points.length;
  const barWidth = Math.min(MAX_BAR_WIDTH, Math.max(8, raw));
  return points.map((point, index) => ({
    label: point.occurredOn ? isoToDisplayDate(String(point.occurredOn)) : String(index + 1),
    value: point.numberValue,
    valueLabel: formatChartNumber(point.numberValue),
    height: Math.max(8, ((point.numberValue - min) / span) * (height - 36)),
    x: gap + index * (barWidth + gap),
    width: barWidth,
  }));
}

export function chartLine(points: ChartPoint[], width = 320, height = 140): ChartLine {
  if (points.length === 0) {
    return { points: '', width, height };
  }

  const { min, max } = chartScale(points);
  const span = max - min || 1;
  const step = points.length === 1 ? width / 2 : width / (points.length - 1);
  const coords = points.map((point, index) => {
    const x = points.length === 1 ? width / 2 : index * step;
    const y = height - ((point.numberValue - min) / span) * (height - 16) - 8;
    return `${x.toFixed(1)},${y.toFixed(1)}`;
  });
  return { points: coords.join(' '), width, height };
}
