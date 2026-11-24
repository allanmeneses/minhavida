import { useState } from 'react';
import { Platform, StyleSheet, Text, TextInput, View, type KeyboardTypeOptions } from 'react-native';
import { useTheme } from '@/context/ThemeContext';

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
  const [grownHeight, setGrownHeight] = useState(88);
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
        onContentSizeChange={
          multiline
            ? (event) => {
                const next = Math.ceil(event.nativeEvent.contentSize.height);
                setGrownHeight((current) => Math.max(88, next, current));
              }
            : undefined
        }
        style={[
          styles.input,
          multiline ? styles.multiline : null,
          multiline ? { height: grownHeight } : null,
          multiline && Platform.OS === 'web' ? ({ fieldSizing: 'content' } as object) : null,
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
