import { useEffect, useState } from 'react';
import { Image, Linking, ScrollView, Text, View } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { useTour } from '@/context/TourContext';
import { api, ApiError, downloadNamedBlob, type PlanInfo, type PrivacyItem, type TwoFactorSetup } from '@/lib/api';
import { t } from '@/lib/i18n';
import { API_URL } from '@/lib/config';
import { SUBSCRIPTION_STATUS_LABEL, THEME_LABEL, displayLabel } from '@/lib/labels';
import { daysPhrase } from '@/lib/plural';
import { screenStyles } from '@/lib/screenStyles';

export default function SettingsScreen() {
  const { token, logout, user } = useAuth();
  const router = useRouter();
  const tour = useTour();
  const { preference, setPreference, resolved, colors, highContrast, setHighContrast } = useTheme();
  const styles = screenStyles(colors);
  const [plan, setPlan] = useState<PlanInfo | null>(null);
  const [password, setPassword] = useState('');
  const [vacationDays, setVacationDays] = useState('7');
  const [twoFactor, setTwoFactor] = useState(false);
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null);
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [emailReminders, setEmailReminders] = useState(false);
  const [emailRemindersLoaded, setEmailRemindersLoaded] = useState(false);
  const [privacy, setPrivacy] = useState<PrivacyItem[]>([]);
  const [supportBody, setSupportBody] = useState('');

  useEffect(() => {
    if (!token) {
      return;
    }
    void api<PlanInfo>('/api/plan', { token }).then(setPlan).catch((err) => {
      setError(err instanceof ApiError ? err.message : 'Falha ao ler o plano.');
    });
    void api<{ enabled: boolean }>('/api/auth/2fa', { token })
      .then((status) => setTwoFactor(status.enabled))
      .catch(() => undefined);
    void api<{ enabled: boolean }>('/api/reminders/email-preference', { token })
      .then((status) => {
        setEmailReminders(status.enabled);
        setEmailRemindersLoaded(true);
      })
      .catch(() => setEmailRemindersLoaded(true));
    void api<{ items: PrivacyItem[] }>('/api/privacy/me', { token })
      .then((payload) => setPrivacy(payload.items))
      .catch(() => setPrivacy([]));
  }, [token]);

  async function vacation() {
    if (!token) {
      return;
    }
    const days = Math.max(1, Number(vacationDays.replace(',', '.')) || 7);
    const until = new Date(Date.now() + days * 24 * 3600_000).toISOString();
    await api('/api/reminders/vacation', { method: 'PUT', token, body: JSON.stringify({ until }) });
    setOk(`Modo férias ligado por ${daysPhrase(days)}. Lembretes ficam guardados.`);
  }

  async function requestDeletion() {
    if (!token || !password) {
      return;
    }
    setError(null);
    try {
      await api('/api/account/deletion-requests', {
        method: 'POST',
        token,
        body: JSON.stringify({ password }),
      });
      setOk('Enviamos um e-mail para confirmar a exclusão.');
      setPassword('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível pedir a exclusão.');
    }
  }

  async function startTwoFactor() {
    if (!token) {
      return;
    }
    setError(null);
    try {
      setSetup(await api<TwoFactorSetup>('/api/auth/2fa/setup', { method: 'POST', token }));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível iniciar o segundo fator.');
    }
  }

  async function enableTwoFactor() {
    if (!token || !code.trim()) {
      return;
    }
    try {
      await api('/api/auth/2fa/enable', { method: 'POST', token, body: JSON.stringify({ code: code.trim() }) });
      setTwoFactor(true);
      setOk('Segundo fator ativo. Guarde os códigos de recuperação.');
      setCode('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Código do autenticador inválido.');
    }
  }

  async function disableTwoFactor() {
    if (!token || !password || !code.trim()) {
      return;
    }
    try {
      await api('/api/auth/2fa/disable', {
        method: 'POST',
        token,
        body: JSON.stringify({ password, code: code.trim() }),
      });
      setTwoFactor(false);
      setSetup(null);
      setOk('Segundo fator desativado.');
      setCode('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível desativar o segundo fator.');
    }
  }

  async function exportProfile() {
    if (!token) {
      return;
    }
    await downloadNamedBlob('/api/export/me', 'circulos-perfil.zip', token);
    setOk('Cópia do perfil baixada.');
  }

  async function exportJson() {
    if (!token) {
      return;
    }
    await downloadNamedBlob('/api/export/me?format=json', 'circulos-perfil.json', token);
    setOk('JSON com todos os seus dados baixado.');
  }

  async function sendSupport() {
    if (!token || supportBody.trim().length < 8) {
      return;
    }
    setError(null);
    try {
      await api('/api/account/support', {
        method: 'POST',
        token,
        body: JSON.stringify({ body: supportBody.trim() }),
      });
      setSupportBody('');
      setOk('Recebemos seu pedido. Sem prazo prometido de resposta.');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível enviar o suporte.');
    }
  }

  const used = plan ? Math.round((plan.storageUsedBytes / plan.storageLimitBytes) * 100) : 0;
  const bar = Math.max(0, Math.min(100, used));

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.title}>Ajustes</Text>
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />

      <Text style={styles.section}>Aparência</Text>
      <Text style={styles.lead}>
        Agora: {displayLabel(resolved, THEME_LABEL)}. Preferência: {displayLabel(preference, THEME_LABEL)}.
      </Text>
      <Text style={styles.lead}>Contraste: {highContrast ? 'alto' : 'normal'}.</Text>
      <Button label="Seguir o aparelho" variant="ghost" onPress={() => void setPreference('system')} />
      <Button label="Claro" variant="ghost" onPress={() => void setPreference('light')} />
      <Button label="Escuro" variant="ghost" onPress={() => void setPreference('dark')} />
      <Button
        label={highContrast ? 'Contraste normal' : 'Contraste alto'}
        variant="ghost"
        onPress={() => void setHighContrast(!highContrast)}
      />

      <Text style={styles.section}>Plano</Text>
      <Text style={styles.lead}>
        {plan?.plan === 'paid' ? 'Pago' : 'Gratuito'}: {plan?.circleLimit ?? 'ilimitado'} círculos,{' '}
        {plan ? Math.round(plan.storageLimitBytes / (1024 * 1024)) : 0} MB, {plan?.shortcutLimit ?? 5} atalhos.
        Uso {used}%.
      </Text>
      <Text style={styles.lead}>
        Status: {displayLabel(plan?.subscriptionStatus ?? 'none', SUBSCRIPTION_STATUS_LABEL)}. {plan?.inGoodStanding ? 'Em dia.' : 'Inadimplente.'}{' '}
        {plan?.checkout}
      </Text>
      {plan?.sandbox ? (
        <Text style={[styles.lead, { fontSize: 12 }]}>Cobrança em ambiente de desenvolvimento.</Text>
      ) : null}
      <Button label="Ver preços" variant="ghost" onPress={() => router.push('/pricing' as Href)} />
      {plan?.plan === 'paid' ? (
        <Button
          label="Cancelar assinatura"
          variant="ghost"
          onPress={() => {
            if (!token) {
              return;
            }
            void api<PlanInfo>('/api/plan/cancel', { method: 'POST', token })
              .then((next) => {
                setPlan(next);
                setOk('Assinatura cancelada. O acesso pago segue até o fim do ciclo.');
              })
              .catch((err) => setError(err instanceof ApiError ? err.message : 'Não foi possível cancelar.'));
          }}
        />
      ) : null}
      <Text style={styles.section}>Uso de armazenamento</Text>
      <View
        accessibilityLabel={`Uso de armazenamento ${used} por cento`}
        style={{ height: 12, borderRadius: 6, backgroundColor: colors.line, overflow: 'hidden' }}>
        <View style={{ width: `${bar}%`, height: 12, backgroundColor: colors.primary }} />
      </View>
      <Text style={styles.lead}>
        {plan ? Math.round(plan.storageUsedBytes / 1024) : 0} KB de{' '}
        {plan ? Math.round(plan.storageLimitBytes / (1024 * 1024)) : 0} MB. Sem separar por tipo.
      </Text>

      <Text style={styles.section}>{t('privacy.title')}</Text>
      <View testID="privacy-overview">
        {privacy.map((item) => (
          <Text key={item.what} style={styles.lead}>
            {item.what}: {item.who}
          </Text>
        ))}
      </View>

      <Text style={styles.section}>Cópia de segurança</Text>
      <Button label="Exportar meu perfil" onPress={() => void exportProfile()} />
      <Button label={t('export.json')} variant="ghost" onPress={() => void exportJson()} />

      <Text style={styles.section}>Segundo fator</Text>
      <Text style={styles.lead}>
        {twoFactor ? 'Ativo. O login pede o código do autenticador.' : 'Opcional. Ative com QR no autenticador.'}
      </Text>
      {!twoFactor && !setup ? <Button label="Ativar segundo fator" onPress={() => void startTwoFactor()} /> : null}
      {setup && !twoFactor ? (
        <>
          <Image
            accessibilityLabel="QR do autenticador"
            source={{ uri: `data:image/png;base64,${setup.qrPngBase64}` }}
            style={{ width: 180, height: 180 }}
          />
          <Text style={styles.lead}>Segredo: {setup.secret}</Text>
          <Text style={styles.lead}>Códigos de recuperação: {setup.recoveryCodes.join(', ')}</Text>
          <TextField label="Código do autenticador" value={code} onChangeText={setCode} keyboardType="number-pad" />
          <Button
            label="Confirmar segundo fator"
            onPress={() => void enableTwoFactor()}
            disabled={!code.trim()}
            disabledHint="Informe o código do autenticador para confirmar."
          />
        </>
      ) : null}
      {setup && twoFactor ? (
        <Text style={styles.lead}>Códigos de recuperação: {setup.recoveryCodes.join(', ')}</Text>
      ) : null}
      {twoFactor ? (
        <>
          <TextField label="Código do autenticador" value={code} onChangeText={setCode} keyboardType="number-pad" />
          <TextField label="Senha para desativar" value={password} onChangeText={setPassword} secureTextEntry />
          <Button
            label="Desativar segundo fator"
            variant="danger"
            onPress={() => void disableTwoFactor()}
            disabled={!password || !code.trim()}
            disabledHint="Informe senha e código do autenticador para desativar."
          />
        </>
      ) : null}

      <Text style={styles.section}>Lembretes por e-mail</Text>
      <Text style={styles.lead}>
        Lembretes do círculo chegam só aos membros, no app e por e-mail se a pessoa ligar. Sem SMS. Padrão: só no
        aplicativo. O envio usa a caixa local, sem SMTP real.
      </Text>
      {emailRemindersLoaded ? (
        <Button
          label={emailReminders ? 'Lembrete por e-mail: ligado' : 'Lembrete por e-mail: só no app'}
          variant="ghost"
          onPress={() => {
            if (!token) {
              return;
            }
            const next = !emailReminders;
            void api<{ enabled: boolean }>('/api/reminders/email-preference', {
              method: 'PUT',
              token,
              body: JSON.stringify({ enabled: next }),
            })
              .then((status) => setEmailReminders(status.enabled))
              .catch((err) => setError(err instanceof ApiError ? err.message : 'Não foi possível salvar a preferência.'));
          }}
        />
      ) : null}

      <Text style={styles.section}>Férias</Text>
      <Text style={styles.lead}>Escolha por quantos dias as notificações ficam suspensas.</Text>
      <TextField label="Dias de férias" value={vacationDays} onChangeText={setVacationDays} keyboardType="number-pad" />
      <Button label="Silenciar férias" onPress={() => void vacation()} />

      {user?.isSystemAdmin ? (
        <Button label="Painel interno" variant="ghost" onPress={() => router.push('/(app)/admin' as Href)} />
      ) : null}

      <Text style={styles.section}>Ajuda</Text>
      <Text style={styles.lead}>Só por e-mail, sem prazo prometido.</Text>
      <TextField label="Mensagem para o suporte" value={supportBody} onChangeText={setSupportBody} />
      <Button
        label={t('support.send')}
        variant="ghost"
        onPress={() => void sendSupport()}
        disabled={supportBody.trim().length < 8}
        disabledHint="Escreva pelo menos 8 caracteres para enviar ao suporte."
      />
      <Button label="FAQ" variant="ghost" onPress={() => router.push('/(app)/faq' as Href)} />
      <Button label="Status do sistema" variant="ghost" onPress={() => void Linking.openURL(`${API_URL}/status`)} />
      <Button label="Ver tour de novo" variant="ghost" onPress={() => void tour.replay()} />

      <Text style={styles.section}>Documentos legais</Text>
      <Button label="Termos de uso" variant="ghost" onPress={() => router.push('/termos' as Href)} />
      <Button label="Política de privacidade" variant="ghost" onPress={() => router.push('/privacidade' as Href)} />

      <Text style={styles.section}>Excluir conta</Text>
      <Text style={styles.lead}>Pede senha e confirmação por e-mail. O rastro do círculo fica anônimo.</Text>
      <TextField label="Senha" value={password} onChangeText={setPassword} secureTextEntry />
      <Button
        label="Pedir exclusão"
        variant="danger"
        onPress={() => void requestDeletion()}
        disabled={!password}
        disabledHint="Informe a senha para pedir a exclusão da conta."
      />
      <Button label="Sair" variant="ghost" onPress={() => void logout()} />
    </ScrollView>
  );
}
