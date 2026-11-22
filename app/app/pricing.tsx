import { useEffect, useState } from 'react';
import { ScrollView, Text } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type PlanCatalog, type PlanInfo } from '@/lib/api';
import { screenStyles } from '@/lib/screenStyles';

function reais(value: number): string {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

export default function PricingScreen() {
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  const { token } = useAuth();
  const [catalog, setCatalog] = useState<PlanCatalog | null>(null);
  const [plan, setPlan] = useState<PlanInfo | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);

  useEffect(() => {
    void api<PlanCatalog>('/api/plans')
      .then(setCatalog)
      .catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao ler os precos.'));
  }, []);

  useEffect(() => {
    if (!token) {
      return;
    }
    void api<PlanInfo>('/api/plan', { token }).then(setPlan).catch(() => undefined);
  }, [token]);

  async function subscribe(period: 'monthly' | 'yearly') {
    if (!token) {
      router.push('/(auth)/register' as Href);
      return;
    }
    setError(null);
    try {
      const next = await api<PlanInfo>('/api/plan/checkout', {
        method: 'POST',
        token,
        body: JSON.stringify({ period, cardToken: 'fake-cartao-aprovado' }),
      });
      setPlan(next);
      setOk(next.sandbox ? 'Assinatura simulada no sandbox. Sem cobranca real.' : 'Assinatura ativa.');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Nao foi possivel assinar.');
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>Circulos</Text>
      <Text style={styles.title}>Precos</Text>
      <Text style={styles.lead}>
        Precos aprovados pelo Allan em 22/11/2026: R$ 29,90 mensal e R$ 299,00 anual. Valores em reais,
        configuraveis. Gateway Mercado Pago, sandbox por padrao, desligado em producao ate ligar o secret.
      </Text>
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />
      {catalog ? (
        <>
          <Text style={styles.lead}>Status dos precos: {catalog.status}. Provedor: {catalog.provider}.</Text>
          <Text style={styles.section}>{catalog.free.name}</Text>
          <Text style={styles.lead}>
            {reais(catalog.free.priceReais)} · {catalog.free.circleLimit} circulos · {catalog.free.shortcutLimit}{' '}
            atalhos · {Math.round(catalog.free.storageLimitBytes / (1024 * 1024))} MB
          </Text>
          <Text style={styles.section}>{catalog.paidMonthly.name}</Text>
          <Text style={styles.lead}>
            {reais(catalog.paidMonthly.priceReais)} por mes · circulos ilimitados · {catalog.paidMonthly.shortcutLimit}{' '}
            atalhos · 1 GB
          </Text>
          <Button label="Assinar mensal (sandbox)" onPress={() => void subscribe('monthly')} />
          <Text style={styles.section}>{catalog.paidYearly.name}</Text>
          <Text style={styles.lead}>{reais(catalog.paidYearly.priceReais)} por ano. Mesmos limites do mensal.</Text>
          <Button label="Assinar anual (sandbox)" variant="ghost" onPress={() => void subscribe('yearly')} />
        </>
      ) : null}
      {plan ? (
        <Text style={styles.lead}>
          Seu plano: {plan.plan === 'paid' ? 'pago' : 'gratuito'}. Status: {plan.subscriptionStatus}.{' '}
          {plan.inGoodStanding ? 'Em dia.' : 'Inadimplente: extras bloqueados.'}
        </Text>
      ) : null}
      <Button label="Voltar" variant="ghost" onPress={() => router.push(token ? '/(app)/home' : '/(auth)/login')} />
    </ScrollView>
  );
}
