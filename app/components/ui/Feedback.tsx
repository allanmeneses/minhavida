import { StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';

export function Feedback({
  message,
  tone = 'error',
  testID,
}: {
  message: string | null;
  tone?: 'error' | 'ok';
  testID?: string;
}) {
  const { colors } = useTheme();
  if (!message) {
    return null;
  }

  return (
    <View
      testID={testID}
      accessibilityLabel={tone === 'ok' ? message : undefined}
      style={[
        styles.box,
        { backgroundColor: tone === 'ok' ? colors.primarySoft : colors.dangerSoft },
      ]}>
      <Text style={[styles.text, { color: tone === 'ok' ? colors.primaryDark : colors.danger }]}>
        {message}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  box: {
    borderRadius: 10,
    padding: 12,
  },
  text: {
    fontSize: 14,
    lineHeight: 20,
  },
});
