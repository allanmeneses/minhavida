import { StyleSheet, Text, View } from 'react-native';
import { colors } from '@/constants/theme';

export function Feedback({ message, tone = 'error' }: { message: string | null; tone?: 'error' | 'ok' }) {
  if (!message) {
    return null;
  }

  return (
    <View style={[styles.box, tone === 'ok' ? styles.ok : styles.error]}>
      <Text style={[styles.text, tone === 'ok' ? styles.okText : styles.errorText]}>{message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  box: {
    borderRadius: 10,
    padding: 12,
  },
  error: {
    backgroundColor: colors.dangerSoft,
  },
  ok: {
    backgroundColor: colors.primarySoft,
  },
  text: {
    fontSize: 14,
    lineHeight: 20,
  },
  errorText: {
    color: colors.danger,
  },
  okText: {
    color: colors.primaryDark,
  },
});
