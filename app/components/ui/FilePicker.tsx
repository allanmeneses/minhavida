import { useRef, useState } from 'react';
import { Platform, StyleSheet, Text, View } from 'react-native';
import { Button } from '@/components/ui/Button';
import { useTheme } from '@/context/ThemeContext';

type Props = {
  label: string;
  accept?: string;
  testID?: string;
  accessibilityLabel?: string;
  valueName?: string | null;
  onFile: (file: File | null) => void;
};

export function FilePicker({ label, accept, testID, accessibilityLabel, valueName, onFile }: Props) {
  const { colors } = useTheme();
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [picked, setPicked] = useState<string | null>(valueName ?? null);
  const shown = valueName ?? picked;

  if (Platform.OS !== 'web') {
    return null;
  }

  return (
    <View style={styles.wrap}>
      <Text nativeID={`${testID ?? 'arquivo'}-label`} style={[styles.label, { color: colors.ink }]}>
        {label}
      </Text>
      <Button label="Escolher arquivo" variant="ghost" onPress={() => inputRef.current?.click()} />
      <Text style={[styles.fileName, { color: colors.muted }]}>{shown || 'Nenhum arquivo escolhido'}</Text>
      <input
        ref={(node) => {
          inputRef.current = node;
        }}
        data-testid={testID}
        aria-labelledby={`${testID ?? 'arquivo'}-label`}
        aria-label={accessibilityLabel ?? label}
        tabIndex={-1}
        type="file"
        accept={accept}
        style={hiddenInput}
        onChange={(event) => {
          const file = event.currentTarget.files?.[0] ?? null;
          setPicked(file?.name ?? null);
          onFile(file);
        }}
      />
    </View>
  );
}

const hiddenInput = {
  position: 'absolute' as const,
  left: -10000,
  width: 1,
  height: 1,
  opacity: 0,
  overflow: 'hidden' as const,
  clipPath: 'inset(50%)',
};

const styles = StyleSheet.create({
  wrap: {
    gap: 6,
  },
  label: {
    fontSize: 13,
    fontWeight: '600',
  },
  fileName: {
    fontSize: 13,
  },
});
