import { useCallback, useEffect, useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, downloadNamedBlob, type CircleDetail, type LivingProfileItem, type MemoryItem, type RevealSummary, type SharedListSummary } from '@/lib/api';
import { formatPersonTime } from '@/lib/formatPersonTime';
import {
  LIST_ACCESS,
  LIST_ACCESS_LABEL,
  LIST_KIND_LABEL,
  LIST_KINDS,
  nextOf,
  type ListAccess,
  type ListKind,
} from '@/lib/listKinds';
import { screenStyles } from '@/lib/screenStyles';

export default function CircleDetailScreen() {
  const params = useLocalSearchParams<{ id: string | string[] }>();
  const circleId = Array.isArray(params.id) ? params.id[0] : params.id;
  const { token, user } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const [detail, setDetail] = useState<CircleDetail | null>(null);
  const [lists, setLists] = useState<SharedListSummary[]>([]);
  const [memories, setMemories] = useState<MemoryItem[]>([]);
  const [living, setLiving] = useState<LivingProfileItem[]>([]);
  const [reveal, setReveal] = useState<RevealSummary | null>(null);
  const [inviteEmail, setInviteEmail] = useState('');
  const [post, setPost] = useState('');
  const [memoryTitle, setMemoryTitle] = useState('');
  const [listName, setListName] = useState('');
  const [listKind, setListKind] = useState<ListKind>('shopping');
  const [listAccess, setListAccess] = useState<ListAccess>('view');
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token || !circleId) {
      return;
    }
    setDetail(await api<CircleDetail>(`/api/circles/${circleId}`, { token }));
    try {
      setLists(await api<SharedListSummary[]>(`/api/lists?circleId=${circleId}`, { token }));
    } catch {
      setLists([]);
    }
    try {
      setMemories(await api<MemoryItem[]>(`/api/memories?circleId=${circleId}`, { token }));
    } catch {
      setMemories([]);
    }
    try {
      setLiving(await api<LivingProfileItem[]>(`/api/living-profiles?circleId=${circleId}`, { token }));
    } catch {
      setLiving([]);
    }
    try {
      setReveal(await api<RevealSummary>(`/api/circles/${circleId}/reveal-summary`, { token }));
    } catch {
      setReveal(null);
    }
  }, [token, circleId]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Circulo nao encontrado.'));
  }, [load]);

  const isMember = Boolean(detail?.members.some((member) => member.userId === user?.id));
  const isOwner = Boolean(detail?.members.some((member) => member.userId === user?.id && member.role === 'owner'));
  const isAdmin = Boolean(
    detail?.members.some((member) => member.userId === user?.id && (member.role === 'admin' || member.role === 'owner')),
  );
  const canCreateList = Boolean(
    detail?.members.some(
      (member) =>
        member.userId === user?.id && (member.role === 'editor' || member.role === 'admin' || member.role === 'owner'),
    ),
  );

  async function createList() {
    if (!token || !circleId || !listName.trim()) {
      return;
    }
    await api('/api/lists', {
      method: 'POST',
      token,
      body: JSON.stringify({ name: listName.trim(), kind: listKind, circleId, memberAccess: listAccess }),
    });
    setListName('');
    await load();
  }

  async function invite() {
    if (!token || !circleId || !inviteEmail.trim()) {
      return;
    }
    const result = await api<{ token: string }>(`/api/circles/${circleId}/invites`, {
      method: 'POST',
      token,
      body: JSON.stringify({ email: inviteEmail.trim(), role: 'viewer' }),
    });
    setOk(`Convite criado. Codigo: ${result.token}`);
    setInviteEmail('');
    await load();
  }

  async function publish() {
    if (!token || !circleId || !post.trim()) {
      return;
    }
    await api(`/api/circles/${circleId}/posts`, {
      method: 'POST',
      token,
      body: JSON.stringify({ body: post.trim() }),
    });
    setPost('');
    await load();
  }

  async function removePost(postId: string) {
    if (!token || !circleId) {
      return;
    }
    await api(`/api/circles/${circleId}/posts/${postId}`, { method: 'DELETE', token });
    await load();
  }

  async function leave() {
    if (!token || !circleId) {
      return;
    }
    await api(`/api/circles/${circleId}/membership`, { method: 'DELETE', token });
    setOk('Voce saiu. Sem rastro para os outros.');
  }

  async function join() {
    if (!token || !circleId) {
      return;
    }
    try {
      await api(`/api/circles/${circleId}/join`, { method: 'POST', token, body: JSON.stringify({}) });
      setOk(detail?.visibility === 'public' ? 'Voce entrou no circulo.' : 'Pedido de entrada enviado.');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel pedir entrada.');
    }
  }

  async function transferTo(memberUserId: string) {
    if (!token || !circleId) {
      return;
    }
    await api(`/api/circles/${circleId}/transfer`, {
      method: 'POST',
      token,
      body: JSON.stringify({ memberUserId }),
    });
    setOk('Dono transferido. Historico e permissoes ficam com o circulo.');
    await load();
  }

  async function revealSurprise() {
    if (!token || !circleId) {
      return;
    }
    const summary = await api<RevealSummary>(`/api/circles/${circleId}/reveal`, { method: 'POST', token });
    setReveal(summary);
    setOk('Surpresa revelada. O resumo com mensagens e fotos esta visivel.');
    await load();
  }

  async function keepMemory() {
    if (!token || !circleId) {
      return;
    }
    await api(`/api/circles/${circleId}/keep-memory`, { method: 'POST', token });
    setOk('Recordacao guardada no seu perfil. A edicao e silenciosa.');
  }

  async function createMemory() {
    if (!token || !circleId || !memoryTitle.trim()) {
      return;
    }
    await api('/api/memories', {
      method: 'POST',
      token,
      body: JSON.stringify({ title: memoryTitle.trim(), circleId }),
    });
    setMemoryTitle('');
    await load();
  }

  async function acceptJoin(requestId: string) {
    if (!token || !circleId) {
      return;
    }
    await api(`/api/circles/${circleId}/join-requests/${requestId}/accept`, { method: 'POST', token });
    setOk('Pedido aceito.');
    await load();
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>{detail?.name ?? 'Circulo'}</Text>
      <Text style={styles.lead}>{detail?.description}</Text>
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />
      {isAdmin && detail?.isSurprise && !detail.revealed ? (
        <Button label="Revelar surpresa" onPress={() => void revealSurprise()} />
      ) : null}
      {detail?.isSurprise && reveal ? (
        <>
          <Text style={styles.section}>Resumo da revelacao</Text>
          <Text style={styles.lead}>
            {reveal.messages.length} mensagens · {reveal.photoCount} fotos
          </Text>
          {reveal.messages.map((item) => (
            <Text key={item.postId} style={styles.item}>
              {item.body}
            </Text>
          ))}
          {reveal.revealedAt ? (
            <Button label="Guardar recordacao no meu perfil" onPress={() => void keepMemory()} />
          ) : null}
        </>
      ) : null}

      {!isMember && !(detail?.isSurprise && detail.revealed) ? (
        <Button
          label={detail?.visibility === 'public' ? 'Entrar' : 'Pedir para entrar'}
          onPress={() => void join()}
        />
      ) : null}

      <Text style={styles.section}>Quem esta aqui</Text>
      {detail?.members.map((member) => (
        <View key={member.userId} style={styles.card}>
          <Text style={styles.item}>
            {member.name} · {member.role}
          </Text>
          {isOwner && member.userId !== user?.id ? (
            <Button label={`Transferir dono para ${member.name}`} onPress={() => void transferTo(member.userId)} />
          ) : null}
        </View>
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

      {isMember ? (
        <>
          <Text style={styles.section}>Memorias do circulo</Text>
          {memories.map((item) => (
            <Text key={item.id} style={styles.item}>
              {item.title}
            </Text>
          ))}
          {canCreateList ? (
            <>
              <TextField label="Titulo da memoria" value={memoryTitle} onChangeText={setMemoryTitle} />
              <Button label="Guardar memoria no circulo" onPress={() => void createMemory()} disabled={!memoryTitle.trim()} />
            </>
          ) : null}
          <Text style={styles.section}>Perfis deste circulo</Text>
          {living.map((item) => (
            <Text key={item.id} style={styles.lead}>
              {item.name} · {item.kind}
            </Text>
          ))}
          <Text style={styles.section}>Listas do circulo</Text>
          {lists.map((item) => (
            <Pressable
              key={item.id}
              accessibilityRole="button"
              accessibilityLabel={item.name}
              onPress={() => router.push(`/(app)/list/${item.id}` as Href)}>
              <Text style={styles.item}>{item.name}</Text>
              <Text style={styles.lead}>
                {LIST_KIND_LABEL[item.kind as ListKind] ?? item.kind} · {LIST_ACCESS_LABEL[item.memberAccess as ListAccess] ?? item.memberAccess}
              </Text>
            </Pressable>
          ))}
          {canCreateList ? (
            <>
              <TextField label="Nome da lista" value={listName} onChangeText={setListName} placeholder="Compras da casa" />
              <Button
                label={`Tipo: ${LIST_KIND_LABEL[listKind]}`}
                variant="ghost"
                onPress={() => setListKind(nextOf(LIST_KINDS, listKind))}
              />
              <Button
                label={`Permissao: ${LIST_ACCESS_LABEL[listAccess]}`}
                variant="ghost"
                onPress={() => setListAccess(nextOf(LIST_ACCESS, listAccess))}
              />
              <Button label="Criar lista" onPress={() => void createList()} disabled={!listName.trim()} />
            </>
          ) : null}
        </>
      ) : null}

      <Text style={styles.section}>Log</Text>
      {detail?.log.map((item) => (
        <Text key={item.id} style={styles.lead}>
          {formatPersonTime(item.createdAt)} · {item.action}: {item.detail}
        </Text>
      ))}

      {detail ? (
        <Button
          label="Exportar este circulo"
          variant="ghost"
          onPress={() => {
            if (!token || !circleId) {
              return;
            }
            void downloadNamedBlob(`/api/export/circles/${circleId}`, `circulos-${circleId}.zip`, token)
              .then(() => setOk('Backup do circulo baixado.'))
              .catch((err) => setError(err instanceof ApiError ? err.message : 'Nao foi possivel exportar.'));
          }}
        />
      ) : null}
      {detail?.members.some((member) => member.userId === user?.id && member.role !== 'owner') ? (
        <Button label="Sair do circulo" variant="danger" onPress={() => void leave()} />
      ) : null}
    </ScrollView>
  );
}
