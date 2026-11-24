import { useCallback, useEffect, useState } from 'react';
import { Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { useRouter, type Href } from 'expo-router';
import { ProfileForm } from '@/components/ProfileForm';
import { ProfilePhoto } from '@/components/ProfilePhoto';
import { Button } from '@/components/ui/Button';
import { FieldChips } from '@/components/ui/FieldChips';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, uploadNativeFile, type LifeArea, type Profile } from '@/lib/api';
import { AREA_KIND_LABEL, displayLabel } from '@/lib/labels';
import { enabledFields, fieldsToDraft, type SuggestedFieldDraft } from '@/lib/suggestFields';
import { type RecordTypeItem } from '@/lib/records';

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
  const [types, setTypes] = useState<RecordTypeItem[]>([]);
  const [gallery, setGallery] = useState<RecordTypeItem[]>([]);
  const [sentence, setSentence] = useState('');
  const [suggestedName, setSuggestedName] = useState<string | null>(null);
  const [suggestedDraft, setSuggestedDraft] = useState<SuggestedFieldDraft[]>([]);
  const [extraFieldName, setExtraFieldName] = useState('');

  const loadAreas = useCallback(async () => {
    if (!token) {
      return;
    }
    setAreas(await api<LifeArea[]>('/api/life-areas', { token }));
    setTypes(await api<RecordTypeItem[]>('/api/record-types', { token }));
    setGallery(await api<RecordTypeItem[]>('/api/record-types/gallery', { token }));
  }, [token]);

  useEffect(() => {
    void loadAreas().catch((err) => {
      setError(err instanceof ApiError ? err.message : 'Não foi possível abrir as áreas.');
    });
  }, [loadAreas]);

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
          O perfil nasce privado. Você descreve o que quer guardar, copia um exemplo ou deixa o app sugerir. Nada
          entra sozinho.
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

        <Text style={[styles.section, { color: colors.ink }]}>Seus tipos</Text>
        <TextField
          label="Descrever um tipo"
          value={sentence}
          onChangeText={setSentence}
          placeholder="quero anotar as vacinas do meu filho, com data, posto e lote"
        />
        <Button
          label="Sugerir tipo"
          variant="ghost"
          disabled={!sentence.trim()}
          onPress={() => {
            if (!token || !sentence.trim()) {
              return;
            }
            void api<{ name: string; fields: { id: string; name: string; kind: string; required: boolean; order: number }[] }>(
              '/api/record-types/suggest',
              { method: 'POST', token, body: JSON.stringify({ sentence: sentence.trim() }) },
            ).then((payload) => {
              setSuggestedName(payload.name);
              setSuggestedDraft(fieldsToDraft(payload.fields ?? []));
              setExtraFieldName('');
            });
          }}
        />
        {suggestedName ? (
          <>
            <Text style={[styles.lead, { color: colors.muted }]}>Sugestão: {suggestedName}</Text>
            <FieldChips
              fields={suggestedDraft}
              extraName={extraFieldName}
              onChange={setSuggestedDraft}
              onExtraName={setExtraFieldName}
            />
            <Button
              label="Usar esta sugestão"
              onPress={() => {
                if (!token || !suggestedName) {
                  return;
                }
                void api<RecordTypeItem>('/api/record-types', {
                  method: 'POST',
                  token,
                  body: JSON.stringify({ name: suggestedName, fields: enabledFields(suggestedDraft) }),
                }).then((created) => {
                  setSentence('');
                  setSuggestedName(null);
                  setSuggestedDraft([]);
                  router.push(`/(app)/type/${created.id}` as Href);
                });
              }}
            />
          </>
        ) : null}
        {types.map((item) => (
          <Pressable
            key={item.id}
            accessibilityRole="button"
            accessibilityLabel={item.name}
            onPress={() => router.push(`/(app)/type/${item.id}` as Href)}
            style={[styles.area, { borderBottomColor: colors.line }]}>
            <Text style={[styles.areaName, { color: colors.ink }]}>{item.name}</Text>
          </Pressable>
        ))}
        <Text style={[styles.section, { color: colors.ink }]}>Exemplos</Text>
        {gallery
          .filter((item) => !types.some((owned) => owned.name === item.name || owned.originTemplateId === item.id))
          .map((item) => (
            <Pressable
              key={item.id}
              accessibilityRole="button"
              accessibilityLabel={item.name}
              onPress={() => {
                if (!token) {
                  return;
                }
                void api<RecordTypeItem>('/api/record-types/from-gallery', {
                  method: 'POST',
                  token,
                  body: JSON.stringify({ templateId: item.id }),
                }).then((copied) => router.push(`/(app)/type/${copied.id}` as Href));
              }}
              style={[styles.area, { borderBottomColor: colors.line }]}>
              <Text style={[styles.areaName, { color: colors.ink }]}>{item.name}</Text>
              <Text style={[styles.lead, { color: colors.muted }]}>{item.description}</Text>
            </Pressable>
          ))}
        {areas.filter((area) => !types.some((item) => item.name === area.name)).length > 0 ? (
          <Text style={[styles.section, { color: colors.ink }]}>Áreas antigas</Text>
        ) : null}
        {areas
          .filter((area) => !types.some((item) => item.name === area.name))
          .map((area) => (
          <Pressable
            key={area.id}
            accessibilityRole="button"
            accessibilityLabel={area.name}
            onPress={() => router.push(`/(app)/life/${area.id}`)}
            style={[styles.area, { borderBottomColor: colors.line }]}>
            <Text style={[styles.areaName, { color: colors.ink }]}>{area.name}</Text>
            {displayLabel(area.kind, AREA_KIND_LABEL) &&
            displayLabel(area.kind, AREA_KIND_LABEL) !== area.name ? (
              <Text style={[styles.lead, { color: colors.muted }]}>
                Tipo: {displayLabel(area.kind, AREA_KIND_LABEL)}
              </Text>
            ) : null}
          </Pressable>
        ))}
        <Feedback message={null} />
        <AreaComposer value={newArea} onChange={setNewArea} onCreate={() => void createArea()} />
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
        <Button label="Memórias" variant="ghost" onPress={() => router.push('/(app)/memories' as Href)} />
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
      <TextField label="Nova área" value={value} onChangeText={onChange} placeholder="Ex.: Corrida" />
      <Button
        label="Criar área"
        variant="ghost"
        onPress={onCreate}
        disabled={!value.trim()}
        disabledHint="Informe o nome da área para criar."
      />
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
