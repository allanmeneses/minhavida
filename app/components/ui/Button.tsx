import { ActivityIndicator, Platform, Pressable, StyleSheet, Text } from 'react-native';
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
      style={({ pressed, hovered }) => [
        styles.base,
        variant === 'primary' && { backgroundColor: colors.primary },
        variant === 'ghost' && { backgroundColor: 'transparent', borderWidth: 1, borderColor: colors.line },
        variant === 'danger' && { backgroundColor: colors.dangerSoft },
        pressed && styles.pressed,
        hovered && !isDisabled && styles.hovered,
        isDisabled && styles.disabled,
      ]}>
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
