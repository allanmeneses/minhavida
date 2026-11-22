import { useEffect, useState } from 'react';
import { ScrollView, Text } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type CircleRecap } from '@/lib/api';
import { CIRCLE_KIND_LABEL, type CircleKind } from '@/lib/circleKinds';
import { screenStyles } from '@/lib/screenStyles';

export default function PublicRecapScreen() {
  const params = useLocalSearchParams<{ token: string | string[] }>();
  const shareToken = Array.isArray(params.token) ? params.token[0] : params.token;
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [recap, setRecap] = useState<CircleRecap | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!shareToken) {
      return;
    }
    void api<CircleRecap>(`/api/public/recaps/${shareToken}`)
      .then(setRecap)
      .catch((err) => {
        setRecap(null);
        setError(err instanceof ApiError ? err.message : 'Recap nao encontrado.');
      });
  }, [shareToken]);

  const kindLabel = recap ? CIRCLE_KIND_LABEL[recap.kind as CircleKind] ?? recap.kind : '';

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>Circulos</Text>
      <Text style={styles.title}>Historia compartilhada</Text>
      <Text style={styles.lead}>Esta pagina mostra so a narrativa de quem compartilhou, nao os dados dos outros.</Text>
      {error ? <Text style={styles.lead}>{error}</Text> : null}
      {recap ? (
        <>
          <Text style={styles.item}>{recap.displayName}</Text>
          <Text style={styles.lead}>
            {recap.name} · {kindLabel} · {recap.status}
          </Text>
          {recap.flows.map((flow) => (
            <Text key={flow.key} style={styles.lead}>
              {flow.label} · {flow.count}
            </Text>
          ))}
          {recap.myTitles.map((title, index) => (
            <Text key={`${title}-${index}`} style={styles.item}>
              {title}
            </Text>
          ))}
        </>
      ) : null}
      <Button label="Entrar no Circulos" variant="ghost" onPress={() => router.push('/(auth)/login')} />
    </ScrollView>
  );
}
