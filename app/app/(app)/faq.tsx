import { ScrollView, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';
import { FAQ_ITEMS } from '@/lib/faq';
import { screenStyles } from '@/lib/screenStyles';

export default function FaqScreen() {
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>Ajuda</Text>
      <Text style={styles.title}>FAQ</Text>
      <Text style={styles.lead}>Role até achar o assunto. Não há busca nesta lista.</Text>
      {FAQ_ITEMS.map((item) => (
        <View key={item.question} style={styles.card}>
          <Text style={styles.section}>{item.question}</Text>
          <Text style={styles.lead}>{item.answer}</Text>
        </View>
      ))}
    </ScrollView>
  );
}
