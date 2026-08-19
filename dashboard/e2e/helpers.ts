import { Page } from '@playwright/test';

/**
 * Injeta um token dummy em `sessionStorage` (chave usada por AuthService, ver
 * `src/app/core/auth/auth.service.ts`) antes de qualquer script da página rodar, para que
 * `authGuard` (`src/app/core/auth/auth.guard.ts`) libere a navegação para rotas protegidas
 * sem depender da API/Postgres estarem no ar. O guard não valida o JWT contra o backend de
 * forma síncrona — só verifica se existe um token — então chamadas de API feitas pela tela
 * podem falhar silenciosamente (404/401), o que é aceitável para o objetivo de screenshot de
 * layout/CSS (Gate Visual: pegar "classe existe mas não foi estilizada", não validar dado).
 */
export async function injectDummyAuth(page: Page): Promise<void> {
  await page.addInitScript(() => {
    sessionStorage.setItem('omuletachou_token', 'dummy-token-e2e-visual');
  });
}

/**
 * Bloqueia (aborta) todas as chamadas a `/api/**` feitas pela página. O token dummy injetado
 * por `injectDummyAuth` não é um JWT válido — se a API .NET estiver de fato no ar (ex.: stack
 * Docker local do Dev), ela responde 401 de verdade, e `authInterceptor`
 * (`src/app/core/auth/auth.interceptor.ts`) trata qualquer 401 fora de `/api/auth/login` como
 * sessão expirada, fazendo `AuthService.logout()` e redirecionando de volta para `/login` —
 * o que quebraria o screenshot da rota autenticada. Abortar a chamada (em vez de deixar
 * acontecer) resulta em erro de rede (status 0, não 401), que os componentes tratam como falha
 * silenciosa (spinner some, snackbar de erro), sem disparar o logout global — e deixa o teste
 * determinístico independente de a API estar ou não rodando localmente.
 */
export async function blockApiCalls(page: Page): Promise<void> {
  await page.route('**/api/**', route => route.abort());
}
