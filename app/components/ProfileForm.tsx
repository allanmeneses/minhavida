import { useRef, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useTheme } from '@/context/ThemeContext';
import { hydrateIfPristine } from '@/lib/formDraft';
import type { Profile } from '@/lib/api';

type Values = {
  displayName: string;
  birthDate: string;
  phone: string;
  secondaryEmail: string;
  isPublic: boolean;
  bio: string;
  findableByLocation: boolean;
};

type Props = {
  initial: Profile | null;
  error: string | null;
  ok: string | null;
  busy: boolean;
  accountEmail?: string;
  onSave: (values: Values) => void;
};

function fromProfile(profile: Profile | null): Values {
  return {
    displayName: profile?.displayName ?? '',
    birthDate: profile?.birthDate ?? '',
    phone: profile?.phone ?? '',
    secondaryEmail: profile?.secondaryEmail ?? '',
    isPublic: profile?.isPublic ?? false,
    bio: profile?.bio ?? '',
    findableByLocation: profile?.findableByLocation ?? false,
  };
}

export function ProfileForm({ initial, error, ok, busy, accountEmail, onSave }: Props) {
  const { colors } = useTheme();
  const dirty = useRef(false);
  const [values, setValues] = useState<Values>(fromProfile(initial));
  const next = fromProfile(initial);
  const shown = hydrateIfPristine(dirty.current, values, next);

  function patch(partial: Partial<Values>) {
    dirty.current = true;
    setValues({ ...shown, ...partial });
  }

  return (
    <View style={styles.form}>
      <TextField
        label="Nome"
        value={shown.displayName}
        onChangeText={(displayName) => patch({ displayName })}
        autoCapitalize="words"
        autoComplete="name"
      />
      <TextField
        label="Data de nascimento"
        value={shown.birthDate}
        onChangeText={(birthDate) => patch({ birthDate })}
        placeholder="AAAA-MM-DD"
      />
      <TextField
        label="Telefone"
        value={shown.phone}
        onChangeText={(phone) => patch({ phone })}
        keyboardType="phone-pad"
        autoComplete="tel"
      />
      <TextField
        label="E-mail de contato"
        value={shown.secondaryEmail}
        onChangeText={(secondaryEmail) => patch({ secondaryEmail })}
        keyboardType="email-address"
        autoComplete="email"
        placeholder="Opcional, se for diferente do login"
      />
      <TextField
        label="Descricao publica"
        value={shown.bio}
        onChangeText={(bio) => patch({ bio })}
        placeholder="Uma frase sobre voce"
      />
      <Button
        label={shown.isPublic ? 'Perfil publico' : 'Perfil privado'}
        variant="ghost"
        onPress={() => patch({ isPublic: !shown.isPublic })}
      />
      <Button
        label={shown.findableByLocation ? 'Pode ser encontrado por perto' : 'Nao aparecer na busca por perto'}
        variant="ghost"
        onPress={() => patch({ findableByLocation: !shown.findableByLocation })}
      />
      <Text style={[styles.account, { color: colors.muted }]}>
        {shown.isPublic
          ? 'Quem visita ve nome, foto e descricao. Telefone, e-mail e diario ficam fora.'
          : 'O perfil nasce privado. Ninguem de fora ve seus dados.'}
      </Text>
      {accountEmail ? <Text style={[styles.account, { color: colors.muted }]}>Conta: {accountEmail}</Text> : null}
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />
      <Button
        label="Salvar perfil"
        onPress={() => onSave(shown)}
        loading={busy}
        disabled={!shown.displayName.trim()}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  form: {
    gap: 14,
  },
  account: {
    fontSize: 13,
  },
});
