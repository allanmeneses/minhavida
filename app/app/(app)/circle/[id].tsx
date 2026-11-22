import { useCallback, useEffect, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError } from '@/lib/api';
import { screenStyles } from '@/lib/screenStyles';

type Detail = {
  id: string;
  name: string | null;
  description: string | null;
  visibility: string;
  members: { userId: string; name: string; role: string }[];
  posts: { id: string; body: string; createdAt: string }[];
  log: { id: string; action: string; detail: string; createdAt: string; actorUserId: string | null }[];
};

export default function CircleDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { token, user } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [detail, setDetail] = useState<Detail | null>(null);
  const [inviteEmail, setInviteEmail] = useState('');
  const [post, setPost] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token || !id) {
      return;
    }
    setDetail(await api<Detail>(`/api/circles/${id}`, { token }));
  }, [token, id]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Circulo nao encontrado.'));
  }, [load]);

  async function invite() {
    if (!token || !id || !inviteEmail.trim()) {
      return;
    }
    const result = await api<{ token: string }>(`/api/circles/${id}/invites`, {
      method: 'POST',
      token,
      body: JSON.stringify({ email: inviteEmail.trim(), role: 'viewer' }),
    });
    setOk(`Convite criado. Codigo: ${result.token}`);
    setInviteEmail('');
    await load();
  }

  async function publish() {
    if (!token || !id || !post.trim()) {
      return;
    }
    await api(`/api/circles/${id}/posts`, {
      method: 'POST',
      token,
      body: JSON.stringify({ body: post.trim() }),
    });
    setPost('');
    await load();
  }

  async function removePost(postId: string) {
    if (!token || !id) {
      return;
    }
    await api(`/api/circles/${id}/posts/${postId}`, { method: 'DELETE', token });
    await load();
  }

  async function leave() {
    if (!token || !id) {
      return;
    }
    await api(`/api/circles/${id}/membership`, { method: 'DELETE', token });
    setOk('Voce saiu. Sem rastro para os outros.');
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>{detail?.name ?? 'Circulo'}</Text>
      <Text style={styles.lead}>{detail?.description}</Text>
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />

      <Text style={styles.section}>Quem esta aqui</Text>
      {detail?.members.map((member) => (
        <Text key={member.userId} style={styles.item}>
          {member.name} · {member.role}
        </Text>
      ))}

      <TextField label="Convidar por e-mail" value={inviteEmail} onChangeText={setInviteEmail} />
      <Button label="Enviar convite" onPress={() => void invite()} disabled={!inviteEmail.trim()} />
      <TextField label="Nova entrada" value={post} onChangeText={setPost} />
      <Button label="Publicar" onPress={() => void publish()} disabled={!post.trim()} />

      {detail?.posts.map((item) => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.item}>{item.body}</Text>
          <Button label="Apagar" variant="danger" onPress={() => void removePost(item.id)} />
        </View>
      ))}

      <Text style={styles.section}>Log</Text>
      {detail?.log.map((item) => (
        <Text key={item.id} style={styles.lead}>
          {item.action}: {item.detail}
        </Text>
      ))}

      {detail?.members.some((member) => member.userId === user?.id && member.role !== 'owner') ? (
        <Button label="Sair do circulo" variant="danger" onPress={() => void leave()} />
      ) : null}
    </ScrollView>
  );
}

