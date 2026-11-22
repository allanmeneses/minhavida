import { useCallback, useEffect, useState } from 'react';
import { Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { useRouter, type Href } from 'expo-router';
import { ProfileForm } from '@/components/ProfileForm';
import { ProfilePhoto } from '@/components/ProfilePhoto';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, uploadNativeFile, type LifeArea, type Profile } from '@/lib/api';
export default function ProfileScreen() {
  const { token, user, profile, setProfile, bumpPhoto, photoRevision } = useAuth();
  const { colors } = useTheme();
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [photoBusy, setPhotoBusy] = useState(false);
  const [areas, setAreas] = useState<LifeArea[]>([]);
  const [newArea, setNewArea] = useState('');

  const loadAreas = useCallback(async () => {
    if (!token) {
      return;
    }
    setAreas(await api<LifeArea[]>('/api/life-areas', { token }));
  }, [token]);

  useEffect(() => {
    void loadAreas().catch((err) => {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel abrir as areas.');
    });
  }, [loadAreas]);

  async function save(values: {
    displayName: string;
    birthDate: string;
    phone: string;
    secondaryEmail: string;
    isPublic: boolean;
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
        }),
      });
      setProfile(next);
      setOk('Perfil atualizado.');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel salvar o perfil.');
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
      setError(err instanceof Error ? err.message : 'Nao foi possivel enviar a foto.');
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
      setError(err instanceof Error ? err.message : 'Nao foi possivel remover a foto.');
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
      setError('Precisamos da camera para tirar a foto.');
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

  async function createArea() {
    if (!token || !newArea.trim()) {
      return;
    }
    await api('/api/life-areas', { method: 'POST', token, body: JSON.stringify({ name: newArea.trim() }) });
    setNewArea('');
    await loadAreas();
  }

  return (
    <ScrollView contentContainerStyle={styles.page} keyboardShouldPersistTaps="handled">
      <View style={[styles.card, { backgroundColor: Platform.OS === 'web' ? colors.surface : 'transparent', borderColor: colors.line }]}>
        <Text style={[styles.kicker, { color: colors.primary }]}>Perfil solo</Text>
        <Text style={[styles.title, { color: colors.ink }]}>Sua vida, no seu ritmo</Text>
        <Text style={[styles.lead, { color: colors.muted }]}>
          O perfil nasce privado. Vacinas, lembretes, documentos e o diario ficam aqui.
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
            <Button label="Camera" variant="ghost" onPress={() => void pickFromCamera()} loading={photoBusy} />
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

        <Text style={[styles.section, { color: colors.ink }]}>Areas de vida</Text>
        {areas.map((area) => (
          <Pressable
            key={area.id}
            accessibilityRole="button"
            accessibilityLabel={area.name}
            onPress={() => router.push(`/(app)/life/${area.id}`)}
            style={[styles.area, { borderBottomColor: colors.line }]}>
            <Text style={[styles.areaName, { color: colors.ink }]}>{area.name}</Text>
            <Text style={[styles.lead, { color: colors.muted }]}>{area.kind}</Text>
          </Pressable>
        ))}
        <Feedback message={null} />
        <AreaComposer value={newArea} onChange={setNewArea} onCreate={() => void createArea()} />
        <Button label="Listas" variant="ghost" onPress={() => router.push('/(app)/lists' as Href)} />
        <Button label="Memorias" variant="ghost" onPress={() => router.push('/(app)/memories' as Href)} />
        <Button label="Pet e grupo" variant="ghost" onPress={() => router.push('/(app)/living' as Href)} />
        <Button label="Modelos" variant="ghost" onPress={() => router.push('/(app)/templates' as Href)} />
      </View>
    </ScrollView>
  );
}

function AreaComposer({
  value,
  onChange,
  onCreate,
}: {
  value: string;
  onChange: (value: string) => void;
  onCreate: () => void;
}) {
  return (
    <View style={{ gap: 8 }}>
      <TextField label="Nova area" value={value} onChangeText={onChange} placeholder="Ex.: Corrida" />
      <Button label="Criar area" variant="ghost" onPress={onCreate} disabled={!value.trim()} />
    </View>
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
  section: {
    marginTop: 12,
    fontWeight: '800',
    fontSize: 18,
  },
  area: {
    paddingVertical: 10,
    borderBottomWidth: 1,
  },
  areaName: {
    fontWeight: '700',
  },
});
