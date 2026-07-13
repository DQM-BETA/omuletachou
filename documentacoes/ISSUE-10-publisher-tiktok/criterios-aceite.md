# Critérios de aceite — ISSUE-10: Publisher TikTok (Content Posting API)

## TikTokPublisher — fluxo de publicação (3 etapas, FILE_UPLOAD)

**CA1 — Etapa 1: init do upload**
Given um item de `PublicationQueue` com `SocialNetwork = TikTok` e vídeo válido (MP4/WebM, duração dentro do intervalo aceito)
When `TikTokPublisher.PublishAsync` é chamado
Then é feita a chamada `POST /v2/post/publish/video/init/` com `title`, `privacy_level` (lido de `app_settings` — `tiktok.privacy_level`), `brand_content_toggle: true`, `disable_duet: false`, `disable_comment: false`, e `upload_url`/`publish_id` retornados são capturados para as etapas seguintes.

**CA2 — Etapa 2: upload chunked via PUT**
Given um `upload_url` retornado pela etapa 1
When `TikTokPublisher` envia os bytes do vídeo
Then a chamada `PUT {upload_url}` é feita com os headers `Content-Range: bytes 0-{size-1}/{size}` e `Content-Length: {size}` corretamente calculados para cada chunk, e o fluxo prossegue para a etapa de polling ao concluir o envio.

**CA3 — Etapa 3: polling até PUBLISH_COMPLETE**
Given um `publish_id` com upload concluído
When `TikTokPublisher` inicia o polling `POST /v2/post/publish/status/fetch/`
Then as chamadas são feitas a intervalos de 15 segundos até `status = PUBLISH_COMPLETE`, e o método retorna sucesso (`true`) quando o status é atingido.

**CA4 — Polling: status FAILED**
Given um `publish_id` cujo polling retorna `status = FAILED`
When `TikTokPublisher.PublishAsync` está em andamento
Then a publicação falha imediatamente com `ErrorMessage` descritivo.

**CA5 — Polling: timeout de 10 minutos**
Given um `publish_id` cujo polling não atinge `status = PUBLISH_COMPLETE` dentro de 10 minutos (intervalos de 15s)
When o timeout é atingido
Then o item é marcado como `Failed`, elegível a retry no próximo ciclo do `PublisherJob` (respeitando `RetryCount` até 3).

## Validação de duração (client-side, obrigatória)

**CA6 — Vídeo dentro do intervalo aceito (caminho feliz)**
Given um vídeo com duração entre `tiktok.min_duration_seconds` e `tiktok.max_duration_seconds` (seed: 3s e 600s)
When `TikTokPublisher.PublishAsync` é chamado
Then a validação de duração passa e o fluxo de upload (etapa 1) é iniciado normalmente.

**CA7 — Vídeo abaixo da duração mínima**
Given um vídeo com duração menor que `tiktok.min_duration_seconds`
When `TikTokPublisher.PublishAsync` é chamado
Then o item é marcado como `Failed` sem retry, com `ErrorMessage` "Vídeo fora do intervalo de duração aceito pelo TikTok (Xs-Ymin)", e nenhuma chamada de upload é feita à API.

**CA8 — Vídeo acima da duração máxima**
Given um vídeo com duração maior que `tiktok.max_duration_seconds`
When `TikTokPublisher.PublishAsync` é chamado
Then o item é marcado como `Failed` sem retry, com `ErrorMessage` "Vídeo fora do intervalo de duração aceito pelo TikTok (Xs-Ymin)", e nenhuma chamada de upload é feita à API.

**CA9 — Limites parametrizáveis via app_settings**
Given `tiktok.min_duration_seconds` e `tiktok.max_duration_seconds` alterados para valores diferentes do seed (ex.: 5 e 300)
When `TikTokPublisher.PublishAsync` valida a duração
Then os novos limites configurados são respeitados, sem necessidade de deploy ou alteração de código.

**CA10 — Proporção e resolução não são validadas previamente**
Given um vídeo com proporção/resolução variadas (ex.: vertical 9:16, horizontal 16:9)
When `TikTokPublisher.PublishAsync` é chamado
Then nenhuma validação de proporção/resolução é feita client-side — o vídeo segue para a etapa de upload independentemente do formato.

## Disclosure duplo (conteúdo comercial de afiliado)

**CA11 — brand_content_toggle ativado no init**
Given qualquer publicação processada por `TikTokPublisher` (conteúdo de afiliado)
When a chamada `POST /v2/post/publish/video/init/` é montada
Then o campo `brand_content_toggle = true` é sempre incluído no payload, classificando o vídeo como conteúdo comercial de marca.

