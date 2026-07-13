# Especificação técnica — ISSUE-10: Publisher TikTok (Content Posting API)

## Contrato de código
```csharp
public class TikTokPublisher : ISocialPublisher
{
    public SocialNetwork Network => SocialNetwork.TikTok;
    public Task<bool> PublishAsync(PublicationQueue item, CancellationToken ct = default);
}
```
Construtor: `HttpClient`, `AfiliadoBotDbContext`, `IMediaStorage`, `ILogger<TikTokPublisher>`
(mesmo padrão de injeção do `YoutubePublisher`).

## Endpoints TikTok Content Posting API (v2)
| Etapa | Método/URL | Corpo/Headers relevantes | Retorno |
|---|---|---|---|
| 1. Init | `POST https://open.tiktokapis.com/v2/post/publish/video/init/` | `Authorization: Bearer {access_token}`; body JSON: `post_info.title`, `post_info.privacy_level`, `post_info.brand_content_toggle=true`, `post_info.disable_duet=false`, `post_info.disable_comment=false`, `source_info.source=FILE_UPLOAD`, `source_info.video_size`, `source_info.chunk_size`, `source_info.total_chunk_count` | `data.publish_id`, `data.upload_url` |
| 2. Upload | `PUT {upload_url}` (chunked) | `Content-Range: bytes {start}-{end}/{total}`; `Content-Length: {chunkSize}`; `Content-Type: video/mp4` | 2xx por chunk |
| 3. Status | `POST https://open.tiktokapis.com/v2/post/publish/status/fetch/` | body: `publish_id` | `data.status` (`PROCESSING_UPLOAD`, `PUBLISH_COMPLETE`, `FAILED`) |
| Refresh token | `POST https://open.tiktokapis.com/v2/oauth/token/` | `client_key`, `client_secret`, `grant_type=refresh_token`, `refresh_token` | `access_token`, `refresh_token` (novo), `expires_in` |

Chunk size: reaproveitar a constante já usada no `YoutubePublisher` (8MB / `8 * 1024 * 1024`) —
mesma ordem de grandeza recomendada pelas duas APIs, evita introduzir uma segunda constante
divergente sem motivo.

## Schema de dados (`app_settings`, novos — migration `SeedTikTokCredentials`)
| Key | Seed | Observação |
|---|---|---|
| `tiktok.client_key` | `""` | placeholder — preenchido manualmente após onboarding no TikTok Developer Portal |
| `tiktok.client_secret` | `""` | idem |
| `tiktok.refresh_token` | `""` | idem |
| `tiktok.privacy_level` | `SELF_ONLY` | alternado manualmente para `PUBLIC_TO_EVERYONE` após aprovação do app |
| `tiktok.min_duration_seconds` | `3` | |
| `tiktok.max_duration_seconds` | `600` | |

Já existentes (seed inicial, não recriar): `tiktok.access_token` (id 18), `tiktok.open_id` (id 19).

## Padrões obrigatórios
1. **`Mp4DurationReader`** (novo, `AfiliadoBot.Infrastructure/Media/`): método estático
   `bool TryGetDurationSeconds(string filePath, out double seconds)`, lê o átomo `moov/mvhd` do
   MP4 (suporta v0 — 32 bits — e v1 — 64 bits — do box). Sem dependência externa. Falha de parsing
   → `false` (tratado como "fora do intervalo", falha sem retry).
2. **`SocialDisclosureHelper`** (novo, `AfiliadoBot.Infrastructure/Integrations/Social/`): extrai
   a lógica hoje privada em `InstagramPublisher` (`DisclosureRegex` + `AppendDisclosureIfMissing`)
   para `public static string AppendIfMissing(string caption, string hashtag = "#publi")` +
   regex compartilhado (`#publi\b|#publicidade\b`, case-insensitive). `InstagramPublisher` passa a
   chamar o helper (refactor comportamentalmente neutro — os testes de regressão do Instagram
   devem continuar verdes sem alteração).
3. **Retry 429**: helper privado `TikTokPublisher.SendWithRetryAsync` — 3 tentativas, delay fixo
   2s/4s/8s, só reage a `HttpStatusCode.TooManyRequests`; qualquer outro status não-2xx propaga
   como falha imediata (mesmo padrão dos demais publishers).
4. **Falha sem retry** (duração fora do intervalo, refresh_token inválido, produto sem vídeo):
   usar o mesmo padrão `FailPermanently` (loop `while (item.RetryCount < 3) item.RegisterAttempt(false, msg)`)
   já usado em `YoutubePublisher`/`InstagramPublisher`.
5. **Testes**: mock de `HttpClient` via `HttpMessageHandler` fake (mesmo padrão de
   `YoutubePublisherTests`/`InstagramPublisherTests`), sem chamadas reais. CA20 (validação real)
   explicitamente fora do escopo de teste automatizado — não bloqueia o Gate 2.
