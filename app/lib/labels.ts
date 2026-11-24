export function displayLabel(value: string | null | undefined, map: Record<string, string>): string {
  if (!value) {
    return '';
  }
  return map[value] ?? map[value.toLowerCase()] ?? value;
}

export const CIRCLE_STATUS_LABEL: Record<string, string> = {
  active: 'Ativo',
  archived: 'Arquivado',
  locked: 'Travado',
  deleted: 'Excluído',
  blocked: 'Bloqueado',
};

export const ROLE_LABEL: Record<string, string> = {
  owner: 'Dono',
  admin: 'Admin',
  editor: 'Editor',
  commenter: 'Comentarista',
  viewer: 'Leitor',
};

export const VISIBILITY_LABEL: Record<string, string> = {
  private: 'Privado',
  public: 'Público',
};

export const TARGET_KIND_LABEL: Record<string, string> = {
  post: 'Publicação',
  profile: 'Perfil',
  circle: 'Círculo',
  user: 'Pessoa',
};

export const REPORT_STATUS_LABEL: Record<string, string> = {
  open: 'Aberta',
  closed: 'Fechada',
  'circle-resolved': 'Decidida no círculo',
};

export const REPORT_DECISION_LABEL: Record<string, string> = {
  keep: 'Mantido',
  hide: 'Escondido',
};

export const APPEAL_STATUS_LABEL: Record<string, string> = {
  open: 'Aberto',
  accepted: 'Aceito',
  rejected: 'Recusado',
};

export const SUBSCRIPTION_STATUS_LABEL: Record<string, string> = {
  active: 'Ativa',
  past_due: 'Em atraso',
  suspended: 'Suspensa',
  cancelled: 'Cancelada',
  none: 'Nenhuma',
};

export const CATALOG_STATUS_LABEL: Record<string, string> = {
  approved: 'Aprovado',
  aprovado: 'Aprovado',
  draft: 'Rascunho',
  rascunho: 'Rascunho',
  sandbox: 'Ambiente de teste',
};

export const FREQUENCY_LABEL: Record<string, string> = {
  none: 'nenhuma',
  daily: 'diária',
  weekly: 'semanal',
  monthly: 'mensal',
  yearly: 'anual',
};

export const THEME_LABEL: Record<string, string> = {
  system: 'aparelho',
  light: 'claro',
  dark: 'escuro',
};

export const LIVING_KIND_LABEL: Record<string, string> = {
  person: 'Pessoa',
  pet: 'Pet',
  group: 'Grupo',
};

export const ADMIN_ACTION_LABEL: Record<string, string> = {
  block: 'bloqueio',
  unblock: 'desbloqueio',
  blocked: 'bloqueado',
  unblocked: 'desbloqueado',
  'appeal-accept': 'recurso aceito',
  'appeal-reject': 'recurso recusado',
};

export const LOG_ACTION_LABEL: Record<string, string> = {
  created: 'criado',
  invited: 'convidado',
  joined: 'entrou',
  transferred: 'transferido',
  'deleted-forever': 'excluído de vez',
  posted: 'publicado',
  deleted: 'apagado',
  revealed: 'revelado',
  archived: 'arquivado',
  'report-decided': 'denúncia decidida',
  'owner-lost': 'sem dono',
  'shortcut-run': 'atalho disparado',
  memory: 'memória',
  'succession-opened': 'votação aberta',
  'succession-expired': 'votação encerrada',
  'ownership-offer-skipped': 'oferta de posse ignorada',
  'ownership-offered': 'oferta de posse enviada',
};

export const SUCCESSION_STATUS_LABEL: Record<string, string> = {
  open: 'aberta',
  closed: 'encerrada',
  expired: 'encerrada',
  completed: 'concluída',
};

export const GRANT_ACCESS_LABEL: Record<string, string> = {
  view: 'só visualiza',
  edit: 'pode editar',
};

export const AREA_KIND_LABEL: Record<string, string> = {
  vaccine: 'Vacina',
  reminder: 'Lembrete',
  document: 'Documento',
  diary: 'Diário',
  custom: 'Livre',
};
