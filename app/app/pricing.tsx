import { useEffect, useState } from 'react';
import { ScrollView, Text } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Feedback } from '@/components/ui/Feedback';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';
import { api, ApiError, type PlanCatalog, type PlanInfo } from '@/lib/api';
import { CATALOG_STATUS_LABEL, displayLabel } from '@/lib/labels';
import { planStandingText } from '@/lib/planStatus';
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
      .catch((err) => setError(err instanceof ApiError ? err.message : 'Falha ao ler os preços.'));
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
      setOk('Assinatura ativa.');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Não foi possível assinar.');
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>Círculos</Text>
      <Text style={styles.title}>Preços</Text>
      <Text style={styles.lead}>
        Preços aprovados pelo Allan em 22/11/2026: R$ 29,90 mensal e R$ 299,00 anual. Valores em reais,
        configuráveis. Pagamento por Mercado Pago. Em desenvolvimento a cobrança real fica desligada.
      </Text>
      <Feedback message={error} />
      <Feedback message={ok} tone="ok" />
      {catalog ? (
        <>
          <Text style={styles.lead}>
            Status dos preços: {displayLabel(catalog.status, CATALOG_STATUS_LABEL)}.
          </Text>
          {catalog.sandbox ? (
            <Text style={[styles.lead, { fontSize: 12 }]}>Ambiente de desenvolvimento. Sem cobrança real.</Text>
          ) : null}
          <Text style={styles.section}>{catalog.free.name}</Text>
          <Text style={styles.lead}>
            {reais(catalog.free.priceReais)} · {catalog.free.circleLimit} círculos · {catalog.free.shortcutLimit}{' '}
            atalhos · {Math.round(catalog.free.storageLimitBytes / (1024 * 1024))} MB
          </Text>
          <Text style={styles.section}>{catalog.paidMonthly.name}</Text>
          <Text style={styles.lead}>
            {reais(catalog.paidMonthly.priceReais)} por mês · círculos ilimitados · {catalog.paidMonthly.shortcutLimit}{' '}
            atalhos · 1 GB
          </Text>
          <Button label="Assinar mensal" onPress={() => void subscribe('monthly')} />
          <Text style={styles.section}>{catalog.paidYearly.name}</Text>
          <Text style={styles.lead}>{reais(catalog.paidYearly.priceReais)} por ano. Mesmos limites do mensal.</Text>
          <Button label="Assinar anual" variant="ghost" onPress={() => void subscribe('yearly')} />
        </>
      ) : null}
      {plan ? (
        <Text style={styles.lead}>
          Seu plano: {plan.plan === 'paid' ? 'pago' : 'gratuito'}. {planStandingText(plan.plan, plan.subscriptionStatus, plan.inGoodStanding)}
        </Text>
      ) : null}
      <Button label="Voltar" variant="ghost" onPress={() => router.push(token ? '/(app)/home' : '/(auth)/login')} />
    </ScrollView>
  );
}
