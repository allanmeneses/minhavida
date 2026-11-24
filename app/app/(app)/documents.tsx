import { useCallback, useEffect, useState } from 'react';
import { Alert, Platform, Pressable, ScrollView, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import * as DocumentPicker from 'expo-document-picker';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import type { ColorPalette } from '@/constants/theme';
import { api, ApiError, downloadDocument, uploadNativeFile, type DocumentItem } from '@/lib/api';
import { MOBILE_SCROLL_BOTTOM } from '@/lib/screenStyles';

export default function DocumentsScreen() {
  const { token } = useAuth();
  const { colors } = useTheme();
  const { width } = useWindowDimensions();
  const stacked = Platform.OS !== 'web' || width < 768;
  const styles = documentStyles(colors, stacked);
  const [items, setItems] = useState<DocumentItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [actingId, setActingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setError(null);
    try {
      const list = await api<DocumentItem[]>('/api/documents', { token });
      setItems(list);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível listar os documentos.');
      setItems([]);
    }
  }, [token]);

  useEffect(() => {
    void load();
  }, [load]);

  async function upload() {
    if (!token) {
      return;
    }
    const picked = await DocumentPicker.getDocumentAsync({
      copyToCacheDirectory: true,
      multiple: false,
    });
    if (picked.canceled || !picked.assets?.[0]) {
      return;
    }

    const asset = picked.assets[0];
    setBusy(true);
    setError(null);
    try {
      if (Platform.OS === 'web' && asset.file) {
        const form = new FormData();
        form.append('file', asset.file, asset.name);
        await api('/api/documents', { method: 'POST', token, body: form });
      } else {
        await uploadNativeFile(
          '/api/documents',
          token,
          { uri: asset.uri, name: asset.name, mimeType: asset.mimeType },
          asset.name || 'arquivo',
        );
      }
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Não foi possível enviar o arquivo.');
    } finally {
      setBusy(false);
    }
  }

  async function download(item: DocumentItem) {
    if (!token) {
      return;
    }
    setActingId(item.id);
    setError(null);
    try {
      await downloadDocument(item.id, item.fileName, token);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível baixar o arquivo.');
    } finally {
      setActingId(null);
    }
  }

  function confirmDelete(item: DocumentItem) {
    if (Platform.OS === 'web') {
      if (globalThis.confirm(`Excluir ${item.fileName}? Esta ação não tem volta.`)) {
        void remove(item);
      }
      return;
    }
    Alert.alert('Excluir documento', `Apagar ${item.fileName} de forma permanente?`, [
      { text: 'Cancelar', style: 'cancel' },
      { text: 'Excluir', style: 'destructive', onPress: () => void remove(item) },
    ]);
  }

  async function remove(item: DocumentItem) {
    if (!token) {
      return;
    }
    setActingId(item.id);
    setError(null);
    try {
      await api(`/api/documents/${item.id}`, { method: 'DELETE', token });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível excluir o arquivo.');
    } finally {
      setActingId(null);
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <View style={styles.header}>
        <View style={styles.headerText}>
          <Text style={styles.kicker}>Área privada</Text>
          <Text style={styles.title}>Documentos pessoais</Text>
          <Text style={styles.lead}>
            Envie arquivos só seus. Ninguém mais vê, baixa ou apaga. Não existe link público permanente.
          </Text>
        </View>
        <Button label="Enviar arquivo" onPress={() => void upload()} loading={busy} />
      </View>
      <Feedback message={error} />
      {items === null ? <Text style={styles.muted}>Carregando documentos...</Text> : null}
      {items && items.length === 0 ? (
        <View style={styles.empty}>
          <Text style={styles.emptyTitle}>Nenhum documento ainda</Text>
          <Text style={styles.lead}>
            Envie um arquivo só seu, como um documento de identidade, um contrato ou uma foto guardada.
          </Text>
        </View>
      ) : null}
      {items && items.length > 0 ? (
        <View style={styles.list}>
          {!stacked ? (
            <View style={styles.tableHead}>
              <Text style={[styles.cellName, styles.headText]}>Arquivo</Text>
              <Text style={[styles.cellMeta, styles.headText]}>Tamanho</Text>
              <Text style={[styles.cellMeta, styles.headText]}>Enviado</Text>
              <Text style={[styles.cellActions, styles.headText]}>Ações</Text>
            </View>
          ) : null}
          {items.map((item) => (
            <View key={item.id} style={styles.row}>
              <View style={styles.cellName}>
                <Text style={styles.fileName} numberOfLines={2}>
                  {item.fileName}
                </Text>
                {stacked ? (
                  <Text style={styles.muted}>
                    {formatSize(item.sizeBytes)} · {formatDate(item.createdAt)}
                  </Text>
                ) : null}
              </View>
              {!stacked ? (
                <>
                  <Text style={styles.cellMeta}>{formatSize(item.sizeBytes)}</Text>
                  <Text style={styles.cellMeta}>{formatDate(item.createdAt)}</Text>
                </>
              ) : null}
              <View style={styles.cellActions}>
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel={`Baixar ${item.fileName}`}
                  onPress={() => void download(item)}
                  disabled={actingId === item.id}>
                  <Text style={styles.action}>Baixar</Text>
                </Pressable>
                <Pressable
                  accessibilityRole="button"
                  accessibilityLabel={`Excluir ${item.fileName}`}
                  onPress={() => confirmDelete(item)}
                  disabled={actingId === item.id}>
                  <Text style={styles.danger}>Excluir</Text>
                </Pressable>
              </View>
            </View>
          ))}
        </View>
      ) : null}
    </ScrollView>
  );
}

function formatSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleDateString('pt-BR');
}

