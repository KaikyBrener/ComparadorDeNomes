using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ComparadorNomes.Tests;

/// <summary>Testes da Etapa 1: superfície HTTP, validação de entrada e tratamento de erros.</summary>
public class ApiSegurancaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiSegurancaTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    // ---------- Endpoints de log removidos do servidor ----------

    [Theory]
    [InlineData("GET", "/log")]
    [InlineData("GET", "/log/download")]
    [InlineData("POST", "/log/limpar")]
    public async Task EndpointsDeLog_NaoExistemMais(string metodo, string caminho)
    {
        var resp = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), caminho));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---------- CORS ----------

    [Fact]
    public async Task Cors_NaoLiberaQualquerOrigem()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/comparar")
        {
            Content = Json("""{"sistema":"Ana","control":"Ana"}""")
        };
        req.Headers.Add("Origin", "https://site-malicioso.example");

        var resp = await _client.SendAsync(req);

        Assert.False(resp.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // ---------- Validação de entrada ----------

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("""{"sistema":"Ana"}""")]
    [InlineData("""{"sistema":"","control":"Ana"}""")]
    [InlineData("""{"sistema":"Ana","control":"   "}""")]
    [InlineData("""{"sistema":123,"control":"Ana"}""")]
    public async Task Comparar_EntradaInvalida_Retorna400ComErroGenerico(string corpo)
    {
        var resp = await _client.PostAsync("/comparar", Json(corpo));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var texto = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(texto);
        var erro = doc.RootElement.GetProperty("error").GetString();
        Assert.False(string.IsNullOrWhiteSpace(erro));
        // Nunca vazar detalhes internos do parser/framework para o cliente
        Assert.DoesNotContain("System.", texto);
        Assert.DoesNotContain("JsonException", texto);
        Assert.DoesNotContain("   at ", texto);
    }

    [Theory]
    [InlineData("""{"dados":"Ana","tipo":"fabricante-inexistente"}""")]
    [InlineData("""{"dados":"Ana"}""")]
    [InlineData("""{"dados":"","tipo":"sistema"}""")]
    [InlineData("nao e json")]
    public async Task Limpar_EntradaInvalida_Retorna400ComCampoError(string corpo)
    {
        var resp = await _client.PostAsync("/limpar", Json(corpo));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task Comparar_CorpoMaiorQueOLimite_Retorna413()
    {
        var grande = new string('a', 2 * 1024 * 1024 + 10);
        var resp = await _client.PostAsync("/comparar",
            Json(JsonSerializer.Serialize(new { sistema = grande, control = "Ana" })));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Limpar_CorpoMaiorQueOLimite_Retorna413()
    {
        var grande = new string('a', 2 * 1024 * 1024 + 10);
        var resp = await _client.PostAsync("/limpar",
            Json(JsonSerializer.Serialize(new { dados = grande, tipo = "sistema" })));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resp.StatusCode);
    }

    // ---------- Caminho feliz continua funcionando ----------

    [Fact]
    public async Task Comparar_EntradaValida_RetornaListas()
    {
        var resp = await _client.PostAsJsonAsync("/comparar", new { sistema = "Ana\nBruno", control = "Ana\nCarlos" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var remover = doc.RootElement.GetProperty("remover").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new[] { "Carlos" }, remover);
    }

    [Fact]
    public async Task Limpar_EntradaValida_RetornaTextoLimpo()
    {
        var resp = await _client.PostAsJsonAsync("/limpar", new { dados = "Ana\nBruno\nAna", tipo = "intelbras" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("Ana\nBruno", doc.RootElement.GetProperty("limpo").GetString());
    }

}
