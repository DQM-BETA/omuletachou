# Relatório de QA — ISSUE-8: Publisher YouTube Shorts

**Status: REPROVADO**

PR validado: #67 (desenv→homolog), merge commit `a1a7496` — confirmado presente em `homolog`
(`git log --oneline -5` após `git fetch && git checkout homolog && git pull`).

## Resumo executivo

A suíte de testes (128/128) passa e o Code Review já havia aprovado build/boot/cobertura.
Na validação **integrada** (aplicação real via Docker Compose, banco Postgres real, chamadas
HTTP reais ao endpoint OAuth2 do Google), foi encontrado um **defeito funcional real** que os
testes unitários (mocados, chamando `YoutubePublisher.PublishAsync` isoladamente) não detectam:
quando o fluxo completo é exercido através do `PublisherJob` (o caminho real de produção), a
`ErrorMessage` específica setada internamente pelo `YoutubePublisher` (ex.: a mensagem exigida
literalmente pelo CA16) é **sobrescrita** por uma mensagem genérica em
`PublisherJob.cs` (linha 62-63:
`item.RegisterAttempt(success, success ? null : "Falha ao publicar (retorno negativo do publisher).")`),
porque `PublicationQueue.RegisterAttempt` sempre sobrescreve `ErrorMessage` incondicionalmente.

Isso viola o CA16, que exige literalmente `ErrorMessage = "Produto sem mídia de vídeo, não
aplicável ao YouTube"` no item persistido — e no banco real, após rodar a aplicação de ponta a
ponta, o valor persistido é `"Falha ao publicar (retorno negativo do publisher)."`.

## Ambiente de validação

- `git fetch origin && git checkout homolog && git pull origin homolog` — commit `a1a7496`
  confirmado no topo (via `git log --oneline -5`).
- `dotnet test` no diretório `backend`: **128/128 aprovados**, 0 falhas (24s).
- `docker compose up -d --build`: os 4 serviços (`db`, `api`, `website`, `dashboard`) subiram
  com sucesso. Migração `SeedYoutubeCredentials` aplicada corretamente.
- `GET /health` → `200 {"status":"healthy",...}`.
- `dashboard` (`:4200`) → 200. `website` (`:3000`) → 200. `/hangfire` → 401 (esperado, senha
  não configurada em app_settings — comportamento correto, não é bug desta issue).
- Endpoints de trigger manual usados para exercer o fluxo real:
  `POST /api/jobs/processor/trigger` e `POST /api/jobs/publisher/trigger`.

## Validação por critério de aceite