const documentStyles = (colors: ColorPalette, stacked: boolean) => StyleSheet.create({
  page: {
    paddingBottom: MOBILE_SCROLL_BOTTOM,
    maxWidth: 920,
    width: '100%',
    alignSelf: 'center',
    gap: 16,
  },
  header: {
    gap: 16,
    flexDirection: stacked ? 'column' : 'row',
    justifyContent: 'space-between',
    alignItems: stacked ? 'stretch' : 'flex-end',
  },
  headerText: {
    flex: 1,
    gap: 6,
    paddingRight: 16,
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
  muted: {
    color: colors.muted,
  },
  empty: {
    backgroundColor: colors.surface,
    borderRadius: 16,
    padding: 24,
    borderWidth: 1,
    borderColor: colors.line,
    gap: 8,
  },
  emptyTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: colors.ink,
  },
  list: {
    backgroundColor: colors.surface,
    borderRadius: Platform.OS === 'web' ? 12 : 16,
    borderWidth: 1,
    borderColor: colors.line,
    overflow: 'hidden',
  },
  tableHead: {
    flexDirection: 'row',
    paddingHorizontal: 16,
    paddingVertical: 10,
    backgroundColor: colors.bg,
    borderBottomWidth: 1,
    borderBottomColor: colors.line,
  },
  headText: {
    color: colors.muted,
    fontWeight: '700',
    fontSize: 12,
    textTransform: 'uppercase',
  },
  row: {
    flexDirection: stacked ? 'column' : 'row',
    paddingHorizontal: 16,
    paddingVertical: 14,
    gap: 8,
    borderBottomWidth: 1,
    borderBottomColor: colors.line,
    alignItems: stacked ? 'stretch' : 'center',
  },
  cellName: {
    flex: stacked ? undefined : 2,
    minWidth: 0,
  },
  cellMeta: {
    flex: 1,
    color: colors.muted,
  },
  cellActions: {
    flexDirection: 'row',
    gap: 16,
    minWidth: 140,
    justifyContent: stacked ? 'flex-start' : 'flex-end',
  },
  fileName: {
    fontWeight: '700',
    color: colors.ink,
    flexShrink: 1,
  },
  action: {
    color: colors.primary,
    fontWeight: '700',
  },
  danger: {
    color: colors.danger,
    fontWeight: '700',
  },
});
