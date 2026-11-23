import { useCallback, useEffect, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type AdminActionItem, type AdminReport, type AdminStats } from '@/lib/api';
import { screenStyles } from '@/lib/screenStyles';

export default function AdminScreen() {
  const { token, user } = useAuth();
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [reports, setReports] = useState<AdminReport[]>([]);
  const [actions, setActions] = useState<AdminActionItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!token) {
      return;
    }
    setStats(await api<AdminStats>('/api/admin/stats', { token }));
    setReports(await api<AdminReport[]>('/api/admin/reports', { token }));
    setActions(await api<AdminActionItem[]>('/api/admin/actions', { token }));
  }, [token]);

  useEffect(() => {
    if (!user?.isSystemAdmin) {
      return;
    }
    void load().catch((err) => setError(err instanceof ApiError ? err.message : 'Nao foi possivel abrir o painel.'));
  }, [load, user?.isSystemAdmin]);

  async function setBlock(targetKind: string, targetId: string, blocked: boolean) {
    if (!token) {
      return;
    }
    setError(null);
    try {
      await api('/api/admin/blocks', {
        method: 'POST',
        token,
        body: JSON.stringify({ targetKind, targetId, blocked }),
      });
      setOk(blocked ? 'Bloqueado.' : 'Desbloqueado.');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel alterar o bloqueio.');
    }
  }

  if (!user?.isSystemAdmin) {
    return (
      <ScrollView contentContainerStyle={styles.page}>
        <Text style={styles.title}>Painel interno</Text>
        <Text style={styles.lead}>So o admin do sistema ve este painel.</Text>
      </ScrollView>
    );
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Painel interno</Text>
      <Text style={styles.lead}>
        Contagem agregada de uso, sem conteudo e sem identificar ninguem. Nao sai do sistema.
      </Text>
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />
      {stats ? (
        <View style={styles.card}>
          <Text style={styles.section}>Uso de recursos</Text>
          <Text style={styles.item}>Atalhos disparados: {stats.shortcutsTriggered}</Text>
          <Text style={styles.item}>Entradas criadas: {stats.entriesCreated}</Text>
          <Text style={styles.item}>Circulos ativos: {stats.activeCircles}</Text>
        </View>
      ) : null}

      <Text style={styles.section}>Denuncias</Text>
      {reports.length === 0 ? <Text style={styles.lead}>Nenhuma denuncia.</Text> : null}
      {reports.map((item) => (
        <View key={item.id} style={styles.card}>
          <Text style={styles.item}>
            {item.targetKind} · {item.targetId}
          </Text>
          <Text style={styles.lead}>
            {item.reporterEmail} · {new Date(item.createdAt).toLocaleString('pt-BR')}
          </Text>
          {item.targetKind === 'profile' || item.targetKind === 'circle' ? (
            <>
              <Button
                label={`Bloquear ${item.targetKind}`}
                variant="danger"
                onPress={() => void setBlock(item.targetKind, item.targetId, true)}
              />
              <Button
                label={`Desbloquear ${item.targetKind}`}
                variant="ghost"
                onPress={() => void setBlock(item.targetKind, item.targetId, false)}
              />
            </>
          ) : null}
        </View>
      ))}

      <Text style={styles.section}>Registro</Text>
      {actions.map((item) => (
        <Text key={item.id} style={styles.lead}>
          {item.action} · {item.targetKind} · {item.detail}
        </Text>
      ))}
    </ScrollView>
  );
}
