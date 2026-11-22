import { useCallback, useEffect, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type CircleDetail } from '@/lib/api';
import { formatPersonTime } from '@/lib/formatPersonTime';
import { screenStyles } from '@/lib/screenStyles';

export default function CircleDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { token, user } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [detail, setDetail] = useState<CircleDetail | null>(null);
  const [inviteEmail, setInviteEmail] = useState('');
  const [post, setPost] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token || !id) {
      return;
    }
    setDetail(await api<CircleDetail>(`/api/circles/${id}`, { token }));
  }, [token, id]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Circulo nao encontrado.'));
  }, [load]);

  const isMember = Boolean(detail?.members.some((member) => member.userId === user?.id));
  const isAdmin = Boolean(
    detail?.members.some((member) => member.userId === user?.id && (member.role === 'admin' || member.role === 'owner')),
  );

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

  async function join() {
    if (!token || !id) {
      return;
    }
    try {
      await api(`/api/circles/${id}/join`, { method: 'POST', token, body: JSON.stringify({}) });
      setOk(detail?.visibility === 'public' ? 'Voce entrou no circulo.' : 'Pedido de entrada enviado.');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel pedir entrada.');
    }
  }

  async function acceptJoin(requestId: string) {
    if (!token || !id) {
      return;
    }
    await api(`/api/circles/${id}/join-requests/${requestId}/accept`, { method: 'POST', token });
    setOk('Pedido aceito.');
    await load();
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>{detail?.name ?? 'Circulo'}</Text>
      <Text style={styles.lead}>{detail?.description}</Text>
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />

      {!isMember ? (
        <Button
          label={detail?.visibility === 'public' ? 'Entrar' : 'Pedir para entrar'}
          onPress={() => void join()}
        />
      ) : null}

      <Text style={styles.section}>Quem esta aqui</Text>
      {detail?.members.map((member) => (
        <Text key={member.userId} style={styles.item}>
          {member.name} · {member.role}
        </Text>
      ))}

      {isAdmin && (detail?.joinRequests?.length ?? 0) > 0 ? (
        <>
          <Text style={styles.section}>Pedidos de entrada</Text>
          {detail?.joinRequests.map((request) => (
            <View key={request.id} style={styles.card}>
              <Text style={styles.item}>{request.name}</Text>
              <Button label={`Aceitar ${request.name}`} onPress={() => void acceptJoin(request.id)} />
            </View>
          ))}
        </>
      ) : null}

      {isMember ? (
        <>
          <TextField label="Convidar por e-mail" value={inviteEmail} onChangeText={setInviteEmail} />
          <Button label="Enviar convite" onPress={() => void invite()} disabled={!inviteEmail.trim()} />
          <TextField label="Nova entrada" value={post} onChangeText={setPost} />
          <Button label="Publicar" onPress={() => void publish()} disabled={!post.trim()} />
        </>
      ) : null}

      {detail?.posts.map((item) => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.item}>{item.body}</Text>
          {isMember ? <Button label="Apagar" variant="danger" onPress={() => void removePost(item.id)} /> : null}
        </View>
      ))}

      <Text style={styles.section}>Log</Text>
      {detail?.log.map((item) => (
        <Text key={item.id} style={styles.lead}>
          {formatPersonTime(item.createdAt)} · {item.action}: {item.detail}
        </Text>
      ))}

      {detail?.members.some((member) => member.userId === user?.id && member.role !== 'owner') ? (
        <Button label="Sair do circulo" variant="danger" onPress={() => void leave()} />
      ) : null}
    </ScrollView>
  );
}
