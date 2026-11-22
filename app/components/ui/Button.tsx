import { ActivityIndicator, Platform, Pressable, StyleSheet, Text, type StyleProp, type ViewStyle } from 'react-native';
import { useTheme } from '@/context/ThemeContext';

type Props = {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  loading?: boolean;
  variant?: 'primary' | 'ghost' | 'danger';
};

export function Button({ label, onPress, disabled, loading, variant = 'primary' }: Props) {
  const { colors } = useTheme();
  const isDisabled = disabled || loading;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      onPress={onPress}
      disabled={isDisabled}
      style={(state) => {
        const hovered = Boolean((state as { hovered?: boolean }).hovered);
        const stylesForState: StyleProp<ViewStyle>[] = [
          styles.base,
          variant === 'primary' ? { backgroundColor: colors.primary } : undefined,
          variant === 'ghost' ? { backgroundColor: 'transparent', borderWidth: 1, borderColor: colors.line } : undefined,
          variant === 'danger' ? { backgroundColor: colors.dangerSoft } : undefined,
          state.pressed ? styles.pressed : undefined,
          hovered && !isDisabled ? styles.hovered : undefined,
          isDisabled ? styles.disabled : undefined,
        ];
        return stylesForState;
      }}>
      {loading ? (
        <ActivityIndicator color={variant === 'ghost' ? colors.primary : colors.white} />
      ) : (
        <Text
          style={[
            styles.label,
            { color: colors.white },
            variant === 'ghost' && { color: colors.ink },
            variant === 'danger' && { color: colors.danger },
          ]}>
          {label}
        </Text>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    minHeight: Platform.OS === 'web' ? 42 : 48,
    borderRadius: Platform.OS === 'web' ? 8 : 14,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 18,
  },
  pressed: {
    opacity: 0.86,
  },
  hovered: {
    opacity: 0.92,
  },
  disabled: {
    opacity: 0.5,
  },
  label: {
    fontWeight: '700',
    fontSize: Platform.OS === 'web' ? 14 : 16,
  },
});
