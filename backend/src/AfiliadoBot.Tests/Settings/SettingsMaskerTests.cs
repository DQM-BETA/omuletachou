using AfiliadoBot.Api.Settings;
using FluentAssertions;

namespace AfiliadoBot.Tests.Settings;

public class SettingsMaskerTests
{
    [Theory]
    [InlineData("telegram.bot_token")]
    [InlineData("amazon.access_key")]
    [InlineData("mercadolivre.client_secret")]
    [InlineData("hangfire.dashboard_password")]
    [InlineData("TELEGRAM.BOT_TOKEN")]
    public void IsSensitive_ChaveComSufixoSensivel_RetornaTrue(string key)
    {
        SettingsMasker.IsSensitive(key).Should().BeTrue();
    }

    [Theory]
    [InlineData("claude.min_score")]
    [InlineData("schedule.collector_cron")]
    [InlineData("networks.telegram.enabled")]
    public void IsSensitive_ChaveSemSufixoSensivel_RetornaFalse(string key)
    {
        SettingsMasker.IsSensitive(key).Should().BeFalse();
    }

    [Fact]
    public void Mask_ValorLongo_Retorna16AsteriscosFixosMaisUltimos4Caracteres()
    {
        SettingsMasker.Mask("sk-live-abcdef1234567890a1b2").Should().Be("****************a1b2");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("abcd")]
    public void Mask_ValorCurto_NaoRevelaValorReal_MascaraTotalmente(string value)
    {
        // Regressao Issue #131: valores com Length <= 4 nao podem revelar o valor
        // completo em claro apos o prefixo de asteriscos.
        var result = SettingsMasker.Mask(value);

        result.Should().NotBeNull();
        result.Should().NotContain(value);
        result.Should().MatchRegex("^\\*+$");
    }

    [Fact]
    public void Mask_ValorNuloOuVazio_RetornaNull_NuncaMascaraStringVazia()
    {
        SettingsMasker.Mask(null).Should().BeNull();
        SettingsMasker.Mask("").Should().BeNull();
    }

    [Fact]
    public void ApplyIfSensitive_ChaveNaoSensivelComValor_RetornaValorSemMascara()
    {
        SettingsMasker.ApplyIfSensitive("claude.min_score", "7").Should().Be("7");
    }

    [Fact]
    public void ApplyIfSensitive_ChaveNaoSensivelComValorVazio_RetornaNull()
    {
        SettingsMasker.ApplyIfSensitive("claude.min_score", "").Should().BeNull();
    }
}
