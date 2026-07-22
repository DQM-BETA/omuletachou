# Especificação Técnica — ISSUE-13: Dashboard Angular (Todas as Páginas Admin)

Fecha os contratos técnicos que Sub-A a Sub-D devem seguir. Não redecide nada de `proposal.md`/`criterios-aceite.md` — concretiza em estrutura de arquivos, tipos TypeScript e contratos HTTP.

Verificado por inspeção direta do código: `dashboard/` (scaffold Issue #17, sem `services/`, sem lib de UI, sem `provideHttpClient`) e dos controllers reais da API em `backend/src/AfiliadoBot.Api/Controllers/*.cs` (Issue #11, já em `main`).

---

## 0. Decisão de biblioteca de UI: Angular Material

**Escolhida: Angular Material** (não PrimeNG).

Justificativa (breve, ferramenta interna sem exigência de marca):
- Integração de primeira parte com Angular 17 standalone (`provideAnimationsAsync`, schematics `ng add @angular/material` configuram tudo automaticamente — tema, tipografia, `BrowserAnimationsModule`).
- CDK (`@angular/cdk`) cobre exatamente os componentes que este dashboard precisa: `MatTable` + `MatSort` + `MatPaginator` (Products/Queue/Reports), `MatFormField`/`MatInput` (Settings/Login), `MatButtonToggle`/`MatChip` (badges de status), `MatDialog` (confirmações de aprovar/rejeitar, se necessário), `MatSnackBar` (mensagens de sessão expirada, sucesso/erro de save).
- Menor superfície de manutenção a longo prazo (mesmo ecossistema/versionamento do Angular CLI) do que PrimeNG, que tem ciclo de release próprio e exige `primeflex`/`primeicons` como dependências adicionais para o mesmo resultado.
- Tema: `M3` (Material 3) light, cor primária arbitrária (azul, sem guideline de marca) — schematic `ng add @angular/material` na Sub-A escolhe o preset e gera `styles.scss` com `@include mat.theme(...)`.

Instalação (Sub-A, tarefa de bootstrap):
```
ng add @angular/material
```
Confirmar opções: tema custom (Azure/Blue), `setupGlobalTypography: yes`, animations: yes (`provideAnimationsAsync` em `app.config.ts`).

---

## 1. Estrutura de autenticação (Sub-A)

### 1.1 `AuthService` (`dashboard/src/app/core/auth/auth.service.ts`)

```typescript
export interface AuthUser {
  email: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly TOKEN_KEY = 'omuletachou_token';
  private tokenSignal = signal<string | null>(this.readStoredToken());

  readonly isAuthenticated = computed(() => !!this.tokenSignal());

  constructor(private http: HttpClient, private router: Router) {}

  login(email: string, password: string): Observable<void> {
    return this.http.post<{ token: string }>('/api/auth/login', { email, password }).pipe(
      tap(res => this.setToken(res.token)),
      map(() => void 0)
    );
  }

  logout(redirectMessage?: string): void {
    this.clearToken();
    this.router.navigate(['/login'], { queryParams: redirectMessage ? { message: redirectMessage } : {} });
  }

  getToken(): string | null {
    return this.tokenSignal();
  }

  private setToken(token: string): void {
    sessionStorage.setItem(this.TOKEN_KEY, token);
    this.tokenSignal.set(token);
  }

  private clearToken(): void {
    sessionStorage.removeItem(this.TOKEN_KEY);
    this.tokenSignal.set(null);
  }

  private readStoredToken(): string | null {
    return sessionStorage.getItem(this.TOKEN_KEY);
  }
}
```

- Token nunca decodificado/validado no front (sem checagem de expiração client-side) — a única fonte de verdade de validade é a resposta 401 da API, tratada pelo interceptor (§1.3). Isso é suficiente para CA-A1/A2/A3.
- `isAuthenticated` como `computed` (Angular Signals, disponível desde Angular 16) — reativo para o guard e para o layout (mostrar/esconder menu).

### 1.2 `authGuard` (functional guard, `dashboard/src/app/core/auth/auth.guard.ts`)

```typescript
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated()) return true;
  router.navigate(['/login']);
  return false;
};

export const loginGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated()) {
    router.navigate(['/products']);
    return false;
  }
  return true;
};
```
- `authGuard` aplicado a todas as rotas protegidas (CA-A4). `loginGuard` aplicado só a `/login` (CA-A5).

### 1.3 `authInterceptor` (functional interceptor, `dashboard/src/app/core/auth/auth.interceptor.ts`)

```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.getToken();
  const authReq = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !req.url.includes('/api/auth/login')) {
        auth.logout('Sessão expirada, faça login novamente');
      }
      return throwError(() => err);
    })
  );
};
```
- Exclusão explícita de `/api/auth/login` no tratamento de 401 (CA-A2 já trata esse caso na própria tela de login, sem disparar o fluxo de "sessão expirada" — evita mensagem incorreta em credenciais erradas).
- Registro em `app.config.ts`: `provideHttpClient(withInterceptors([authInterceptor]))`.

### 1.4 Rotas e layout (`app.routes.ts` + shell)

```typescript
export const routes: Routes = [
  { path: 'login', component: LoginComponent, canActivate: [loginGuard] },
  {
    path: '',
    component: ShellComponent, // layout com menu lateral (novo, Sub-A)
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'products', pathMatch: 'full' },
      { path: 'products', component: ProductsComponent },
      { path: 'queue', component: QueueComponent },
      { path: 'facebook-manual', component: FacebookManualComponent },
      { path: 'settings', component: SettingsComponent },
      { path: 'jobs', component: JobsComponent },
      { path: 'reports', component: ReportsComponent },
    ],
  },
];
```
- `ShellComponent` (novo, `dashboard/src/app/core/shell/shell.component.ts`): `MatSidenav` com menu lateral (6 itens: Products, Queue, Facebook Manual, Settings, Jobs, Reports) + botão de logout. Não existe no scaffold — task de bootstrap da Sub-A.
- `/login` fora do `ShellComponent` (CA-A8 — sem menu lateral).

### 1.5 `ng serve` local (proxy de dev)

O `nginx.conf` do container só resolve `/api/` em produção/homolog. Para `ng serve` local, criar `dashboard/proxy.conf.json`:
```json
{ "/api": { "target": "http://localhost:8080", "secure": false } }
```
E `angular.json` → `serve.options.proxyConfig: "proxy.conf.json"`. Tarefa de bootstrap da Sub-A (sem isso, os devs de Sub-B/C/D não conseguem testar contra a API local).

---

## 2. Contratos dos services Angular (espelham os DTOs reais da API — Issue #11, `main`)

Todos os services usam `HttpClient` com paths relativos (`/api/...` — resolvido pelo proxy nginx/dev). Nenhum service precisa anexar o header `Authorization` manualmente (feito pelo `authInterceptor`, §1.3).

### 2.1 `ProductsService` (`dashboard/src/app/core/services/products.service.ts`)

```typescript
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export type ProductStatus = 'Pending' | 'Queued' | 'Published' | 'Rejected' | 'Processing' | 'Error';
export type Platform = 'Amazon' | 'MercadoLivre' | 'Shopee';

export interface ProductListItem {
  id: string;
  title: string;
  salePrice: number;
  originalPrice: number;
  discountPct: number;
  status: ProductStatus;
  platform: Platform;
  slug: string;
  category: string;
  createdAt: string; // ISO
  // ai_score / ai_reason: ver §2.1.1 — ajuste aditivo necessário no backend
  ai_score?: number | null;
  ai_reason?: string | null;
}

export interface ProductDetail extends ProductListItem {
  description: string;
  affiliateLink: string | null;
  imageUrl: string | null;
  mediaUrl: string | null;
  mediaLocalPath: string | null;
  updatedAt: string;
}

@Injectable({ providedIn: 'root' })
export class ProductsService {
  constructor(private http: HttpClient) {}

  list(params: { status?: string; platform?: string; page?: number; pageSize?: number }): Observable<PagedResult<ProductListItem>> {
    return this.http.get<PagedResult<ProductListItem>>('/api/products', { params: cleanParams(params) });
  }

  updateStatus(id: string, status: 'pending' | 'rejected'): Observable<void> {
    return this.http.patch<void>(`/api/products/${id}/status`, { status });
  }
}
```

#### 2.1.1 GAP DE CONTRATO — `ai_score`/`ai_reason` ausentes em `GET /api/products` (ação: ajuste aditivo no backend, Sub-B)

Inspeção de `backend/src/AfiliadoBot.Api/Products/ProductDtos.cs` confirma: `ProductListItemDto` (usado por `GET /api/products`, endpoint da tabela) **não inclui** `ai_score`/`ai_reason` — esses campos só existem em `ProductDetailDto` (`GET /api/products/{id}`). CA-B1/CA-B2 desta issue exigem badge de `ai_score` e tooltip de `ai_reason` **na própria tabela**, não em uma tela de detalhe (que não existe no escopo desta issue).

**Decisão do LT (dentro da autoridade de refinamento técnico — não é mudança de comportamento em produção, é extensão puramente aditiva de um DTO de leitura, sem quebrar nenhum consumidor existente):** a Sub-B deve estender `ProductListItemDto` com os dois campos `ai_score`/`ai_reason` (mesmo `[JsonPropertyName]` snake_case do detalhe) e incluí-los na projeção do `ProductsController.GetProducts`. É a mesma tabela (`products`), os campos já existem na entidade `Product` — apenas não estavam projetados na listagem. Sem migration, sem alteração de contrato para quem já consome `ProductListItemDto` (só o dashboard Angular consome esse endpoint). Buscar N+1 (`GetProduct` por linha) é descartado por custo de performance desnecessário.
- Task explícita atribuída a Sub-B (dev mexe em `backend/src/AfiliadoBot.Api/Products/ProductDtos.cs` e `Controllers/ProductsController.cs`, + ajuste no teste correspondente em `AfiliadoBot.Tests`).
- Isso NÃO reabre a Issue #11 nem precisa de aprovação do Gerente — é extensão aditiva de leitura, análoga ao padrão já usado (campos adicionais, sem mudança de comportamento), diferente das correntes retroativas de negócio das Issues #8/#9.

### 2.2 `QueueService` (`dashboard/src/app/core/services/queue.service.ts`)

```typescript
export type PublicationStatus = 'Scheduled' | 'Published' | 'Failed' | 'ManualPending';
export type SocialNetwork = 'Telegram' | 'Youtube' | 'Instagram' | 'TikTok' | 'Facebook';

export interface QueueItem {
  id: string;
  productId: string;
  socialNetwork: SocialNetwork;
  status: PublicationStatus;
  scheduledAt: string;
  publishedAt: string | null;
  retryCount: number;
  errorMessage: string | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class QueueService {
  constructor(private http: HttpClient) {}

  list(params: { status?: string; network?: string; page?: number; pageSize?: number }): Observable<PagedResult<QueueItem>> {
    return this.http.get<PagedResult<QueueItem>>('/api/queue', { params: cleanParams(params) });
  }

  listManualPending(params: { page?: number; pageSize?: number }): Observable<PagedResult<QueueItem>> {
    return this.http.get<PagedResult<QueueItem>>('/api/queue/manual', { params: cleanParams(params) });
  }

  retry(id: string): Observable<void> {
    return this.http.post<void>(`/api/queue/${id}/retry`, {});
  }

  markPublished(id: string): Observable<void> {
    // ver §2.2.1 — novo endpoint aditivo, Sub-D
    return this.http.patch<void>(`/api/queue/${id}/status`, { status: 'Published' });
  }
}
```
- `/queue` (Sub-B) usa `list()` com filtros de status/network e `retry()`.
- `/facebook-manual` (Sub-D) usa `listManualPending()` e `markPublished()`.

#### 2.2.1 GAP DE CONTRATO — sem endpoint para "marcar como publicado" (ação: novo endpoint aditivo no backend, Sub-D)

CA-D3 exige que o card de post pendente do Facebook dispare uma chamada para marcar o item como publicado. Inspeção de `QueueController.cs`/`PublicationQueue.cs`: só existe `POST /api/queue/{id}/retry` (transição `Failed → Scheduled`, via `Retry()`). Não há nenhum endpoint nem método de domínio para a transição `ManualPending → Published`.

**Decisão do LT:** Sub-D adiciona:
- Método de domínio em `PublicationQueue.cs` (mesmo padrão de `Retry()`):
```csharp
public void MarkAsPublishedManually()
{
    if (Status != PublicationStatus.ManualPending)
        throw new InvalidOperationException("Somente itens ManualPending podem ser marcados como publicados manualmente.");
    Status = PublicationStatus.Published;
    PublishedAt = DateTime.UtcNow;
}
```
- Endpoint em `QueueController.cs`, mesmo padrão de `RetryQueueItem` (404 se não existe, 409 se não está em `ManualPending`):
```csharp
[HttpPatch("{id:guid}/status")]
public async Task<IActionResult> MarkPublished(Guid id, [FromBody] UpdateQueueStatusRequest request, CancellationToken ct)
{
    // valida request.Status == "Published" (case-insensitive); demais valores => 400
    // busca item; 404 se ausente; 409 se Status != ManualPending; senão MarkAsPublishedManually() + SaveChanges + 204
}
```
- Endpoint novo e aditivo (não altera comportamento de nenhum endpoint existente da Issue #11) — mesma justificativa de baixo risco do §2.1.1. Sub-D inclui teste em `AfiliadoBot.Tests/Queue/QueueControllerTests.cs`.

### 2.3 `SettingsService` (`dashboard/src/app/core/services/settings.service.ts`)

```typescript
export interface Setting {
  key: string;
  value: string; // valor mascarado (****************a1b2) para chaves sensíveis, ou valor real para não-sensíveis
}

@Injectable({ providedIn: 'root' })
export class SettingsService {
  constructor(private http: HttpClient) {}

  getAll(): Observable<Setting[]> {
    return this.http.get<Setting[]>('/api/settings');
  }

  updateOne(key: string, value: string): Observable<Setting> {
    return this.http.put<Setting>(`/api/settings/${key}`, { value });
  }
}
```

**Comportamento exato do formulário de Settings (CA-C1/C2/C3 — ver §3 abaixo para o detalhamento completo do componente.)**

### 2.4 `JobsService` (`dashboard/src/app/core/services/jobs.service.ts`)

```typescript
export type JobKind = 'collector' | 'collector-amazon' | 'collector-mercadolivre' | 'collector-shopee' | 'processor' | 'publisher';

const JOB_ENDPOINTS: Record<JobKind, string> = {
  'collector': '/api/jobs/collector/trigger',
  'collector-amazon': '/api/jobs/collector/amazon/trigger',
  'collector-mercadolivre': '/api/jobs/collector/mercadolivre/trigger',
  'collector-shopee': '/api/jobs/collector/shopee/trigger',
  'processor': '/api/jobs/processor/trigger',
  'publisher': '/api/jobs/publisher/trigger',
};

@Injectable({ providedIn: 'root' })
export class JobsService {
  constructor(private http: HttpClient) {}

  trigger(kind: JobKind): Observable<{ count?: number }> {
    return this.http.post<{ count?: number }>(JOB_ENDPOINTS[kind], {});
  }
}
```
- Todos os 6 endpoints já existem em `JobsController.cs` (Issue #11, protegidos por `[Authorize]`) — nenhum ajuste de backend necessário aqui.
- `trigger()` retorna quando a chamada HTTP completa (200), não quando o job (Hangfire, assíncrono) termina de fato — CA-C8 exige exibir "resultado da última execução disparada" a partir dessa resposta (sucesso HTTP = "disparado com sucesso"; erro HTTP = exibe mensagem de erro), sem polling de status do job nesta issue.

### 2.5 `ReportsService` (`dashboard/src/app/core/services/reports.service.ts`)

```typescript
export interface ReportsSummary {
  periodStart: string;
  periodEnd: string;
  totalPublished: number;
  byNetwork: { network: string; count: number }[];
  byDay: { date: string; count: number }[];
}

export interface ReportsTotals {
  today: number;
  week: number;
  month: number;
}

@Injectable({ providedIn: 'root' })
export class ReportsService {
  constructor(private http: HttpClient) {}

  summary(): Observable<ReportsSummary> {
    return this.http.get<ReportsSummary>('/api/reports/summary');
  }

  totals(): Observable<ReportsTotals> {
    // ver §2.5.1 — novo endpoint aditivo, Sub-D
    return this.http.get<ReportsTotals>('/api/reports/totals');
  }

  // Falhas recentes: reaproveita QueueService.list({ status: 'Failed' }) — ver §2.5.2, sem endpoint novo
}
```

#### 2.5.1 GAP DE CONTRATO — cards de totais hoje/semana/mês (ação: novo endpoint aditivo, Sub-D)

`GET /api/reports/summary` (`ReportsController.cs`) só cobre uma janela fixa de 7 dias (`totalPublished`, `byNetwork`, `byDay`) — usada para o **gráfico** (CA-D5). CA-D4 exige 3 cards de totais distintos: hoje / semana / mês, que a janela fixa de 7 dias não cobre integralmente (mês precisa de uma janela maior).

**Decisão do LT:** Sub-D adiciona `[HttpGet("totals")]` em `ReportsController.cs`, endpoint de leitura aditivo (não altera `summary`), calculando 3 contagens sobre `PublicationQueue` com `Status == Published`:
- `today`: `PublishedAt.Date == UtcNow.Date`.
- `week`: `PublishedAt >= início da semana ISO corrente (segunda-feira, UTC) `.
- `month`: `PublishedAt >= primeiro dia do mês corrente (UTC)`.

```json
{ "today": 3, "week": 12, "month": 47 }
```
Sem migration, sem mudança em endpoint existente — mesma justificativa de baixo risco dos gaps anteriores. Sub-D inclui teste em `AfiliadoBot.Tests` (novo arquivo ou extensão do existente de Reports, se houver).

#### 2.5.2 Tabela de falhas recentes (CA-D6) — SEM endpoint novo, reaproveita `QueueService`

`GET /api/queue?status=Failed` (já existente, `QueueController.GetQueue`) cobre exatamente a necessidade de CA-D6 (itens falhos com `errorMessage`). A tela `/reports` chama `QueueService.list({ status: 'Failed', pageSize: 10 })` e reaproveita `QueueService.retry(id)` para o botão "Retry" (mesmo fluxo de `/queue`, conforme já é dito na proposta). **Nenhum ajuste de backend necessário aqui.**

---

## 3. Comportamento do formulário de Settings (Sub-C) — detalhamento

1. **Carregamento (`ngOnInit`):** `SettingsService.getAll()` retorna todas as chaves com valor mascarado (sensíveis) ou real (não sensíveis, ex. `claude.min_score`, `publish.max_per_day`, `networks.*.enabled`).
2. **Agrupamento por seção**, mapeado por prefixo da chave (fixo no componente, não vem da API):
   - `amazon.*` → seção "Amazon"
   - `mercadolivre.*` → seção "MercadoLivre"
   - `shopee.*` → seção "Shopee"
   - `telegram.*` → seção "Telegram"
   - `youtube.*` → seção "YouTube"
   - `instagram.*` → seção "Instagram"
   - `tiktok.*` → seção "TikTok"
   - `claude.*` → seção "Claude AI"
   - `schedule.*`, `publish.*` → seção "Agendamentos"
   - `networks.*.enabled` → seção "Redes habilitadas" (toggle boolean: valor `"true"`/`"false"` como string, renderizado como `MatSlideToggle`)
   - `hangfire.dashboard_password`, `api.public_base_url` → seção "Avançado" (não prevista no Gate 1 explicitamente, mas precisa de um lugar — não travar o build por chave órfã)
3. **Campo sensível (chave termina em `_key`/`_secret`/`_token`/`_password`, mesma regra de `especificacao-tecnica.md` da Issue #11 §5):** renderizado como `<input type="password">` com toggle show/hide (`MatIconButton` + `visibility`/`visibility_off`, CA-C4), **valor do input sempre iniciado vazio** (nunca populado com o valor mascarado retornado pela API), `placeholder="Valor atual: ${value} — digite para substituir"` (usa o valor mascarado já retornado, CA-C1). Campo não sensível: `<input type="text">`, populado normalmente com o valor real retornado.
4. **Submit por seção (botão "Salvar" da seção, CA-C5):** para cada campo da seção,
   - Se o input está vazio (`value === ''`) **e é um campo sensível** → **não gera chamada `PUT`** (CA-C2). Preservado no backend sem qualquer ação.
   - Se o input tem valor (novo ou igual ao anterior, para campos não sensíveis onde o valor real já está no input) → dispara `SettingsService.updateOne(key, value)` (CA-C3).
   - Implementação: `forkJoin` dos `PUT`s da seção (paralelo), com `MatSnackBar` de sucesso/erro ao final; falha em uma chave não deve bloquear as demais (usar `catchError` por chamada individual dentro do `forkJoin`, coletando sucesso/erro por chave).
5. **CA-C6 (sem botão "Testar conexão"):** não implementar nenhuma ação de teste de conexão nesta issue — nenhuma referência a esse endpoint no componente.

---

## 4. Estrutura de pastas (`dashboard/src/app/`)

```
app/
  core/
    auth/
      auth.service.ts
      auth.guard.ts
      auth.interceptor.ts
    services/
      products.service.ts
      queue.service.ts
      settings.service.ts
      jobs.service.ts
      reports.service.ts
      paged-result.model.ts        (interface PagedResult<T> + helper cleanParams())
    shell/
      shell.component.ts(.html/.scss)   (menu lateral + router-outlet)
  pages/
    login/            (novo — Sub-A)
    products/          (evolui stub — Sub-B)
    queue/              (evolui stub — Sub-B)
    facebook-manual/    (evolui stub — Sub-D)
    settings/           (evolui stub — Sub-C)
    jobs/              (novo — Sub-C)
    reports/            (evolui stub — Sub-D)
  app.routes.ts
  app.config.ts
```

- `cleanParams(params)` (helper compartilhado, `core/services/paged-result.model.ts`): remove chaves `undefined`/vazias antes de montar `HttpParams`, evitando enviar `status=&platform=` para a API.

---

## 5. Testes unitários (CA-T2)

- `AuthService`: login sucesso (token salvo em `sessionStorage` + signal atualizado), login falha (401, nenhum token salvo), `logout()` limpa storage e navega para `/login`.
- `authInterceptor`: anexa header em request autenticada; 401 fora de `/api/auth/login` dispara `logout()`; 401 em `/api/auth/login` NÃO dispara `logout()` (evita loop/mensagem incorreta na própria tela de login).
- `authGuard`/`loginGuard`: navegação condicional conforme `isAuthenticated()`.
- `ProductsService`/`QueueService`/`SettingsService`/`ReportsService`: request HTTP correto (`HttpTestingController`, `HttpClientTestingModule`), incluindo o comportamento de "campo vazio não dispara PUT" testado no componente de Settings (não no service, que só expõe `updateOne` por chave — a lógica de decisão é do componente, §3).
- Cobertura: sem threshold numérico fixo definido no Gate 1 — CA-T2 exige cobertura dos "fluxos principais" de cada service, não um percentual mínimo.

---

## 6. Ordem de dependência entre sub-issues

- **Sub-A é pré-requisito e deve ser mergeada em `desenv` antes de Sub-B/C/D iniciarem em paralelo** (decisão do LT — diferente do padrão "iniciar em paralelo com stub" sugerido para a Issue #12, aqui o `ShellComponent`/layout com menu lateral, o `authInterceptor` registrado em `app.config.ts`, e o `provideHttpClient` global são pré-requisitos físicos de infraestrutura que todas as demais telas usam diretamente — não há stub razoável para "layout com menu lateral" que não seja retrabalho).
- Sub-B, Sub-C, Sub-D são paralelizáveis entre si após o merge de Sub-A (cada uma mexe em pastas de páginas distintas + endpoints de backend distintos — sem sobreposição de arquivos, exceto potencialmente `QueueController.cs`/`QueueItemDto.cs`, compartilhados por Sub-B (retry) e Sub-D (`markPublished`, novo endpoint) — resolução: quem mergear primeiro em `desenv` entre B/D não deve conflitar, já que os métodos são adicionados, não modificados; o LT resolve conflito textual trivial de import/adjacência de métodos no merge, se necessário).
