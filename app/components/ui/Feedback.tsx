import { StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';

export function Feedback({ message, tone = 'error' }: { message: string | null; tone?: 'error' | 'ok' }) {
  const { colors } = useTheme();
  if (!message) {
    return null;
  }

  return (
    <View
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
