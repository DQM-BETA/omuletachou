namespace AfiliadoBot.Domain.Services;

/// <summary>
/// Deteccao de categoria/subcategoria por palavras-chave no titulo do produto (Issue #167).
/// Classe estatica, sem dependencia de IA/banco — regra de negocio pura, por isso vive em
/// Domain (movida de Application para resolver dependencia circular: os 3 collectors, que
/// precisam chama-la em CollectAsync, moram em Infrastructure, que nao referencia Application).
/// Comparacao case-insensitive; primeira categoria/subcategoria com match (na ordem de
/// declaracao do dicionario) vence; sem nenhum match -> fallback ("Geral", null).
/// </summary>
public static class CategoryDetector
{
    private const string FallbackCategory = "Geral";

    // Dicionario de 2 niveis: Categoria -> Subcategoria -> lista de keywords.
    private static readonly Dictionary<string, Dictionary<string, List<string>>> Taxonomia = new()
    {
        ["Eletrodomésticos"] = new()
        {
            ["Refrigeração"] = new() { "geladeira", "refrigerador", "frigobar", "freezer" },
            ["Lavanderia"] = new() { "máquina de lavar", "maquina de lavar", "lavadora", "secadora de roupas", "tanquinho" },
            ["Fogões e Fornos"] = new() { "fogão", "fogao", "forno elétrico", "forno eletrico", "cooktop", "forno de embutir" },
            ["Micro-ondas"] = new() { "micro-ondas", "microondas", "forno micro-ondas" },
            ["Adega e Bebidas"] = new() { "adega climatizada", "purificador de água", "purificador de agua", "bebedouro" },
        },
        ["Climatização"] = new()
        {
            ["Ar-condicionado"] = new() { "ar-condicionado", "ar condicionado", "split hi-wall", "ar-condicionado portátil", "ar-condicionado portatil" },
            ["Ventilação"] = new() { "ventilador", "circulador de ar", "exaustor" },
            ["Aquecimento"] = new() { "aquecedor", "aquecedor a gás", "aquecedor a gas", "calefator" },
            ["Umidificação"] = new() { "umidificador", "desumidificador", "purificador de ar" },
        },
        ["Ferramentas"] = new()
        {
            ["Ferramentas Elétricas"] = new() { "furadeira", "parafusadeira", "esmerilhadeira", "serra elétrica", "serra eletrica", "lixadeira" },
            ["Ferramentas Manuais"] = new() { "chave de fenda", "alicate", "martelo", "chave inglesa", "jogo de chaves" },
            ["Medição e Precisão"] = new() { "trena", "nível a laser", "nivel a laser", "paquímetro", "paquimetro", "multímetro", "multimetro" },
            ["Jardim e Área Externa"] = new() { "cortador de grama", "motosserra", "soprador de folhas", "roçadeira", "roçadeira" },
        },
        ["Eletrônicos"] = new()
        {
            ["Celulares e Smartphones"] = new() { "smartphone", "celular", "iphone", "capinha" },
            ["Áudio"] = new() { "fone", "headphone", "earphone", "caixa de som", "soundbar" },
            ["Informática"] = new() { "notebook", "tablet", "mouse", "teclado", "monitor", "pendrive" },
            ["TV e Imagem"] = new() { "tv", "smart tv", "chromecast", "projetor" },
            ["Fotografia"] = new() { "câmera", "camera", "filmadora", "tripé", "tripe" },
        },
        ["Casa e Cozinha"] = new()
        {
            ["Eletroportáteis"] = new() { "airfryer", "liquidificador", "batedeira", "sanduicheira", "cafeteira" },
            ["Panelas e Utensílios"] = new() { "panela", "frigideira", "jogo de panelas", "utensílio de cozinha", "utensilio de cozinha" },
            ["Limpeza Doméstica"] = new() { "aspirador de pó", "aspirador de po", "vassoura", "rodo", "balde" },
            ["Cama, Mesa e Banho"] = new() { "travesseiro", "edredom", "jogo de cama", "toalha de banho" },
            ["Decoração e Móveis"] = new() { "sofá", "sofa", "cortina", "quadro decorativo", "tapete", "decoração", "decoracao" },
        },
        ["Beleza"] = new()
        {
            ["Perfumaria"] = new() { "perfume", "colônia", "colonia", "deo colônia", "deo colonia" },
            ["Maquiagem"] = new() { "maquiagem", "batom", "base facial", "rímel", "rimel", "sombra de olhos" },
            ["Cuidados com a Pele"] = new() { "creme facial", "hidratante", "protetor solar", "sérum facial", "serum facial" },
            ["Cuidados com o Cabelo"] = new() { "shampoo", "condicionador", "chapinha", "secador de cabelo" },
        },
        ["Moda"] = new()
        {
            ["Roupas Femininas"] = new() { "vestido", "saia", "blusa feminina" },
            ["Roupas Masculinas"] = new() { "camisa", "camiseta masculina", "bermuda" },
            ["Calçados"] = new() { "tênis", "tenis", "sapato", "sandália", "sandalia", "chinelo" },
            ["Acessórios"] = new() { "bolsa", "relógio", "relogio", "óculos de sol", "oculos de sol", "cinto" },
        },
        ["Brinquedos"] = new()
        {
            ["Bonecas e Bonecos"] = new() { "boneca", "boneco de ação", "boneco de acao", "barbie" },
            ["Blocos de Montar"] = new() { "lego", "blocos de montar", "quebra-cabeça", "quebra-cabeca" },
            ["Jogos e Brincadeiras"] = new() { "jogo de tabuleiro", "videogame infantil", "pelúcia", "pelucia" },
            ["Veículos de Brinquedo"] = new() { "carrinho de brinquedo", "controle remoto", "pista de carrinho" },
        },
        // "Geral" nao entra no dicionario — e o fallback quando nenhuma keyword casa.
    };

    /// <summary>
    /// Taxonomia fixa (Categoria -> lista de Subcategorias), exposta para reuso por quem monta o
    /// prompt do fallback de categorizacao via IA (ClaudeAiService.ClassifyCategoryAsync, Issue
    /// #167 — Sub-B/#169) — evita duplicar a lista de categorias/subcategorias em dois lugares
    /// (especificacao-tecnica.md §7 exige reaproveitar esta mesma lista).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Categorias { get; } =
        Taxonomia.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value.Keys.ToList());

    public static (string Category, string? Subcategory) Detect(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return (FallbackCategory, null);

        foreach (var (categoria, subcategorias) in Taxonomia)
        {
            foreach (var (subcategoria, keywords) in subcategorias)
            {
                foreach (var keyword in keywords)
                {
                    if (title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        return (categoria, subcategoria);
                }
            }
        }

        return (FallbackCategory, null);
    }
}
