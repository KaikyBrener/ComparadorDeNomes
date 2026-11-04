using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ComparadorNomes.Tests;

/// <summary>Leitura das listas pelos endpoints: /limpar e /comparar compartilham o mesmo leitor.</summary>
public class ApiListasTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiListasTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    // ---------- /limpar: tipo nulo, vazio, em branco, desconhecido ou ausente => 400 ----------

    [Theory]
    [InlineData("""{"dados":"Ana","tipo":null}""")]
    [InlineData("""{"dados":"Ana","tipo":""}""")]
    [InlineData("""{"dados":"Ana","tipo":"   "}""")]
    [InlineData("""{"dados":"Ana","tipo":"fabricante-x"}""")]
    [InlineData("""{"dados":"Ana"}""")]
    public async Task Limpar_TipoNuloVazioOuDesconhecido_Retorna400(string corpo)
    {
        var resp = await _client.PostAsync("/limpar", Json(corpo));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("Fonte de dados inválida.", doc.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("sistema")]
    [InlineData("HIKVISION")]
    [InlineData(" control ")]
    [InlineData("intelbras")]
    public async Task Limpar_TiposValidos_Retornam200(string tipo)
    {
        var resp = await _client.PostAsJsonAsync("/limpar", new { dados = "1 | 2 | 3 | Ana", tipo });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Limpar_TabelaMarkdownComPipeNasPontas_Funciona()
    {
        var resp = await _client.PostAsJsonAsync("/limpar", new
        {
            dados = "| Código | Nome |\n|---|---|\n| 1 | Ana Lima |\n| 2 | Bruno |",
            tipo = "control"
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("Ana Lima\nBruno", doc.RootElement.GetProperty("limpo").GetString());
    }

    // ---------- /comparar com os formatos de tabela ----------

    [Fact]
    public async Task Comparar_LeTabelaMarkdownComCelulasVaziasEPipeNoInicio()
    {
        var sistema = "| Código | Nome | Depto |\n|---|---|---|\n| 1 | Ana Lima | TI |\n| 2 | Bruno Costa | RH |";
        var control = "| 12 | | Ana Lima |\n| 13 | | Carlos Souza |";
        // (controle sem cabeçalho: a coluna do nome é a 3ª, com a 2ª vazia)

        var resp = await _client.PostAsJsonAsync("/comparar", new { sistema, control });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "Carlos Souza" },
            doc.RootElement.GetProperty("remover").EnumerateArray().Select(e => e.GetString()));
        Assert.Empty(doc.RootElement.GetProperty("similares").EnumerateArray());
    }

    [Fact]
    public async Task Comparar_ListaSeparadaPorTabEPorEspacos()
    {
        var resp = await _client.PostAsJsonAsync("/comparar", new
        {
            sistema = "1\tAna Lima\n2\tBruno Costa",
            control = "10     Ana Lima\n11     Carlos Souza"
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "Carlos Souza" },
            doc.RootElement.GetProperty("remover").EnumerateArray().Select(e => e.GetString()));
    }
}
