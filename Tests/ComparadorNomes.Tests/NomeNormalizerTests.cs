namespace ComparadorNomes.Tests;

public class NomeNormalizerTests
{
    [Theory]
    [InlineData("José Silva", "jose silva")]          // acento
    [InlineData("JOSÉ SILVA", "jose silva")]          // caixa
    [InlineData("José  Silva", "jose silva")]         // espaço duplo
    [InlineData("  José\tSilva  ", "jose silva")]     // tab + bordas
    [InlineData("José Silva", "jose silva")]     // espaço não separável
    [InlineData("João   da\t\t Conceição", "joao da conceicao")]
    [InlineData("Muñoz Çelik", "munoz celik")]
    [InlineData("ÁÉÍÓÚ ÀÂÃÊÔÜ", "aeiou aaaeou")]
    public void Normaliza(string entrada, string esperado)
        => Assert.Equal(esperado, NomeNormalizer.Normalizar(entrada));

    [Fact]
    public void JoseSilva_E_jose_silva_SaoIguais()
        => Assert.Equal(NomeNormalizer.Normalizar("José  Silva"), NomeNormalizer.Normalizar("jose silva"));

    [Fact]
    public void FormaComposta_E_Decomposta_SaoIguais()
    {
        var composto = "José";        // é
        var decomposto = "José";     // e + acento combinante
        Assert.Equal(NomeNormalizer.Normalizar(composto), NomeNormalizer.Normalizar(decomposto));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   \t ", "")]
    public void VazioOuNulo_ViraVazio(string? entrada, string esperado)
        => Assert.Equal(esperado, NomeNormalizer.Normalizar(entrada!));

    // ---------- Normalização usada para COMPARAR (partículas, pontuação) ----------

    [Fact]
    public void Normalizar_Estrita_MantemParticulas()
        => Assert.Equal("joao da silva", NomeNormalizer.Normalizar("João da Silva"));

    [Theory]
    [InlineData("João da Silva", "joao silva")]
    [InlineData("Maria de Souza e Silva", "maria souza silva")]
    [InlineData("Ana DOS Santos", "ana santos")]
    [InlineData("Pedro das Neves", "pedro neves")]
    [InlineData("José do Carmo", "jose carmo")]
    [InlineData("Maria-José", "maria jose")]
    [InlineData("Jr.", "jr")]
    [InlineData("João P. Silva", "joao p silva")]
    [InlineData("D'Ávila  Neto", "d avila neto")]
    [InlineData("E", "e")]               // só partículas: mantém, para não esvaziar o nome
    [InlineData("de da", "de da")]
    [InlineData("---", "---")]           // sem letras/dígitos: devolve a normalização simples
    [InlineData("   ", "")]
    public void NormalizarParaComparacao(string entrada, string esperado)
        => Assert.Equal(esperado, NomeNormalizer.NormalizarParaComparacao(entrada));

    [Fact]
    public void NormalizarParaComparacao_Nulo_ViraVazio()
        => Assert.Equal("", NomeNormalizer.NormalizarParaComparacao(null!));

    [Fact]
    public void ResultadoNaoTemMarcasDeAcento()
    {
        var r = NomeNormalizer.Normalizar("Ação Ünïcödé");
        Assert.Equal("acao unicode", r);
        Assert.True(r.IsNormalized(System.Text.NormalizationForm.FormC));
    }
}
