// Mapeamento enum -> texto de exibição (definido pelo UX/UI, ver
// documentacoes/ISSUE-229-exibir-tag-pequena-de-plataforma/ux-ui-spec.md). Nome completo, sem
// abreviação; nunca exibir o valor bruto do enum na tela. Fonte única compartilhada entre
// `DealCard` e `DealDetail` (CA 8 — texto/estilo idênticos entre telas para o mesmo produto).
export const PLATFORM_LABELS: Record<string, string> = {
  Amazon: 'Amazon',
  MercadoLivre: 'Mercado Livre',
  Shopee: 'Shopee',
};

/**
 * Resolve o texto de exibição da tag de plataforma a partir do valor bruto do enum.
 * Retorna `undefined` quando `platform` está ausente/`null` ou não está mapeado — nesses casos
 * a tag não deve ser renderizada (CA 4 e CA 5).
 */
export function getPlatformLabel(platform: string | null | undefined): string | undefined {
  return platform ? PLATFORM_LABELS[platform] : undefined;
}
