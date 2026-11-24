export type ChartPoint = { occurredOn: string | null; numberValue: number };

export type ChartBar = {
  label: string;
  value: number;
  height: number;
  x: number;
  width: number;
};

export type ChartLine = {
  points: string;
  width: number;
  height: number;
};

export function chartBars(points: ChartPoint[], width = 320, height = 140): ChartBar[] {
  if (points.length === 0) {
    return [];
  }

  const max = Math.max(...points.map((point) => Math.abs(point.numberValue)), 1);
  const gap = 6;
  const barWidth = Math.max(8, (width - gap * (points.length + 1)) / points.length);
  return points.map((point, index) => ({
    label: point.occurredOn ? point.occurredOn.slice(8, 10) : String(index + 1),
    value: point.numberValue,
    height: Math.max(4, (Math.abs(point.numberValue) / max) * (height - 24)),
    x: gap + index * (barWidth + gap),
    width: barWidth,
  }));
}

export function chartLine(points: ChartPoint[], width = 320, height = 140): ChartLine {
  if (points.length === 0) {
    return { points: '', width, height };
  }

  const max = Math.max(...points.map((point) => point.numberValue), 1);
  const min = Math.min(...points.map((point) => point.numberValue), 0);
  const span = max - min || 1;
  const step = points.length === 1 ? width / 2 : width / (points.length - 1);
  const coords = points.map((point, index) => {
    const x = points.length === 1 ? width / 2 : index * step;
    const y = height - ((point.numberValue - min) / span) * (height - 16) - 8;
    return `${x.toFixed(1)},${y.toFixed(1)}`;
  });
  return { points: coords.join(' '), width, height };
}
