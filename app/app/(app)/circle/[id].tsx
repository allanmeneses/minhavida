import { useCallback, useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useLocalSearchParams, useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import {
  api,
  ApiError,
  downloadNamedBlob,
  type CircleDetail,
  type CircleRecap,
  type GiftIdeaShare,
  type LivingProfileItem,
  type MemoryItem,
  type RecapShareCreated,
  type RevealSummary,
  type SharedListSummary,
  type SuccessionVote,
} from '@/lib/api';
import { hydrateIfPristine } from '@/lib/formDraft';
import { canArchiveKind, CIRCLE_KIND_LABEL, type CircleKind } from '@/lib/circleKinds';
import {
  CIRCLE_STATUS_LABEL,
  LIVING_KIND_LABEL,
  LOG_ACTION_LABEL,
  REPORT_DECISION_LABEL,
  REPORT_STATUS_LABEL,
  ROLE_LABEL,
  SUCCESSION_STATUS_LABEL,
  TARGET_KIND_LABEL,
  displayLabel,
} from '@/lib/labels';
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
import { copyText } from '@/lib/copyText';
import { reportTarget } from '@/lib/report';
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
  const [recap, setRecap] = useState<CircleRecap | null>(null);
  const [sharePath, setSharePath] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [vote, setVote] = useState<SuccessionVote | null>(null);
  const [deleteText, setDeleteText] = useState('');
  const [logFilter, setLogFilter] = useState('');
  const [filteredLog, setFilteredLog] = useState<CircleDetail['log'] | null>(null);
  const [shareText, setShareText] = useState<string | null>(null);
  const [inviteCode, setInviteCode] = useState<string | null>(null);
  const [giftIdeas, setGiftIdeas] = useState<GiftIdeaShare[]>([]);
  const [giftIdeaDraft, setGiftIdeaDraft] = useState('');
  const giftIdeaDirty = useRef(false);
  const shownGiftIdea = hydrateIfPristine(giftIdeaDirty.current, giftIdeaDraft, giftIdeaDraft);
  const [circleReports, setCircleReports] = useState<
    { id: string; targetKind: string; status: string; circleDecision: string | null }[]
  >([]);

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
    try {
      setRecap(await api<CircleRecap>(`/api/circles/${circleId}/recap`, { token }));
    } catch {
      setRecap(null);
    }
    try {
      setVote(await api<SuccessionVote>(`/api/circles/${circleId}/succession`, { token }));
    } catch {
      setVote(null);
    }
    try {
      setGiftIdeas(await api<GiftIdeaShare[]>(`/api/circles/${circleId}/gift-ideas`, { token }));
    } catch {
      setGiftIdeas([]);
    }
    try {
      setCircleReports(await api(`/api/circles/${circleId}/reports`, { token }));
    } catch {
      setCircleReports([]);
    }
  }, [token, circleId]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Círculo não encontrado.'));
  }, [load]);

  const isMember = Boolean(detail?.members.some((member) => member.userId === user?.id));
  const isOwner = Boolean(detail?.members.some((member) => member.userId === user?.id && member.role === 'owner'));
  const isAdmin = Boolean(
    detail?.members.some((member) => member.userId === user?.id && (member.role === 'admin' || member.role === 'owner')),
  );
  const isLocked = detail?.status === 'locked';
  const canCreateList = Boolean(
    !isLocked &&
      detail?.members.some(
        (member) =>
          member.userId === user?.id && (member.role === 'editor' || member.role === 'admin' || member.role === 'owner'),
      ),
  );
  const canPost = canCreateList;
  const canEditGiftIdeas = canCreateList;

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
    setInviteCode(result.token);
    setOk(`Convite criado. Código: ${result.token}`);
    setInviteEmail('');
    await load();
  }

  async function inviteLink() {
    if (!token || !circleId) {
      return;
    }
    const result = await api<{ token: string; shareText: string; shareUrl: string }>(
      `/api/circles/${circleId}/invite-link`,
      { method: 'POST', token },
    );
    setInviteCode(result.token);
    setShareText(result.shareText);
    setOk('Texto pronto para colar no WhatsApp.');
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
    setOk('Você saiu. Sem rastro para os outros.');
    router.push('/(app)/circles');
  }

  async function join() {
    if (!token || !circleId) {
      return;
    }
    try {
      await api(`/api/circles/${circleId}/join`, { method: 'POST', token, body: JSON.stringify({}) });
      setOk(detail?.visibility === 'public' ? 'Você entrou no círculo.' : 'Pedido de entrada enviado.');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível pedir entrada.');
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
    setOk('Dono transferido. Histórico e permissões ficam com o círculo.');
    await load();
  }

  async function revealSurprise() {
    if (!token || !circleId) {
      return;
    }
    const summary = await api<RevealSummary>(`/api/circles/${circleId}/reveal`, { method: 'POST', token });
    setReveal(summary);
    setOk('Surpresa revelada. O resumo com mensagens e fotos está visível.');
    await load();
  }

  async function keepMemory() {
    if (!token || !circleId) {
      return;
    }
    await api(`/api/circles/${circleId}/keep-memory`, { method: 'POST', token });
    setOk('Recordação guardada no seu perfil. A edição é silenciosa.');
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

  async function archive() {
    if (!token || !circleId) {
      return;
    }
    setError(null);
    try {
      await api(`/api/circles/${circleId}/archive`, { method: 'POST', token });
      setOk('Ciclo encerrado. A retrospectiva fica para consulta.');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível arquivar.');
    }
  }

  async function shareRecap() {
    if (!token || !circleId) {
      return;
    }
    setError(null);
    try {
      const created = await api<RecapShareCreated>(`/api/circles/${circleId}/recap/share`, { method: 'POST', token });
      setSharePath(created.path);
      setOk(`História compartilhada: ${created.path}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível compartilhar.');
    }
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
      <Text style={styles.title}>{detail?.name ?? 'Círculo'}</Text>
      <Text style={styles.lead}>{detail?.description}</Text>
      {detail ? (
        <Text style={styles.lead}>
          {CIRCLE_KIND_LABEL[detail.kind as CircleKind] ?? detail.kind} · {displayLabel(detail.status, CIRCLE_STATUS_LABEL)}
          {detail.endsAt ? ` · fim ${new Date(detail.endsAt).toLocaleDateString('pt-BR')}` : ''}
        </Text>
      ) : null}
      {detail?.status === 'archived' ? (
        <Text style={styles.lead}>Arquivo e retrospectiva ficam visíveis neste app, para qualquer plano.</Text>
      ) : null}
      {isLocked ? (
        <Text style={styles.lead}>Este círculo está travado. Leituras seguem; ninguém pública entrada nova.</Text>
      ) : null}
      {detail?.ownershipOffer ? (
        <>
          <Text style={styles.section}>Oferta de posse</Text>
          <Text style={styles.lead}>
            O sistema oferece a posse deste círculo para você. O primeiro que aceitar vira dono; os outros convites
            saem na hora.
          </Text>
          <Button
            label="Aceitar posse"
            onPress={() => {
              if (!token || !circleId) {
                return;
              }
              void api(`/api/circles/${circleId}/ownership-offer/accept`, { method: 'POST', token })
                .then(() => {
                  setOk('Você assumiu a posse deste círculo.');
                  return load();
                })
                .catch((err) =>
                  setError(err instanceof ApiError ? err.message : 'Não foi possível aceitar a posse.'),
                );
            }}
          />
        </>
      ) : null}
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />
      {token && circleId ? (
        <Button
          label="Denunciar"
          accessibilityLabel="Denunciar círculo"
          variant="ghost"
          onPress={() => {
            void reportTarget(token, 'circle', circleId)
              .then(() => setOk('Denúncia registrada.'))
              .catch((err) => setError(err instanceof ApiError ? err.message : 'Não foi possível denunciar.'));
          }}
        />
      ) : null}
      {isAdmin && detail?.isSurprise && !detail.revealed ? (
        <Button label="Revelar surpresa" onPress={() => void revealSurprise()} />
      ) : null}
      {detail?.isSurprise && reveal ? (
        <>
          <Text style={styles.section}>Resumo da revelação</Text>
          <Text style={styles.lead}>
            {reveal.messages.length} mensagens · {reveal.photoCount} fotos
          </Text>
          {reveal.messages.map((item) => (
            <Text key={item.postId} style={styles.item}>
              {item.body}
            </Text>
          ))}
          {reveal.revealedAt ? (
            <Button label="Guardar recordação no meu perfil" onPress={() => void keepMemory()} />
          ) : null}
        </>
      ) : null}

      {!isMember && !(detail?.isSurprise && detail.revealed) ? (
        <Button
          label={detail?.visibility === 'public' ? 'Entrar' : 'Pedir para entrar'}
          onPress={() => void join()}
        />
      ) : null}

      <Text style={styles.section}>Quem está aqui</Text>
      {detail?.members.map((member) => (
        <View key={member.userId} style={styles.card}>
          <Text style={styles.item}>
            {member.name} · {displayLabel(member.role, ROLE_LABEL)}
          </Text>
          {isOwner && member.userId !== user?.id ? (
            <Button label={`Transferir dono para ${member.name}`} onPress={() => void transferTo(member.userId)} />
          ) : null}
          {token && member.userId !== user?.id ? (
            <Button
              label="Denunciar"
              accessibilityLabel={`Denunciar perfil de ${member.name}`}
              variant="ghost"
              onPress={() => {
                void reportTarget(token, 'profile', member.userId)
                  .then(() => setOk('Denúncia registrada.'))
                  .catch((err) => setError(err instanceof ApiError ? err.message : 'Não foi possível denunciar.'));
              }}
            />
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
          <Button
            label="Enviar convite"
            onPress={() => void invite()}
            disabled={!inviteEmail.trim()}
            disabledHint="Informe o e-mail para enviar o convite."
          />
          {isAdmin ? <Button label="Criar link para WhatsApp" variant="ghost" onPress={() => void inviteLink()} /> : null}
          {inviteCode ? (
            <>
              <Text testID="invite-code" style={styles.lead}>
                Código do convite: {inviteCode}
              </Text>
              <Button
                label="Copiar"
                accessibilityLabel="Copiar código do convite"
                variant="ghost"
                onPress={() => {
                  void copyText(inviteCode).then((copied) => {
                    if (copied) {
                      setOk(`Código copiado: ${inviteCode}`);
                    }
                  });
                }}
              />
            </>
          ) : null}
          {shareText ? (
            <Text testID="invite-share-text" style={styles.lead}>
              {shareText}
            </Text>
          ) : null}
          {canPost ? (
            <>
              <TextField label="Nova entrada" value={post} onChangeText={setPost} />
              <Button
                label="Publicar"
                onPress={() => void publish()}
                disabled={!post.trim()}
                disabledHint="Escreva a entrada para publicar."
              />
            </>
          ) : null}
        </>
      ) : null}

      {(detail?.sharedEntries ?? []).length + (detail?.sharedRecords ?? []).length > 0 ? (
        <>
          <Text style={styles.section}>Entradas compartilhadas</Text>
          {(detail?.sharedEntries ?? []).map((item) => (
            <View key={item.id} style={styles.card}>
              <Text style={styles.item}>{item.title}</Text>
              {item.body ? <Text style={styles.lead}>{item.body}</Text> : null}
              <Text style={styles.lead}>De {item.authorName}. A mesma entrada, sem duplicar.</Text>
              {(item.tags ?? []).length > 0 ? (
                <Text style={styles.lead}>Tags: {(item.tags ?? []).join(', ')}</Text>
              ) : null}
            </View>
          ))}
          {(detail?.sharedRecords ?? []).map((item) => (
            <View key={item.id} style={styles.card}>
              <Text style={styles.item}>{item.title}</Text>
              {item.body ? <Text style={styles.lead}>{item.body}</Text> : null}
              <Text style={styles.lead}>De {item.authorName}. A mesma entrada, sem duplicar.</Text>
            </View>
          ))}
        </>
      ) : null}

      {giftIdeas.length > 0 ? (
        <>
          <Text style={styles.section}>Ideias de presente</Text>
          {giftIdeas.map((item) => (
            <View key={item.reminderId} style={styles.card}>
              <Text style={styles.lead}>
                {item.title}: {item.ideas.join(', ')}
              </Text>
              {canEditGiftIdeas ? (
                <>
                  <TextField
                    label={`Nova ideia para ${item.title}`}
                    value={shownGiftIdea}
                    onChangeText={(value) => {
                      giftIdeaDirty.current = true;
                      setGiftIdeaDraft(value);
                    }}
                  />
                  <Button
                    label="Adicionar ideia"
                    variant="ghost"
                    disabled={!shownGiftIdea.trim()}
                    onPress={() => {
                      if (!token || !circleId || !shownGiftIdea.trim()) {
                        return;
                      }
                      void api(`/api/circles/${circleId}/gift-ideas/${item.reminderId}/ideas`, {
                        method: 'POST',
                        token,
                        body: JSON.stringify({ idea: shownGiftIdea.trim() }),
                      })
                        .then(() => {
                          giftIdeaDirty.current = false;
                          setGiftIdeaDraft('');
                          return load();
                        })
                        .catch((err) =>
                          setError(err instanceof ApiError ? err.message : 'Não foi possível adicionar a ideia.'),
                        );
                    }}
                  />
                </>
              ) : (
                <Text style={styles.lead}>Quem só visualiza não edita ideias de presente.</Text>
              )}
            </View>
          ))}
        </>
      ) : null}

      {isAdmin && circleReports.length > 0 ? (
        <>
          <Text style={styles.section}>Denúncias do círculo</Text>
          {circleReports.map((item) => (
            <View key={item.id} style={styles.card}>
              <Text style={styles.item}>
                {displayLabel(item.targetKind, TARGET_KIND_LABEL)} · {displayLabel(item.status, REPORT_STATUS_LABEL)}
              </Text>
              {item.status !== 'circle-resolved' ? (
                <>
                  <Button
                    label="Manter conteúdo"
                    variant="ghost"
                    onPress={() => {
                      if (!token || !circleId) {
                        return;
                      }
                      void api(`/api/circles/${circleId}/reports/${item.id}/decide`, {
                        method: 'POST',
                        token,
                        body: JSON.stringify({ decision: 'keep' }),
                      }).then(() => load());
                    }}
                  />
                  <Button
                    label="Esconder conteúdo"
                    variant="ghost"
                    onPress={() => {
                      if (!token || !circleId) {
                        return;
                      }
                      void api(`/api/circles/${circleId}/reports/${item.id}/decide`, {
                        method: 'POST',
                        token,
                        body: JSON.stringify({ decision: 'hide' }),
                      }).then(() => load());
                    }}
                  />
                </>
              ) : (
                <Text style={styles.lead}>{displayLabel(item.circleDecision, REPORT_DECISION_LABEL)}</Text>
              )}
            </View>
          ))}
        </>
      ) : null}

      {detail?.posts.map((item) => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.item}>{item.body}</Text>
          {isMember && !isLocked ? <Button label="Apagar" variant="danger" onPress={() => void removePost(item.id)} /> : null}
          {token ? (
            <Button
              label="Denunciar"
              accessibilityLabel={`Denunciar post ${item.body}`}
              variant="ghost"
              onPress={() => {
                void reportTarget(token, 'post', item.id)
                  .then(() => setOk('Denúncia registrada.'))
                  .catch((err) => setError(err instanceof ApiError ? err.message : 'Não foi possível denunciar.'));
              }}
            />
          ) : null}
        </View>
      ))}

      {isMember ? (
        <>
          <Text style={styles.section}>Memórias do círculo</Text>
          {memories.map((item) => (
            <Text key={item.id} style={styles.item}>
              {item.title}
            </Text>
          ))}
          {canCreateList ? (
            <>
              <TextField label="Título da memória" value={memoryTitle} onChangeText={setMemoryTitle} />
              <Button label="Guardar memória no círculo" onPress={() => void createMemory()} disabled={!memoryTitle.trim()} />
            </>
          ) : null}
          <Text style={styles.section}>Perfis deste círculo</Text>
          {living.map((item) => (
            <View key={item.id}>
              <Text style={styles.lead}>
                {item.name} · {displayLabel(item.kind, LIVING_KIND_LABEL)} · {item.canEdit ? 'pode editar' : 'só visualiza'}
              </Text>
              {item.canEdit ? (
                <Button
                  label={`Renomear ${item.name}`}
                  variant="ghost"
                  onPress={() => {
                    if (!token) {
                      return;
                    }
                    void api(`/api/living-profiles/${item.id}`, {
                      method: 'PUT',
                      token,
                      body: JSON.stringify({ name: `${item.name} editado`, kind: item.kind, circleId }),
                    }).then(() => load());
                  }}
                />
              ) : null}
            </View>
          ))}
          <Text style={styles.section}>Listas do círculo</Text>
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
                label={`Permissão: ${LIST_ACCESS_LABEL[listAccess]}`}
                variant="ghost"
                onPress={() => setListAccess(nextOf(LIST_ACCESS, listAccess))}
              />
              <Button
                label="Criar lista"
                onPress={() => void createList()}
                disabled={!listName.trim()}
                disabledHint="Informe o nome da lista para criar."
              />
            </>
          ) : null}
        </>
      ) : null}

      <Text style={styles.section}>Log</Text>
      {detail?.log.map((item) => (
        <Text key={item.id} style={styles.lead}>
          {formatPersonTime(item.createdAt)} · {displayLabel(item.action, LOG_ACTION_LABEL)}: {item.detail}
        </Text>
      ))}

      {isMember && recap ? (
        <>
          <Text style={styles.section}>Minha retrospectiva</Text>
          <Text style={styles.lead}>Só a sua história neste círculo. Os dados dos outros não entram.</Text>
          {recap.flows.length === 0 ? <Text style={styles.lead}>Você ainda não registrou nada aqui.</Text> : null}
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
          <Button label="Compartilhar minha história" onPress={() => void shareRecap()} />
          {sharePath ? <Text style={styles.lead}>Link: {sharePath}</Text> : null}
        </>
      ) : null}

      {isAdmin && detail && canArchiveKind(detail.kind) && detail.status !== 'archived' ? (
        <Button label="Encerrar ciclo e arquivar" variant="ghost" onPress={() => void archive()} />
      ) : null}

      {detail ? (
        <Button
          label="Exportar este círculo"
          variant="ghost"
          onPress={() => {
            if (!token || !circleId) {
              return;
            }
            void downloadNamedBlob(`/api/export/circles/${circleId}`, `circulos-${circleId}.zip`, token)
              .then(() => setOk('Cópia do círculo baixada.'))
              .catch((err) => setError(err instanceof ApiError ? err.message : 'Não foi possível exportar.'));
          }}
        />
      ) : null}
      {vote && isMember ? (
        <>
          <Text style={styles.section}>Votação de sucessão</Text>
          <Text style={styles.lead}>
            Maioria simples, sem quorum. Prazo de 3 dias. Todos os membros votam. Status: {displayLabel(vote.status, SUCCESSION_STATUS_LABEL)}. Encerra em{' '}
            {formatPersonTime(vote.deadlineAt)}.
          </Text>
          {vote.replacementBlockedMessage ? (
            <Text style={styles.lead}>{vote.replacementBlockedMessage}</Text>
          ) : null}
          {vote.status === 'open'
            ? detail?.members.map((member) => (
                <Button
                  key={member.userId}
                  label={`Votar em ${member.name}`}
                  variant="ghost"
                  onPress={() => {
                    if (!token || !circleId) {
                      return;
                    }
                    void api(`/api/circles/${circleId}/succession/votes`, {
                      method: 'POST',
                      token,
                      body: JSON.stringify({ candidateUserId: member.userId }),
                    })
                      .then(() => load())
                      .catch((err) => setError(err instanceof ApiError ? err.message : 'Não foi possível votar.'));
                  }}
                />
              ))
            : null}
          {vote.canRequestReplacement ? (
            <Button
              label="Pedir nova votação"
              variant="ghost"
              onPress={() => {
                if (!token || !circleId) {
                  return;
                }
                void api(`/api/circles/${circleId}/succession/replace`, { method: 'POST', token })
                  .then(() => load())
                  .catch((err) =>
                    setError(err instanceof ApiError ? err.message : 'Não foi possível pedir nova votação.'),
                  );
              }}
            />
          ) : null}
        </>
      ) : null}
      <Text style={styles.section}>Filtro do log</Text>
      <TextField label="Ação do log" value={logFilter} onChangeText={setLogFilter} placeholder="criado" />
      <Button
        label="Filtrar log"
        variant="ghost"
        onPress={() => {
          if (!token || !circleId) {
            return;
          }
          const typed = logFilter.trim().toLowerCase();
          const action =
            Object.entries(LOG_ACTION_LABEL).find(([, label]) => label === typed)?.[0] ?? typed;
          const q = action ? `?action=${encodeURIComponent(action)}` : '';
          void api<CircleDetail['log']>(`/api/circles/${circleId}/log${q}`, { token }).then(setFilteredLog);
        }}
      />
      {filteredLog
        ? filteredLog.map((item) => (
            <Text key={item.id} style={styles.lead}>
              {formatPersonTime(item.createdAt)} · {displayLabel(item.action, LOG_ACTION_LABEL)}: {item.detail}
            </Text>
          ))
        : null}
      {isOwner ? (
        <>
          <Text style={styles.section}>Excluir círculo</Text>
          <Text style={styles.lead}>Exclusão definitiva. Não dá para desfazer. Escreva: excluir definitivo</Text>
          <TextField label="Confirmação da exclusão" value={deleteText} onChangeText={setDeleteText} />
          <Button
            label="Excluir círculo de vez"
            variant="danger"
            onPress={() => {
              if (!token || !circleId) {
                return;
              }
              void api(`/api/circles/${circleId}/delete`, {
                method: 'POST',
                token,
                body: JSON.stringify({ confirmation: deleteText.trim() }),
              })
                .then(() => router.push('/(app)/circles'))
                .catch((err) => setError(err instanceof ApiError ? err.message : 'Não foi possível excluir.'));
            }}
            disabled={deleteText.trim().toLowerCase() !== 'excluir definitivo'}
          />
          <Button label="Sair e abrir votação" variant="ghost" onPress={() => void leave()} />
        </>
      ) : null}
      {detail?.members.some((member) => member.userId === user?.id && member.role !== 'owner') ? (
        <Button label="Sair do círculo" variant="danger" onPress={() => void leave()} />
      ) : null}
    </ScrollView>
  );
}
