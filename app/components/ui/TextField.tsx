import { Platform, StyleSheet, Text, TextInput, View, type KeyboardTypeOptions } from 'react-native';
import { colors } from '@/constants/theme';

type Props = {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  placeholder?: string;
  secureTextEntry?: boolean;
  keyboardType?: KeyboardTypeOptions;
  autoCapitalize?: 'none' | 'sentences' | 'words' | 'characters';
  autoComplete?: 'email' | 'password' | 'name' | 'tel' | 'off';
};

export function TextField({
  label,
  value,
  onChangeText,
  placeholder,
  secureTextEntry,
  keyboardType,
  autoCapitalize = 'none',
  autoComplete,
}: Props) {
  return (
    <View style={styles.wrap}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor={colors.muted}
        secureTextEntry={secureTextEntry}
        keyboardType={keyboardType}
        autoCapitalize={autoCapitalize}
        autoComplete={autoComplete}
        autoCorrect={false}
        style={styles.input}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    gap: 6,
  },
  label: {
    color: colors.ink,
    fontSize: 13,
    fontWeight: '600',
  },
  input: {
    minHeight: Platform.OS === 'web' ? 42 : 48,
    borderWidth: 1,
    borderColor: colors.line,
    borderRadius: Platform.OS === 'web' ? 8 : 14,
    paddingHorizontal: 14,
    backgroundColor: colors.white,
    color: colors.ink,
    fontSize: Platform.OS === 'web' ? 14 : 16,
  },
});