| CA | Descrição | Resultado | Como validado |
|---|---|---|---|
| CA1 | Upload com vídeo local | ✅ Passa | Análise de código (`ResolveMediaSourceAsync`/`UploadChunksAsync`) + testes mockados. Upload real ao YouTube não é viável (credenciais reais indisponíveis) |
| CA2 | Fallback MediaUrl → download | ✅ Passa | Análise de código + teste `PublishAsync_BaixaMediaUrl_QuandoMediaLocalPathNulo` (mock `IMediaStorage`) |
| CA3 | Sem validação prévia de proporção/duração | ✅ Passa | Análise de código — nenhuma validação de proporção/duração antes do upload |
| CA4 | Vídeo fora dos critérios de Short publicado normalmente | ✅ Passa | Análise de código — mesmo fluxo de upload, sem branch de erro para isso |
| CA5 | Title truncado a 100 chars | ✅ Passa | Teste `PublishAsync_TruncaTitulo_Para100CaracteresNoMetadata` + `BuildMetadataJson` (linha 254) |
| CA6 | Description = AiCaption | ✅ Passa | Teste `PublishAsync_DescricaoNoMetadata_IgualAoAiCaption` + código (linha 255) |
| CA7 | Tags fixas | ✅ Passa | Teste `PublishAsync_TagsNoMetadata_ContemValoresFixosEsperados` + código (linha 264) |
| CA8 | Mapeamento categoryId | ✅ Passa | Teste `PublishAsync_MapeiaCategoriaCorreta_PorCategoriaDoProduto` (Theory, 6 categorias) + `CategoryMap` |
| CA9 | Fallback categoryId "22" | ✅ Passa | Mesmo teste Theory cobre categoria não mapeada (`DefaultCategoryId`) |
| CA10 | privacyStatus sempre "public" | ✅ Passa | Teste `PublishAsync_PrivacyStatusNoMetadata_SempreIgualAPublic` + código (linha 269) |
| CA11 | Renovação automática de access_token | ✅ Passa (validado end-to-end real) | **E2E real**: trigger de `/api/jobs/publisher/trigger` gerou uma chamada HTTP real a `https://oauth2.googleapis.com/token` (log confirmado); fluxo de renovação executado de fato pela aplicação rodando |
| CA12 | Falha de refresh_token sem retry | ✅ Passa (validado end-to-end real) | **E2E real**: com credenciais fake, o endpoint OAuth2 real do Google respondeu 401; `YoutubePublisher` marcou o item `Failed` (status=2), `retry_count` >= 3 (não reprocessado — confirmado pela query do `PublisherJob`), e `youtube.token_invalid=true` foi persistido em `app_settings` (confirmado via `SELECT`) |
| CA13 | Chunks de 8MB | ✅ Passa | Análise de código (`ChunkSizeBytes = 8*1024*1024`) + teste `PublishAsync_UploadPorChunks_RespeitaTamanhoDe8MB`. Upload real de arquivo >8MB ao YouTube não viável sem credenciais reais |
| CA14 | Timeout de chunk (5min) | ✅ Passa | Teste `PublishAsync_TimeoutDeChunk_LancaInvalidOperationExceptionComMensagemDeChunk` + código (`ChunkTimeout`) |
| CA15 | Timeout total (15min) | ✅ Passa | Teste `PublishAsync_TimeoutTotal_...` + código (`TotalTimeout`) |
| **CA16** | **Fallback de segurança — item sem vídeo, ErrorMessage exata** | ❌ **REPROVADO** | **E2E real**: inserida entrada de `PublicationQueue` (Youtube) para produto sem vídeo diretamente no banco (simulando item legado, cenário do próprio CA16) e disparado `POST /api/jobs/publisher/trigger`. Log confirma que o fallback de segurança do `YoutubePublisher` foi acionado ("sem midia de video ... Fallback de seguranca acionado"), mas o `error_message` **persistido no banco** ficou `"Falha ao publicar (retorno negativo do publisher)."`, e não `"Produto sem mídia de vídeo, não aplicável ao YouTube"` como o CA exige literalmente. Causa raiz: `PublisherJob.cs:62-63` chama `item.RegisterAttempt(success, "Falha ao publicar...")` incondicionalmente após `PublishAsync` retornar, e `PublicationQueue.RegisterAttempt` sobrescreve `ErrorMessage` sem checar se já havia uma mensagem mais específica setada. O teste unitário `PublishAsync_FalhaSemRetry_QuandoProdutoSemVideo` não pega isso porque chama `YoutubePublisher.PublishAsync` isoladamente, sem passar pelo `PublisherJob` |
| CA17 | ProcessorJob não enfileira Youtube sem vídeo | ✅ Passa (validado end-to-end real) | **E2E real**: produto real inserido no banco (`status=Queued`, sem vídeo, Youtube habilitado com credenciais fake, demais redes desabilitadas), `POST /api/jobs/processor/trigger` disparado na aplicação real — nenhuma entrada de `PublicationQueue` foi criada para o produto. Também coberto por teste `ExecuteAsync_NaoCriaEntradaYoutube_QuandoProdutoSemVideo` |
| CA18 | ProcessorJob enfileira Youtube com vídeo | ✅ Passa (validado end-to-end real) | **E2E real**: produto real com `media_type='video'` e `media_local_path` apontando a um arquivo real no volume `/app/media`, mesmo trigger real — entrada `PublicationQueue` com `social_network=1` (Youtube), `status=0` (Scheduled) criada corretamente. Também coberto por `ExecuteAsync_CriaEntradaYoutube_QuandoProdutoComVideo` |
| CA19 | Regressão — demais redes não afetadas | ✅ Passa | Teste `ExecuteAsync_NaoAfetaDemaisRedes_QuandoYoutubeFiltrado` cobre explicitamente Telegram/Instagram/TikTok/Facebook com produto sem vídeo — todas as 4 entradas criadas normalmente, só Youtube filtrado. Análise de código confirma que o filtro `network == SocialNetwork.Youtube && !HasVideoAvailable(product)` só afeta a iteração da rede Youtube dentro do loop `foreach (var (network, ...) in NetworkSettings)` (ProcessorJob.cs:231-253), sem alterar a lógica das demais redes |
| CA20 | Testes com mocks, sem chamadas reais | ✅ Passa | `dotnet test`: 128/128 aprovados. Confirmado uso de `Mock<HttpMessageHandler>` (YoutubePublisherTests.cs:86) — nenhuma chamada real ao Google nos testes automatizados. (As chamadas reais ao OAuth2 do Google mencionadas nas linhas de CA11/CA12/CA16 acima foram feitas **pela validação manual de QA via Docker**, não pela suíte de testes) |

