using System.Net;
using System.Net.Http.Headers;
using AfiliadoBot.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AfiliadoBot.Tests;

/// <summary>
/// Boot de integracao (Issue #9 / #73) + protecao [Authorize] (Issue #11 / Sub-C, CA-C7/C8/C9/C10):
/// confirma que o container de DI resolve corretamente todas as dependencias registradas em
/// Program.cs — incluindo o <c>AddHttpClient&lt;ISocialPublisher, InstagramPublisher&gt;()</c>
/// (consumido via <c>IEnumerable&lt;ISocialPublisher&gt;</c> pelo <c>PublisherJob</c>) e o
/// <c>UseStaticFiles</c> mapeado para <c>/media</c> — sem depender de containers Docker/Postgres
/// reais (usa <see cref="CustomWebApplicationFactory"/>, InMemory database). Os endpoints de
/// trigger agora vivem em <c>JobsController</c> e exigem token valido.
/// </summary>
public class JobsTriggerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public JobsTriggerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static HttpClient AuthenticatedClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestTokenFactory.CreateValidToken());
        return client;
    }

    [Fact]
    public async Task PostProcessorTrigger_ComTokenValido_RetornaOk_SemExcecaoDeBootDoDI()
    {
        var client = AuthenticatedClient(_factory.CreateClient());

        var response = await client.PostAsync("/api/jobs/processor/trigger", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostProcessorTrigger_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/jobs/processor/trigger", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostPublisherTrigger_ComTokenValido_RetornaOk_ResolveTodosOsISocialPublisherRegistrados()
    {
        var client = AuthenticatedClient(_factory.CreateClient());

        // Resolve IEnumerable<ISocialPublisher> (Telegram + Youtube + Instagram) via DI real —
        // se algum publisher tiver dependencia nao registrada, o boot/instanciamento falha aqui.
        var response = await client.PostAsync("/api/jobs/publisher/trigger", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostPublisherTrigger_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/jobs/publisher/trigger", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostCollectorTrigger_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/jobs/collector/trigger", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMedia_NaoRetorna500_ConfirmaUseStaticFilesConfigurado()
    {
        var client = _factory.CreateClient();

        // Arquivo inexistente -> 404 esperado (nao 500), confirmando que o middleware
        // UseStaticFiles/PhysicalFileProvider foi registrado sem lancar excecao no boot.
        var response = await client.GetAsync("/media/arquivo-inexistente.mp4");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