**CA12 — Hashtag #publi anexada automaticamente**
Given qualquer legenda gerada pelo `GenerateCaptionAsync` para a rede TikTok (com ou sem hashtag de disclosure já incluída pela IA)
When a legenda final é montada pelo `TikTokPublisher`
Then a hashtag `#publi` é anexada automaticamente ao final da legenda, garantindo presença do disclosure independentemente do que a IA gerou.

**CA13 — Disclosure não duplicado**
Given uma legenda gerada pela IA que já inclui `#publi`
When a legenda final é montada pelo `TikTokPublisher`
Then o disclosure não é duplicado (apenas uma ocorrência de `#publi` na legenda final).

## Privacy level configurável

**CA14 — privacy_level padrão SELF_ONLY em desenvolvimento/teste**
Given `tiktok.privacy_level` não alterado manualmente (valor seed)
When `TikTokPublisher.PublishAsync` é chamado
Then o valor `SELF_ONLY` é enviado no `/video/init`, restringindo a publicação à conta autenticada (comportamento esperado em sandbox/unaudited).

**CA15 — privacy_level alterado manualmente para PUBLIC_TO_EVERYONE**
Given `tiktok.privacy_level` configurado manualmente pelo operador como `PUBLIC_TO_EVERYONE` (após aprovação do app em produção)
When `TikTokPublisher.PublishAsync` é chamado
Then o valor `PUBLIC_TO_EVERYONE` é enviado no `/video/init`, sem necessidade de alteração de código ou deploy.

## Refresh automático de token

**CA16 — Refresh automático em 401**
Given uma chamada à API do TikTok que retorna HTTP 401 (token expirado)
When `TikTokPublisher.PublishAsync` está em andamento
Then o `access_token` é renovado via `refresh_token`, o novo token é persistido em `app_settings`, e a chamada original é repetida automaticamente.

## Retry e rate limit

**CA17 — Backoff exponencial em 429**
Given uma chamada à API do TikTok que retorna HTTP 429 (rate limit)
When `TikTokPublisher.PublishAsync` está em andamento
Then o backoff exponencial é aplicado (3 tentativas, 2s/4s/8s) antes de desistir, consistente com o padrão já usado em Telegram/YouTube/Instagram.

**CA18 — RetryCount respeitado após esgotar tentativas**
Given uma publicação que falha de forma recuperável (timeout de polling, 429 esgotado)
When o `PublisherJob` reprocessa o item
Then o `RetryCount` é incrementado até o máximo de 3, e o item é marcado `Failed` definitivo ao esgotar as tentativas.

## Testes automatizados

**CA19 — Testes com mocks, sem chamadas reais**
Given a suíte de testes do projeto
When `dotnet test` é executado
Then todos os testes relacionados a `TikTokPublisher` passam usando mocks da TikTok Content Posting API e do `HttpClient`, sem nenhuma chamada real à API do TikTok.

## Validação em conta real (débito de validação — NÃO bloqueante para o Gate 2)

**CA20 — Publicação real validada visualmente (pendente, não-bloqueante)**
Given o app TikTok for Developers aprovado para acesso pleno à Content Posting API (fora do controle do time, prazo de 3-7 dias sem garantia) e a conta TikTok real configurada em produção
When um vídeo de teste é publicado via `TikTokPublisher` contra a API real do TikTok (fora do CI, execução manual/assistida), com `tiktok.privacy_level = PUBLIC_TO_EVERYONE`
Then o vídeo aparece de fato no perfil do TikTok (confirmação visual, não apenas resposta HTTP da API), com `brand_content_toggle` e hashtag `#publi` corretamente aplicados.

**IMPORTANTE — este critério NÃO bloqueia o Gate 2 desta issue.** Decisão explícita do Gerente no Gate 1 (Issue #10, comentário de respostas): diferente da Issue #9 (onde a dispensa da validação real equivalente só ocorreu manualmente já no Gate 2, causando confusão e quase virando bloqueio), aqui a não-obrigatoriedade já está definida antecipadamente. O Dev prossegue com testes mockados + validação em sandbox/`SELF_ONLY` (CA1-CA19) como suficientes para o Gate 2. O CA20 fica registrado como débito de validação pendente, a ser marcado como concluído (com evidência — print/link do post) assim que o app for aprovado pelo TikTok, sem reabrir o Gate 2 desta issue.
