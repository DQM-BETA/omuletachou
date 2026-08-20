'use client';

import { useEffect } from 'react';

/**
 * Error Boundary de rota (`app/error.tsx`, contrato do Next.js App Router — Client Component
 * obrigatório). Defesa em profundidade documentada em design.md §"Investigação do bug do item 2":
 * antes desta correção, `website/app/` não tinha nenhum `error.tsx`/`global-error.tsx`, então
 * qualquer exceção não tratada (ex.: o `SecurityError` de `history.pushState` do bug do slider,
 * já corrigido na causa raiz em `FilterBar.tsx`) derrubava a árvore de componentes e caía no
 * fallback genérico do Next.js ("Application error..."), sem mensagem para o usuário.
 * Não é a correção do bug (essa é o estado local + debounce/router.replace do slider) — é rede de
 * segurança para qualquer exceção futura não tratada nesta árvore de rotas.
 */
export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    // eslint-disable-next-line no-console
    console.error(error);
  }, [error]);

  return (
    <main className="app-error" data-testid="app-error">
      <div className="app-error__content">
        <h1>Algo deu errado</h1>
        <p>Não foi possível carregar esta página. Tente novamente.</p>
        <button type="button" className="filter-bar__apply" onClick={() => reset()}>
          Tentar novamente
        </button>
      </div>
    </main>
  );
}
