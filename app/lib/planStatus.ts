import { SUBSCRIPTION_STATUS_LABEL, displayLabel } from './labels.ts';

export function planStandingText(
  plan: string | undefined,
  status: string | undefined,
  inGoodStanding: boolean | undefined,
): string {
  if (plan !== 'paid') {
    return 'Plano gratuito. Sem cobrança e sem inadimplência.';
  }

  const label = displayLabel(status ?? 'none', SUBSCRIPTION_STATUS_LABEL) || 'nenhum';
  if (inGoodStanding) {
    return `Status: ${label}. Em dia.`;
  }

  return `Status: ${label}. Inadimplente: extras bloqueados.`;
}
