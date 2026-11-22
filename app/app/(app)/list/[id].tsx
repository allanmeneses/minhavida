import { useCallback, useEffect, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { useLocalSearchParams } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useOffline } from '@/context/OfflineContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type SharedListDetail, type SharedListItem } from '@/lib/api';
import { LIST_ACCESS, LIST_ACCESS_LABEL, LIST_KIND_LABEL, nextOf, type ListAccess, type ListKind } from '@/lib/listKinds';
import { screenStyles } from '@/lib/screenStyles';

export default function ListDetailScreen() {
  const params = useLocalSearchParams<{ id: string | string[] }>();
  const listId = Array.isArray(params.id) ? params.id[0] : params.id;
  const { token } = useAuth();
  const { online, warning, enqueueAdd, enqueueComplete, syncQueue, clearWarning } = useOffline();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [detail, setDetail] = useState<SharedListDetail | null>(null);
  const [text, setText] = useState('');
  const [quantity, setQuantity] = useState('');
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token || !listId) {
      return;
    }
    setDetail(await api<SharedListDetail>(`/api/lists/${listId}`, { token }));
  }, [token, listId]);

  useEffect(() => {
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Lista nao encontrada.'));
    const timer = setInterval(() => {
      void load().catch(() => undefined);
    }, 5000);
    return () => clearInterval(timer);
  }, [load]);

  async function add() {
    if (!token || !listId || !text.trim()) {
      return;
    }
    const payload = { text: text.trim(), quantity: quantity.trim() || null, note: note.trim() || null };
    if (!online) {
      const action = enqueueAdd(listId, payload);
      setDetail((current) =>
        current
          ? {
              ...current,
              items: [
                ...current.items,
                {
                  id: action.id,
                  text: payload.text,
                  quantity: payload.quantity,
                  note: payload.note,
                  isDone: false,
                  sortOrder: current.items.length,
                  addedByUserId: null,
                  addedByName: 'voce',
                  doneByUserId: null,
                  doneByName: null,
                },
              ],
            }
          : current,
      );
      setText('');
      setQuantity('');
      setNote('');
      return;
    }
    try {
      await api(`/api/lists/${listId}/items`, {
        method: 'POST',
        token,
        body: JSON.stringify(payload),
      });
    } catch (err) {
      if (err instanceof ApiError && err.status === 0) {
        enqueueAdd(listId, payload);
      } else {
        throw err;
      }
    }
    setText('');
    setQuantity('');
    setNote('');
    await load();
  }

  async function toggle(item: SharedListItem) {
    if (!token || !listId) {
      return;
    }
    if (!online) {
      enqueueComplete(listId, item.id);
      setDetail((current) =>
        current
          ? {
              ...current,
              items: current.items.map((entry) =>
                entry.id === item.id
                  ? { ...entry, isDone: !entry.isDone, doneByName: entry.isDone ? null : 'voce' }
                  : entry,
              ),
            }
          : current,
      );
      return;
    }
    try {
      await api(`/api/lists/${listId}/items/${item.id}/done`, { method: 'POST', token });
    } catch (err) {
      if (err instanceof ApiError && err.status === 0) {
        enqueueComplete(listId, item.id);
        return;
      }
      throw err;
    }
    await load();
  }

  async function remove(item: SharedListItem) {
    if (!token || !listId) {
      return;
    }
    await api(`/api/lists/${listId}/items/${item.id}`, { method: 'DELETE', token });
    await load();
  }

  async function move(item: SharedListItem, direction: -1 | 1) {
    if (!token || !listId || !detail) {
      return;
    }
    const ordered = [...detail.items];
    const index = ordered.findIndex((entry) => entry.id === item.id);
    const next = index + direction;
    if (index < 0 || next < 0 || next >= ordered.length) {
      return;
    }
    const swap = ordered[index];
    ordered[index] = ordered[next];
    ordered[next] = swap;
    await api(`/api/lists/${listId}/items/order`, {
      method: 'PUT',
      token,
      body: JSON.stringify({ ids: ordered.map((entry) => entry.id) }),
    });
    await load();
  }

  async function cycleAccess() {
    if (!token || !listId || !detail) {
      return;
    }
    const current = (LIST_ACCESS.includes(detail.memberAccess as ListAccess) ? detail.memberAccess : 'view') as ListAccess;
    await api(`/api/lists/${listId}/access`, {
      method: 'PUT',
      token,
      body: JSON.stringify({ memberAccess: nextOf(LIST_ACCESS, current) }),
    });
    await load();
  }

  async function cycleGrant(userId: string, current: string) {
    if (!token || !listId) {
      return;
    }
    const access = (LIST_ACCESS.includes(current as ListAccess) ? current : 'view') as ListAccess;
    await api(`/api/lists/${listId}/grants`, {
      method: 'PUT',
      token,
      body: JSON.stringify({ userId, access: nextOf(LIST_ACCESS, access) }),
    });
    await load();
  }

  const kindLabel = detail ? LIST_KIND_LABEL[detail.kind as ListKind] ?? detail.kind : '';
  const accessLabel = detail
    ? LIST_ACCESS_LABEL[detail.memberAccess as ListAccess] ?? detail.memberAccess
    : '';

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>{detail?.name ?? 'Lista'}</Text>
      <Text style={styles.lead}>
        {kindLabel}. {accessLabel}. Quem adicionou e quem marcou ficam no item.
      </Text>
      <Text style={styles.lead}>{online ? 'Online' : 'Offline'}</Text>
      <Feedback message={error} />
      <Feedback message={warning} />
      {warning ? <Button label="Esconder aviso da fila" variant="ghost" onPress={clearWarning} /> : null}
      {!online ? (
        <Button label="Sincronizar ao voltar" variant="ghost" onPress={() => void syncQueue()} />
      ) : (
        <Button label="Sincronizar fila" variant="ghost" onPress={() => void syncQueue()} />
      )}
      {detail?.canChangeAccess ? (
        <Button label={`Permissao dos membros: ${accessLabel}`} variant="ghost" onPress={() => void cycleAccess()} />
      ) : null}
      {detail?.canChangeAccess
        ? detail.members.map((member) => (
            <Button
              key={member.userId}
              label={`Permissao de ${member.name}: ${LIST_ACCESS_LABEL[member.access as ListAccess] ?? member.access}`}
              variant="ghost"
              onPress={() => void cycleGrant(member.userId, member.access)}
            />
          ))
        : null}
      {detail?.canAdd ? (
        <>
          <TextField label="Item" value={text} onChangeText={setText} placeholder="Arroz" />
          <TextField label="Quantidade" value={quantity} onChangeText={setQuantity} placeholder="2 kg" />
          <TextField label="Observacao" value={note} onChangeText={setNote} placeholder="tipo 1" />
          <Button label="Adicionar item" onPress={() => void add()} disabled={!text.trim()} />
        </>
      ) : (
        <Text style={styles.lead}>Voce so ve esta lista.</Text>
      )}
      {detail?.items.map((item) => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.item}>{item.text}</Text>
          {item.quantity ? <Text style={styles.lead}>Qtd: {item.quantity}</Text> : null}
          {item.note ? <Text style={styles.lead}>{item.note}</Text> : null}
          <Text style={styles.lead}>
            {item.addedByName ? `adicionou ${item.addedByName}` : 'adicionado'}
            {item.isDone ? ` · feito por ${item.doneByName ?? 'alguem'}` : ''}
          </Text>
          {detail.canComplete ? (
            <Button
              label={item.isDone ? `Desmarcar ${item.text}` : `Marcar ${item.text}`}
              variant="ghost"
              onPress={() => void toggle(item)}
            />
          ) : null}
          {detail.canManageItems ? (
            <>
              <Button label={`Subir ${item.text}`} variant="ghost" onPress={() => void move(item, -1)} />
              <Button label={`Descer ${item.text}`} variant="ghost" onPress={() => void move(item, 1)} />
              <Button label={`Excluir ${item.text}`} variant="danger" onPress={() => void remove(item)} />
            </>
          ) : null}
        </View>
      ))}
    </ScrollView>
  );
}
