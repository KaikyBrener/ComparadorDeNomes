namespace ComparadorNomes.Tests;

/// <summary>Porta de escuta: variável PORT (Render/Docker), respeitando URLs explícitas.</summary>
public class HospedagemTests
{
    [Theory]
    [InlineData("10000", "http://0.0.0.0:10000")]   // porta típica do Render
    [InlineData("5000", "http://0.0.0.0:5000")]
    [InlineData(" 8080 ", "http://0.0.0.0:8080")]   // espaços ao redor são tolerados
    [InlineData("1", "http://0.0.0.0:1")]
    [InlineData("65535", "http://0.0.0.0:65535")]
    public void PortDefinida_EscutaEmTodasAsInterfacesNessaPorta(string port, string esperado)
        => Assert.Equal(esperado, Hospedagem.ResolverUrl(urlsConfiguradas: null, port));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PortAusente_MantemOPadraoDoKestrel_5000_EmLocalhost(string? port)
        => Assert.Null(Hospedagem.ResolverUrl(urlsConfiguradas: null, port));

    [Theory]
    [InlineData("http://localhost:7000")]
    [InlineData("http://+:9000;https://+:9001")]
    public void UrlsExplicitas_TemPrioridade_EPortEhIgnorada(string urls)
        => Assert.Null(Hospedagem.ResolverUrl(urls, "10000"));

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("80a")]
    [InlineData("1.5")]
    public void PortInvalida_FalhaNaInicializacaoComMensagemClara(string port)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => Hospedagem.ResolverUrl(null, port));
        Assert.Contains("PORT", erro.Message);
    }

    [Fact]
    public void PadraoDocumentado_EhA5000()
        => Assert.Equal(5000, Hospedagem.PortaPadrao);
}
