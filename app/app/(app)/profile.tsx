import { useCallback, useEffect, useState } from 'react';
import { Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { ProfilePhoto } from '@/components/ProfilePhoto';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { colors } from '@/constants/theme';
import { useAuth } from '@/context/AuthContext';
import { api, ApiError, uploadNativeFile, type Profile } from '@/lib/api';

export default function ProfileScreen() {
  const { token, user, profile, setProfile, bumpPhoto, photoRevision, refreshProfile } = useAuth();
  const [displayName, setDisplayName] = useState(profile?.displayName ?? '');
  const [birthDate, setBirthDate] = useState(profile?.birthDate ?? '');
  const [phone, setPhone] = useState(profile?.phone ?? '');
  const [secondaryEmail, setSecondaryEmail] = useState(profile?.secondaryEmail ?? '');
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [photoBusy, setPhotoBusy] = useState(false);

  useEffect(() => {
    setDisplayName(profile?.displayName ?? '');
    setBirthDate(profile?.birthDate ?? '');
    setPhone(profile?.phone ?? '');
    setSecondaryEmail(profile?.secondaryEmail ?? '');
  }, [profile]);

  const save = useCallback(async () => {
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
          displayName: displayName.trim(),
          birthDate: birthDate.trim() ? birthDate.trim() : null,
          phone: phone.trim() ? phone.trim() : null,
          secondaryEmail: secondaryEmail.trim() ? secondaryEmail.trim() : null,
        }),
      });
      setProfile(next);
      setOk('Perfil atualizado.');
      await refreshProfile();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel salvar o perfil.');
    } finally {
      setBusy(false);
    }
  }, [token, displayName, birthDate, phone, secondaryEmail, setProfile, refreshProfile]);

  async function pickPhoto() {
    if (!token) {
      return;
    }
    setError(null);
    setOk(null);
    const picked = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.85,
    });
    if (picked.canceled || !picked.assets[0]) {
      return;
    }

    const asset = picked.assets[0];
    setPhotoBusy(true);
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
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel remover a foto.');
    } finally {
      setPhotoBusy(false);
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.page} keyboardShouldPersistTaps="handled">
      <View style={styles.card}>
        <Text style={styles.kicker}>Perfil pessoal</Text>
        <Text style={styles.title}>Seus dados, so seus</Text>
        <Text style={styles.lead}>
          Este e o ponto de partida do Circulos. Nome, foto, nascimento e contatos ficam aqui.
          Documentos privados ficam na outra aba.
        </Text>

        <View style={styles.photoRow}>
          <ProfilePhoto
            name={displayName || user?.name || ''}
            hasPhoto={Boolean(profile?.hasPhoto)}
            token={token}
            revision={photoRevision}
          />
          <View style={styles.photoActions}>
            <Button label={photoBusy ? 'Enviando...' : 'Trocar foto'} onPress={() => void pickPhoto()} loading={photoBusy} />
            {profile?.hasPhoto ? (
              <Pressable onPress={() => void removePhoto()} disabled={photoBusy}>
                <Text style={styles.removePhoto}>Remover foto</Text>
              </Pressable>
            ) : null}
          </View>
        </View>

        <TextField label="Nome" value={displayName} onChangeText={setDisplayName} autoCapitalize="words" autoComplete="name" />
        <TextField
          label="Data de nascimento"
          value={birthDate}
          onChangeText={setBirthDate}
          placeholder="AAAA-MM-DD"
        />
        <TextField label="Telefone" value={phone} onChangeText={setPhone} keyboardType="phone-pad" autoComplete="tel" />
        <TextField
          label="E-mail de contato"
          value={secondaryEmail}
          onChangeText={setSecondaryEmail}
          keyboardType="email-address"
          autoComplete="email"
          placeholder="Opcional, se for diferente do login"
        />
        <Text style={styles.account}>Conta: {user?.email}</Text>
        <Feedback message={error} />
        <Feedback message={ok} tone="ok" />
        <Button label="Salvar perfil" onPress={() => void save()} loading={busy} disabled={!displayName.trim()} />
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
    backgroundColor: Platform.OS === 'web' ? colors.surface : 'transparent',
    borderRadius: Platform.OS === 'web' ? 16 : 0,
    padding: Platform.OS === 'web' ? 28 : 4,
    gap: 14,
    borderWidth: Platform.OS === 'web' ? 1 : 0,
    borderColor: colors.line,
  },
  kicker: {
    color: colors.primary,
    fontWeight: '700',
    textTransform: 'uppercase',
    fontSize: 12,
    letterSpacing: 0.6,
  },
  title: {
    fontSize: 28,
    fontWeight: '800',
    color: colors.ink,
  },
  lead: {
    color: colors.muted,
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
  removePhoto: {
    color: colors.danger,
    fontWeight: '600',
  },
  account: {
    color: colors.muted,
    fontSize: 13,
  },
});
