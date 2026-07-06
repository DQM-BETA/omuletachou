namespace AfiliadoBot.Domain.Interfaces;

/// <summary>
/// Abstracao de armazenamento de midia para o ProcessorJob (Issue #6).
/// Baixa a midia de uma URL remota para armazenamento local, detectando o tipo por extensao.
/// Implementacoes NAO devem propagar exception em caso de falha (404, timeout, URL malformada) —
/// devem retornar LocalPath nulo e logar Warning, permitindo que o produto seja processado
/// mesmo sem midia local.
/// </summary>
public interface IMediaStorage
{
    /// <summary>
    /// Baixa o arquivo de <paramref name="mediaUrl"/> para o armazenamento local.
    /// </summary>
    /// <returns>
    /// Tupla com o caminho local do arquivo baixado (nulo em caso de falha) e o tipo de midia
    /// detectado pela extensao (\"video\" para .mp4/.webm, \"image\" para as demais extensoes).
    /// </returns>
    Task<(string? LocalPath, string MediaType)> DownloadAsync(string mediaUrl, CancellationToken ct = default);
}
