import { Platform, StyleSheet, Text, TextInput, View, type KeyboardTypeOptions } from 'react-native';
import { useTheme } from '@/context/ThemeContext';
import { multilineInputHeight } from '@/lib/textFieldHeight';

type Props = {
  label: string;
  value: string;
  onChangeText: (value: string) => void;
  placeholder?: string;
  secureTextEntry?: boolean;
  keyboardType?: KeyboardTypeOptions;
  autoCapitalize?: 'none' | 'sentences' | 'words' | 'characters';
  autoComplete?: 'email' | 'password' | 'name' | 'tel' | 'off';
  multiline?: boolean;
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
  multiline = false,
}: Props) {
  const { colors } = useTheme();
  const inputHeight = multiline
    ? multilineInputHeight(value, { fontSize: Platform.OS === 'web' ? 14 : 16 })
    : undefined;
  return (
    <View style={styles.wrap}>
      <Text style={[styles.label, { color: colors.ink }]}>{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor={colors.placeholder}
        secureTextEntry={secureTextEntry}
        keyboardType={keyboardType}
        autoCapitalize={autoCapitalize}
        autoComplete={autoComplete}
        autoCorrect={false}
        multiline={multiline}
        textAlignVertical={multiline ? 'top' : 'center'}
        accessibilityLabel={label}
        style={[
          styles.input,
          multiline ? styles.multiline : null,
          inputHeight ? { height: inputHeight } : null,
          {
            borderColor: colors.line,
            backgroundColor: colors.white,
            color: colors.ink,
          },
        ]}
      />
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
  input: {
    minHeight: Platform.OS === 'web' ? 42 : 48,
    borderWidth: 1,
    borderRadius: Platform.OS === 'web' ? 8 : 14,
    paddingHorizontal: 14,
    fontSize: Platform.OS === 'web' ? 14 : 16,
  },
  multiline: {
    minHeight: 88,
    paddingVertical: 10,
  },
});