## Gate visual (UI)

N/A — issue é 100% backend (`YoutubePublisher`, `ProcessorJob`), sem alteração em `dashboard`
ou `website`. Confirmado que nenhum dos dois projetos define script `test:visual` em
`package.json` (`dashboard/package.json`, `website/package.json`) — não há UI nova para este
escopo, logo não há gate visual/Playwright aplicável a esta issue.
`E2E/screenshots: N/A (issue sem UI)`.

## Evidências de execução (comandos-chave)

```
dotnet test → Aprovado! Com falha: 0, Aprovado: 128, Total: 128, Duração: 24s

docker compose up -d --build → 4/4 containers "Started"
GET /health → 200 {"status":"healthy"}

# CA17 (real, sem mock):
INSERT produto sem vídeo, youtube habilitado (credenciais fake), demais redes desabilitadas
POST /api/jobs/processor/trigger → 200
SELECT publication_queue WHERE product_id=... → 0 linhas (nenhuma entrada Youtube)

# CA18 (real, sem mock):
INSERT produto com media_type='video', media_local_path='/app/media/qa/video.mp4' (arquivo real)
POST /api/jobs/processor/trigger → 200
SELECT publication_queue → social_network=1 (Youtube), status=0 (Scheduled)

# CA11/CA12 (real, sem mock — chamada HTTP real ao Google):
POST /api/jobs/publisher/trigger → 200
Log: "Sending HTTP request POST https://oauth2.googleapis.com/token" → "Received ... 401"
Log: "YoutubePublisher: falha ao renovar access_token via refresh_token..."
SELECT app_settings WHERE key='youtube.token_invalid' → 'true'
SELECT publication_queue → status=2 (Failed), retry_count=4 (não será reprocessado)

# CA16 (real, sem mock — DEFEITO ENCONTRADO):
INSERT publication_queue manual (item "legado" sem vídeo, simulando cenário do CA16)
POST /api/jobs/publisher/trigger → 200
Log: "YoutubePublisher: produto ... sem midia de video (MediaType=(null)). Fallback de
      seguranca acionado."
SELECT publication_queue → status=2 (Failed) [OK], mas
       error_message = 'Falha ao publicar (retorno negativo do publisher).'  [ESPERADO:
       'Produto sem mídia de vídeo, não aplicável ao YouTube']
```

## Issue encontrada

**Severidade: Média/Alta (funcional, viola CA explícito, não é ambiguidade de requisito)**

- **Local:** `backend/src/AfiliadoBot.Application/Jobs/PublisherJob.cs:62-63` +
  `backend/src/AfiliadoBot.Domain/Entities/PublicationQueue.cs:51-65` (`RegisterAttempt`).
- **Problema:** `PublisherJob` chama `item.RegisterAttempt(success, "Falha ao publicar (retorno
  negativo do publisher).")` incondicionalmente após `PublishAsync` retornar `false`, e
  `RegisterAttempt` sempre sobrescreve `ErrorMessage` — apagando a mensagem específica que o
  `YoutubePublisher` já havia setado internamente via `FailPermanently` (usada tanto no CA16
  quanto potencialmente em outras falhas específicas retornadas via `false` em vez de exceção).
- **Impacto:** CA16 falha literalmente no ambiente real — a mensagem de erro persistida no
  banco não é a exigida pelo critério de aceite. Isso prejudica observabilidade/diagnóstico em
  produção (mensagem genérica em vez da causa raiz real).
- **Sugestão para o LT/Dev (não prescritivo, decisão de implementação é do time):** ou (a)
  `PublisherJob` não sobrescrever `ErrorMessage`/não chamar `RegisterAttempt` de novo quando o
  publisher já deixou o item em `Status = Failed` com `RetryCount >= 3` (verificar estado antes
  de registrar); ou (b) `YoutubePublisher` lançar exceção com a mensagem específica em vez de
  retornar `false` diretamente (o `catch` do `PublisherJob` já preserva `ex.Message` — linha 71).

## Conclusão

QA **reprovado** — 19/20 critérios de aceite passam (a maioria confirmada com validação
end-to-end real contra a aplicação rodando via Docker + Postgres real + chamada HTTP real ao
Google OAuth2), mas o **CA16 falha na validação integrada real**: a `ErrorMessage` persistida
não corresponde ao valor literal exigido pelo critério. É defeito de implementação (não
ambiguidade de requisito) — retorna ao Líder Técnico para mapear e ao(s) Dev(s) para correção.
