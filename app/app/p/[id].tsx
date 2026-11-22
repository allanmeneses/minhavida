import { useEffect, useState } from 'react';
import { Image, ScrollView, Text } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type PublicProfile } from '@/lib/api';
import { API_URL } from '@/lib/config';
import { screenStyles } from '@/lib/screenStyles';

export default function PublicProfileScreen() {
  const params = useLocalSearchParams<{ id: string | string[] }>();
  const userId = Array.isArray(params.id) ? params.id[0] : params.id;
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [profile, setProfile] = useState<PublicProfile | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!userId) {
      return;
    }
    void api<PublicProfile>(`/api/public/profiles/${userId}`)
      .then(setProfile)
      .catch((err) => {
        setProfile(null);
        setError(err instanceof ApiError ? err.message : 'Perfil nao encontrado.');
      });
  }, [userId]);

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>Circulos</Text>
      <Text style={styles.title}>Perfil publico</Text>
      <Text style={styles.lead}>Quem visita ve so nome e foto. Telefone, e-mail e diario ficam fora.</Text>
      {error ? <Text style={styles.lead}>{error}</Text> : null}
      {profile ? (
        <>
          <Text style={styles.item}>{profile.displayName}</Text>
          {profile.hasPhoto ? (
            <Image
              accessibilityLabel={`Foto de ${profile.displayName}`}
              source={{ uri: `${API_URL}/api/public/profiles/${profile.userId}/photo` }}
              style={{ width: 160, height: 160, borderRadius: 80 }}
            />
          ) : (
            <Text style={styles.lead}>Sem foto.</Text>
          )}
        </>
      ) : null}
      <Button label="Entrar no Circulos" variant="ghost" onPress={() => router.push('/(auth)/login')} />
    </ScrollView>
  );
}
