import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';
import { shareChipLabel, toggleCircleShare } from '@/lib/circleShare';

export type ShareCircle = {
  id: string;
  name?: string | null;
};

type Props = {
  circles: ShareCircle[];
  selectedIds: string[];
  onChange: (ids: string[]) => void;
};

export function CircleShareChips({ circles, selectedIds, onChange }: Props) {
  const { colors } = useTheme();
  if (circles.length === 0) {
    return null;
  }

  return (
    <View style={styles.wrap}>
      <Text style={[styles.lead, { color: colors.muted }]}>Compartilhar com</Text>
      <View style={styles.row}>
        {circles.map((circle) => {
          const name = circle.name?.trim() || 'círculo';
          const selected = selectedIds.includes(circle.id);
          return (
            <Pressable
              key={circle.id}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              accessibilityLabel={shareChipLabel(name, selected)}
              onPress={() => onChange(toggleCircleShare(selectedIds, circle.id))}
              style={[
                styles.chip,
                {
                  backgroundColor: selected ? colors.primarySoft : colors.surface,
                  borderColor: selected ? colors.primary : colors.line,
                },
              ]}>
              <Text style={{ color: selected ? colors.primaryDark : colors.ink, fontWeight: '700' }}>{name}</Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    gap: 8,
  },
  lead: {
    fontWeight: '700',
  },
  row: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  chip: {
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
});
