using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ComparadorNomes.Tests;

/// <summary>POST /comparar: formato da resposta com pares e score real.</summary>
public class ApiComparacaoTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiComparacaoTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static async Task<JsonElement> Post(HttpClient client, string sistema, string control)
    {
        var resp = await client.PostAsJsonAsync("/comparar", new { sistema, control });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task Similar_DevolvePar_ComScoreReal_NoCampoSimilares_ENosRegistros()
    {
        var json = await Post(_factory.CreateClient(), "Mariana Silva", "Mariana Silvia\nAna\nMariana Silva");

        var par = Assert.Single(json.GetProperty("similares").EnumerateArray());
        Assert.Equal("Mariana Silvia", par.GetProperty("control").GetString());
        Assert.Equal("Mariana Silva", par.GetProperty("sistema").GetString());
        Assert.Equal(92, par.GetProperty("score").GetInt32());     // 1 - 1/14 = 92,86% (truncado)

        var registros = json.GetProperty("registros").EnumerateArray().ToList();
        var similar = Assert.Single(registros, r => r.GetProperty("acao").GetString() == "Similar");
        Assert.Equal("Mariana Silvia", similar.GetProperty("nome").GetString());
        Assert.Equal(92, similar.GetProperty("score").GetInt32());   // não é mais o 80 fixo

        var remover = Assert.Single(registros, r => r.GetProperty("acao").GetString() == "Remover");
        Assert.Equal("Ana", remover.GetProperty("nome").GetString());
        Assert.Equal(0, remover.GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task Ana_x_MarianaSilva_VaiParaRemover()
    {
        var json = await Post(_factory.CreateClient(), "Mariana Silva", "Ana");

        Assert.Equal(new[] { "Ana" }, json.GetProperty("remover").EnumerateArray().Select(e => e.GetString()));
        Assert.Empty(json.GetProperty("similares").EnumerateArray());
    }

    [Fact]
    public async Task OrdemDosTokens_DevolveScore100()
    {
        var json = await Post(_factory.CreateClient(), "João Silva", "Silva João");

        var par = Assert.Single(json.GetProperty("similares").EnumerateArray());
        Assert.Equal(100, par.GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task AcentoEEspacoDuplo_NaoGeraRemocaoNemSimilar()
    {
        var json = await Post(_factory.CreateClient(), "José Silva", "jose  silva");

        Assert.Empty(json.GetProperty("remover").EnumerateArray());
        Assert.Empty(json.GetProperty("similares").EnumerateArray());
        Assert.Empty(json.GetProperty("registros").EnumerateArray());
    }

    [Fact]
    public async Task Limiar_ELidoDaConfiguracao()
    {
        var client = _factory
            .WithWebHostBuilder(b => b.UseSetting("Comparacao:Limiar", "0.95"))
            .CreateClient();

        var json = await Post(client, "Mariana Silva", "Mariana Silvia");

        Assert.Equal(new[] { "Mariana Silvia" }, json.GetProperty("remover").EnumerateArray().Select(e => e.GetString()));
        Assert.Empty(json.GetProperty("similares").EnumerateArray());
    }
}
