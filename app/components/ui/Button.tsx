import { ActivityIndicator, Platform, Pressable, StyleSheet, Text } from 'react-native';
import { colors } from '@/constants/theme';

type Props = {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  loading?: boolean;
  variant?: 'primary' | 'ghost' | 'danger';
};

export function Button({ label, onPress, disabled, loading, variant = 'primary' }: Props) {
  const isDisabled = disabled || loading;
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      disabled={isDisabled}
      style={({ pressed, hovered }) => [
        styles.base,
        variant === 'primary' && styles.primary,
        variant === 'ghost' && styles.ghost,
        variant === 'danger' && styles.danger,
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
            variant === 'ghost' && styles.ghostLabel,
            variant === 'danger' && styles.dangerLabel,
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
  primary: {
    backgroundColor: colors.primary,
  },
  ghost: {
    backgroundColor: 'transparent',
    borderWidth: 1,
    borderColor: colors.line,
  },
  danger: {
    backgroundColor: colors.dangerSoft,
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
    color: colors.white,
    fontWeight: '700',
    fontSize: Platform.OS === 'web' ? 14 : 16,
  },
  ghostLabel: {
    color: colors.ink,
  },
  dangerLabel: {
    color: colors.danger,
  },
});
