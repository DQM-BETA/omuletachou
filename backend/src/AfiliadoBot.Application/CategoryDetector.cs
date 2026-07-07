namespace AfiliadoBot.Application;

/// <summary>
/// Deteccao de categoria por palavras-chave no titulo do produto (ProcessorJob, Issue #6).
/// Classe estatica, sem dependencia de IA/banco. Substitui o "Geral" hardcoded dos collectors
/// quando ha match mais especifico. Comparacao case-insensitive; primeira categoria com match
/// (na ordem do dicionario) vence; sem nenhum match -> fallback "Geral".
/// </summary>
public static class CategoryDetector
{
    private const string FallbackCategory = "Geral";

    private static readonly Dictionary<string, List<string>> CategoriaPorPalavraChave = new()
    {
        ["Eletrônicos"] = new List<string>
        {
            "fone", "headphone", "earphone", "smartphone", "celular", "notebook", "tablet",
            "carregador", "cabo", "mouse", "teclado", "tv", "câmera", "camera"
        },
        ["Casa e Cozinha"] = new List<string>
        {
            "panela", "frigideira", "airfryer", "liquidificador", "aspirador", "cortina",
            "travesseiro", "cama", "sofá", "sofa", "decoração", "decoracao"
        },
        ["Beleza"] = new List<string>
        {
            "perfume", "maquiagem", "batom", "shampoo", "creme"
        },
        ["Moda"] = new List<string>
        {
            "camisa", "calça", "calca", "tênis", "tenis", "vestido", "bolsa", "relógio", "relogio"
        },
        ["Brinquedos"] = new List<string>
        {
            "boneca", "lego", "brinquedo", "jogo"
        }
    };

    public static string Detect(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return FallbackCategory;

        foreach (var (categoria, palavrasChave) in CategoriaPorPalavraChave)
        {
            foreach (var palavraChave in palavrasChave)
            {
                if (title.Contains(palavraChave, StringComparison.OrdinalIgnoreCase))
                    return categoria;
            }
        }

        return FallbackCategory;
    }
}
