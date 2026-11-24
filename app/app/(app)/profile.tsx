import { useState } from 'react';
import { Platform, ScrollView, StyleSheet, Text, View } from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { useRouter, type Href } from 'expo-router';
import { ProfileForm } from '@/components/ProfileForm';
import { ProfilePhoto } from '@/components/ProfilePhoto';
import { Button } from '@/components/ui/Button';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, uploadNativeFile, type Profile } from '@/lib/api';

export default function ProfileScreen() {
  const { token, user, profile, setProfile, bumpPhoto, photoRevision } = useAuth();
  const { colors } = useTheme();
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [photoBusy, setPhotoBusy] = useState(false);

  async function save(values: {
    displayName: string;
    birthDate: string;
    phone: string;
    secondaryEmail: string;
    isPublic: boolean;
    bio: string;
  }) {
    if (!token) {
      return;
    }
    setError(null);
    setOk(null);
    setBusy(true);
    try {
      const next = await api<Profile>('/api/profile', {
        method: 'PUT',
        token,
        body: JSON.stringify({
          displayName: values.displayName.trim(),
          birthDate: values.birthDate.trim() ? values.birthDate.trim() : null,
          phone: values.phone.trim() ? values.phone.trim() : null,
          secondaryEmail: values.secondaryEmail.trim() ? values.secondaryEmail.trim() : null,
          isPublic: values.isPublic,
          bio: values.bio.trim() ? values.bio.trim() : null,
        }),
      });
      setProfile(next);
      setOk('Perfil atualizado.');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível salvar o perfil.');
    } finally {
      setBusy(false);
    }
  }

  async function sendPhoto(asset: ImagePicker.ImagePickerAsset) {
    if (!token) {
      return;
    }
    setPhotoBusy(true);
    setError(null);
    try {
      let next: Profile;
      if (Platform.OS === 'web' && asset.file) {
        const form = new FormData();
        form.append('file', asset.file, asset.fileName ?? 'foto.jpg');
        next = await api<Profile>('/api/profile/photo', { method: 'POST', token, body: form });
      } else {
        next = await uploadNativeFile<Profile>(
          '/api/profile/photo',
          token,
          { uri: asset.uri, name: asset.fileName, mimeType: asset.mimeType ?? 'image/jpeg' },
          'foto.jpg',
        );
      }
      setProfile(next);
      bumpPhoto();
      setOk('Foto atualizada.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível enviar a foto.');
    } finally {
      setPhotoBusy(false);
    }
  }

  async function removePhoto() {
    if (!token) {
      return;
    }
    setPhotoBusy(true);
    setError(null);
    try {
      const next = await api<Profile>('/api/profile/photo', { method: 'DELETE', token });
      setProfile(next);
      bumpPhoto();
      setOk('Foto removida.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível remover a foto.');
    } finally {
      setPhotoBusy(false);
    }
  }

  async function pickFromLibrary() {
    const picked = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.85,
    });
    if (!picked.canceled && picked.assets[0]) {
      await sendPhoto(picked.assets[0]);
    }
  }

  async function pickFromCamera() {
    const permission = await ImagePicker.requestCameraPermissionsAsync();
    if (!permission.granted) {
      setError('Precisamos da câmera para tirar a foto.');
      return;
    }
    const picked = await ImagePicker.launchCameraAsync({
      mediaTypes: ['images'],
      quality: 0.85,
    });
    if (!picked.canceled && picked.assets[0]) {
      await sendPhoto(picked.assets[0]);
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.page} keyboardShouldPersistTaps="handled">
      <View style={[styles.card, { backgroundColor: Platform.OS === 'web' ? colors.surface : 'transparent', borderColor: colors.line }]}>
        <Text style={[styles.kicker, { color: colors.primary }]}>Perfil solo</Text>
        <Text style={[styles.title, { color: colors.ink }]}>Sua vida, no seu ritmo</Text>
        <Text style={[styles.lead, { color: colors.muted }]}>
          O perfil nasce privado. Nome, foto e como te chamam ficam aqui. Tipos de registro ficam em Tipos.
        </Text>

        <View style={styles.photoRow}>
          <ProfilePhoto
            name={profile?.displayName || user?.name || ''}
            hasPhoto={Boolean(profile?.hasPhoto)}
            token={token}
            revision={photoRevision}
          />
          <View style={styles.photoActions}>
            <Button label={photoBusy ? 'Enviando...' : 'Galeria'} onPress={() => void pickFromLibrary()} loading={photoBusy} />
            <Button label="Câmera" variant="ghost" onPress={() => void pickFromCamera()} loading={photoBusy} />
            {profile?.hasPhoto ? (
              <Button label="Remover foto" variant="danger" onPress={() => void removePhoto()} loading={photoBusy} />
            ) : null}
          </View>
        </View>

        <ProfileForm
          initial={profile}
          error={error}
          ok={ok}
          busy={busy}
          accountEmail={user?.email}
          onSave={(values) => void save(values)}
        />

        {profile?.isPublic && user?.id ? (
          <Text style={[styles.lead, { color: colors.muted }]}>
            Link público: /p/{user.id}. Quem visita vê nome, foto e descrição.
          </Text>
        ) : (
          <Text style={[styles.lead, { color: colors.muted }]}>
            O perfil está privado. Ninguém de fora vê nome nem foto.
          </Text>
        )}
        <Button label="Resumo de uso" variant="ghost" onPress={() => router.push('/(app)/recap' as Href)} />
        <Button label="Listas" variant="ghost" onPress={() => router.push('/(app)/lists' as Href)} />
        <Button label="Pet e grupo" variant="ghost" onPress={() => router.push('/(app)/living' as Href)} />
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  page: {
    paddingBottom: 48,
    maxWidth: 720,
    width: '100%',
    alignSelf: 'center',
  },
  card: {
    borderRadius: Platform.OS === 'web' ? 16 : 0,
    padding: Platform.OS === 'web' ? 28 : 4,
    gap: 14,
    borderWidth: Platform.OS === 'web' ? 1 : 0,
  },
  kicker: {
    fontWeight: '700',
    textTransform: 'uppercase',
    fontSize: 12,
    letterSpacing: 0.6,
  },
  title: {
    fontSize: 28,
    fontWeight: '800',
  },
  lead: {
    lineHeight: 22,
  },
  photoRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 16,
    marginVertical: 8,
  },
  photoActions: {
    flex: 1,
    gap: 8,
    maxWidth: 220,
  },
});
