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
    setOk(`Convite criado. Codigo: ${result.token}`);
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
    setOk('Voce saiu. Sem rastro para os outros.');
    router.push('/(app)/circles');
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
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel arquivar.');
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
      setOk(`Historia compartilhada: ${created.path}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel compartilhar.');
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
      <Text style={styles.title}>{detail?.name ?? 'Circulo'}</Text>
      <Text style={styles.lead}>{detail?.description}</Text>
      {detail ? (
        <Text style={styles.lead}>
          {CIRCLE_KIND_LABEL[detail.kind as CircleKind] ?? detail.kind} · {detail.status}
          {detail.endsAt ? ` · fim ${new Date(detail.endsAt).toLocaleDateString('pt-BR')}` : ''}
        </Text>
      ) : null}
      {detail?.status === 'archived' ? (
        <Text style={styles.lead}>Arquivo e retrospectiva ficam visiveis neste app, para qualquer plano.</Text>
      ) : null}
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />
      {token && circleId ? (
        <Button
          label="Denunciar circulo"
          variant="ghost"
          onPress={() => {
            void reportTarget(token, 'circle', circleId)
              .then(() => setOk('Denuncia registrada.'))
              .catch((err) => setError(err instanceof ApiError ? err.message : 'Nao foi possivel denunciar.'));
          }}
        />
      ) : null}
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
          {token && member.userId !== user?.id ? (
            <Button
              label={`Denunciar perfil de ${member.name}`}
              variant="ghost"
              onPress={() => {
                void reportTarget(token, 'profile', member.userId)
                  .then(() => setOk('Denuncia registrada.'))
                  .catch((err) => setError(err instanceof ApiError ? err.message : 'Nao foi possivel denunciar.'));
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
          <Button label="Enviar convite" onPress={() => void invite()} disabled={!inviteEmail.trim()} />
          {isAdmin ? <Button label="Criar link para WhatsApp" variant="ghost" onPress={() => void inviteLink()} /> : null}
          {shareText ? (
            <Text testID="invite-share-text" style={styles.lead}>
              {shareText}
            </Text>
          ) : null}
          <TextField label="Nova entrada" value={post} onChangeText={setPost} />
          <Button label="Publicar" onPress={() => void publish()} disabled={!post.trim()} />
        </>
      ) : null}

      {(detail?.sharedEntries ?? []).length > 0 ? (
        <>
          <Text style={styles.section}>Entradas compartilhadas</Text>
          {(detail?.sharedEntries ?? []).map((item) => (
            <View key={item.id} style={styles.card}>
              <Text style={styles.item}>{item.title}</Text>
              <Text style={styles.lead}>De {item.authorName}. A mesma entrada, sem duplicar.</Text>
              {(item.tags ?? []).length > 0 ? (
                <Text style={styles.lead}>Tags: {(item.tags ?? []).join(', ')}</Text>
              ) : null}
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
                          setError(err instanceof ApiError ? err.message : 'Nao foi possivel adicionar a ideia.'),
                        );
                    }}
                  />
                </>
              ) : (
                <Text style={styles.lead}>Quem so visualiza nao edita ideias de presente.</Text>
              )}
            </View>
          ))}
        </>
      ) : null}

      {isAdmin && circleReports.length > 0 ? (
        <>
          <Text style={styles.section}>Denuncias do circulo</Text>
          {circleReports.map((item) => (
            <View key={item.id} style={styles.card}>
              <Text style={styles.item}>
                {item.targetKind} · {item.status}
              </Text>
              {item.status !== 'circle-resolved' ? (
                <>
                  <Button
                    label="Manter conteudo"
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
                    label="Esconder conteudo"
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
                <Text style={styles.lead}>{item.circleDecision}</Text>
              )}
            </View>
          ))}
        </>
      ) : null}

      {detail?.posts.map((item) => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.item}>{item.body}</Text>
          {isMember ? <Button label="Apagar" variant="danger" onPress={() => void removePost(item.id)} /> : null}
          {token ? (
            <Button
              label={`Denunciar post ${item.body}`}
              variant="ghost"
              onPress={() => {
                void reportTarget(token, 'post', item.id)
                  .then(() => setOk('Denuncia registrada.'))
                  .catch((err) => setError(err instanceof ApiError ? err.message : 'Nao foi possivel denunciar.'));
              }}
            />
          ) : null}
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
            <View key={item.id}>
              <Text style={styles.lead}>
                {item.name} · {item.kind} · {item.canEdit ? 'pode editar' : 'so visualiza'}
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

      {isMember && recap ? (
        <>
          <Text style={styles.section}>Minha retrospectiva</Text>
          <Text style={styles.lead}>So a sua historia neste circulo. Os dados dos outros nao entram.</Text>
          {recap.flows.length === 0 ? <Text style={styles.lead}>Voce ainda nao registrou nada aqui.</Text> : null}
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
          <Button label="Compartilhar minha historia" onPress={() => void shareRecap()} />
          {sharePath ? <Text style={styles.lead}>Link: {sharePath}</Text> : null}
        </>
      ) : null}

      {isAdmin && detail && canArchiveKind(detail.kind) && detail.status !== 'archived' ? (
        <Button label="Encerrar ciclo e arquivar" variant="ghost" onPress={() => void archive()} />
      ) : null}

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
      {vote && isMember ? (
        <>
          <Text style={styles.section}>Votacao de sucessao</Text>
          <Text style={styles.lead}>
            Maioria simples, sem quorum. Prazo de 3 dias. Todos os membros votam. Status: {vote.status}. Encerra em{' '}
            {new Date(vote.deadlineAt).toLocaleString('pt-BR')}.
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
                      .catch((err) => setError(err instanceof ApiError ? err.message : 'Nao foi possivel votar.'));
                  }}
                />
              ))
            : null}
          {vote.canRequestReplacement ? (
            <Button
              label="Pedir nova votacao"
              variant="ghost"
              onPress={() => {
                if (!token || !circleId) {
                  return;
                }
                void api(`/api/circles/${circleId}/succession/replace`, { method: 'POST', token })
                  .then(() => load())
                  .catch((err) =>
                    setError(err instanceof ApiError ? err.message : 'Nao foi possivel pedir nova votacao.'),
                  );
              }}
            />
          ) : null}
        </>
      ) : null}
      <Text style={styles.section}>Filtro do log</Text>
      <TextField label="Acao do log" value={logFilter} onChangeText={setLogFilter} placeholder="created" />
      <Button
        label="Filtrar log"
        variant="ghost"
        onPress={() => {
          if (!token || !circleId) {
            return;
          }
          const q = logFilter.trim() ? `?action=${encodeURIComponent(logFilter.trim())}` : '';
          void api<CircleDetail['log']>(`/api/circles/${circleId}/log${q}`, { token }).then(setFilteredLog);
        }}
      />
      {filteredLog
        ? filteredLog.map((item) => (
            <Text key={item.id} style={styles.lead}>
              {formatPersonTime(item.createdAt)} · {item.action}: {item.detail}
            </Text>
          ))
        : null}
      {isOwner ? (
        <>
          <Text style={styles.section}>Excluir circulo</Text>
          <Text style={styles.lead}>Exclusao definitiva. Nao da para desfazer. Escreva: excluir definitivo</Text>
          <TextField label="Confirmacao da exclusao" value={deleteText} onChangeText={setDeleteText} />
          <Button
            label="Excluir circulo de vez"
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
                .catch((err) => setError(err instanceof ApiError ? err.message : 'Nao foi possivel excluir.'));
            }}
            disabled={deleteText.trim().toLowerCase() !== 'excluir definitivo'}
          />
          <Button label="Sair e abrir votacao" variant="ghost" onPress={() => void leave()} />
        </>
      ) : null}
      {detail?.members.some((member) => member.userId === user?.id && member.role !== 'owner') ? (
        <Button label="Sair do circulo" variant="danger" onPress={() => void leave()} />
      ) : null}
    </ScrollView>
  );
}
