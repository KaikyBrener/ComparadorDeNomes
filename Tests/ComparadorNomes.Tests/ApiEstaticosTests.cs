using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ComparadorNomes.Tests;

/// <summary>/health, cabeçalhos de segurança e entrega do front compilado (wwwroot).</summary>
public class ApiEstaticosTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string CspEsperada =
        "default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self' data:; " +
        "connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "wwwroot-teste-" + Guid.NewGuid().ToString("N"));

    public ApiEstaticosTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><title>Comparador</title>");
        File.WriteAllText(Path.Combine(_webRoot, "limpeza.html"), "<!doctype html><title>Limpeza</title>");
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    private HttpClient ClienteComFront() =>
        _factory.WithWebHostBuilder(b => b.UseSetting("webroot", _webRoot)).CreateClient();

    private static void AssertCabecalhosDeSeguranca(HttpResponseMessage resp)
    {
        Assert.Equal(CspEsperada, Assert.Single(resp.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(resp.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(resp.Headers.GetValues("Referrer-Policy")));
    }

    // ---------- /health ----------

    [Fact]
    public async Task Health_Retorna200ComStatusOk()
    {
        var resp = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }

    // ---------- Cabeçalhos de segurança em todas as respostas ----------

    [Fact]
    public async Task Cabecalhos_NaRespostaDaApi()
        => AssertCabecalhosDeSeguranca(await _factory.CreateClient().GetAsync("/health"));

    [Fact]
    public async Task Cabecalhos_NoNotFound()
        => AssertCabecalhosDeSeguranca(await _factory.CreateClient().GetAsync("/nao-existe"));

    [Fact]
    public async Task Cabecalhos_NasRespostasDeErroTratadas()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/comparar", new { sistema = "", control = "" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        AssertCabecalhosDeSeguranca(resp);

        // erro vindo do handler global de exceções (JSON inválido)
        var invalido = await _factory.CreateClient().PostAsync("/comparar",
            new StringContent("{ruim", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        AssertCabecalhosDeSeguranca(invalido);
    }

    [Fact]
    public void Csp_NaoUsaUnsafeInlineNemUnsafeEval()
    {
        Assert.DoesNotContain("unsafe-inline", CspEsperada);
        Assert.DoesNotContain("unsafe-eval", CspEsperada);
    }

    // ---------- Front compilado ----------

    [Fact]
    public async Task Raiz_ServeOIndexHtml_ComCabecalhos()
    {
        var resp = await ClienteComFront().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("Comparador", await resp.Content.ReadAsStringAsync());
        AssertCabecalhosDeSeguranca(resp);
    }

    [Fact]
    public async Task OutrasPaginas_SaoServidasPeloNome()
    {
        var resp = await ClienteComFront().GetAsync("/limpeza.html");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("Limpeza", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApiContinuaFuncionandoComOFrontServido()
    {
        var resp = await ClienteComFront().PostAsJsonAsync("/comparar", new { sistema = "Ana", control = "Bruno" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
