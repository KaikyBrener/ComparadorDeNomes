namespace ComparadorNomes.Tests;

public class LeitorDeListaTests
{
    private static string[] Nomes(string texto) => LeitorDeLista.ExtrairNomes(texto).ToArray();

    // ---------- Células vazias não deslocam as colunas ----------

    [Fact]
    public void CelulaVaziaNoMeio_NaoDesloca_E_LinhaComecandoComPipeNaoEhDescartada()
        => Assert.Equal(new[] { "João" }, Nomes("| 12 | | João |"));

    [Theory]
    [InlineData("1 | 2 | | Ana")]          // coluna 3 preenchida, coluna 2 vazia
    [InlineData("| 1 | 2 | | Ana |")]
    [InlineData("1 | Ana | |")]            // colunas finais vazias: cai para a coluna do nome preenchida
    public void CelulasVazias_NaoImpedemAchar_O_Nome(string linha)
        => Assert.Equal(new[] { "Ana" }, Nomes(linha));

    [Fact]
    public void LinhasComPipeNoInicio_SaoTodasLidas()
    {
        var texto = "| 1 | Ana Lima |\n| 2 | Bruno Costa |\n| 3 | Carla Dias |";

        Assert.Equal(new[] { "Ana Lima", "Bruno Costa", "Carla Dias" }, Nomes(texto));
    }

    // ---------- Tabela com cabeçalho ----------

    [Fact]
    public void TabelaMarkdownComCabecalho_UsaAColunaNome()
    {
        var texto = string.Join("\n",
            "| Código | Nome | Depto |",
            "|---|:---:|---|",
            "| 1 | Ana Lima | TI |",
            "| 2 | Bruno Costa | RH |");

        Assert.Equal(new[] { "Ana Lima", "Bruno Costa" }, Nomes(texto));
    }

    [Fact]
    public void TabelaSemBordas_ComCabecalhoNome()
    {
        var texto = "Codigo | Nome\n---|---\n10 | Ana\n11 | Bruno";

        Assert.Equal(new[] { "Ana", "Bruno" }, Nomes(texto));
    }

    [Fact]
    public void CabecalhoComNomeNaPrimeiraColuna()
    {
        var texto = "Nome | Depto\nAna | TI\nBruno | RH";

        Assert.Equal(new[] { "Ana", "Bruno" }, Nomes(texto));
    }

    [Fact]
    public void CabecalhoSimples_EhIgnorado()
        => Assert.Equal(new[] { "Ana", "Bruno" }, Nomes("Nome\nAna\nBruno"));

    [Fact]
    public void PrimeiraLinhaQueEhNome_NaoEhTomadaPorCabecalho()
        => Assert.Equal(new[] { "Ana Lima", "Bruno" }, Nomes("Ana Lima\nBruno"));

    // ---------- Uma coluna só ----------

    [Fact]
    public void UmaColuna_AceitaLinhasEmBrancoEEspacosNasPontas()
        => Assert.Equal(new[] { "Ana", "Bruno", "Carlos" }, Nomes("Ana\r\n\r\n  Bruno \n\t\n  Carlos  "));

    [Fact]
    public void UmaColuna_ComEspacoDuploNoMeio_NaoTrunca()
        => Assert.Equal(new[] { "José Silva" }, Nomes("José  Silva"));

    [Fact]
    public void UmaColunaComPipes_Sozinhos()
        => Assert.Equal(new[] { "Ana", "Bruno" }, Nomes("| Ana |\n| Bruno |"));

    // ---------- Separadores: tab e 2+ espaços ----------

    [Theory]
    [InlineData("12\tJoão da Silva")]
    [InlineData("12\t\tJoão da Silva")]            // célula vazia entre tabs
    [InlineData("12   João da Silva")]             // 2+ espaços
    [InlineData("12      João da Silva")]
    [InlineData("João da Silva\t12")]              // número depois do nome
    public void TabEDoisOuMaisEspacos_SeparamAsColunas(string linha)
        => Assert.Equal(new[] { "João da Silva" }, Nomes(linha));

    [Fact]
    public void ColunasSeparadasPorTab_ComCabecalho()
    {
        var texto = "Codigo\tNome\tDepto\n1\tAna Lima\tTI\n2\tBruno\tRH";

        Assert.Equal(new[] { "Ana Lima", "Bruno" }, Nomes(texto));
    }

    // ---------- Vazio ----------

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    [InlineData("|---|---|\n| | |")]
    public void SemNomes_ListaVazia(string texto)
        => Assert.Empty(Nomes(texto));

    // ---------- Limpar (por fabricante) ----------

    [Fact]
    public void Limpar_Sistema_PegaAQuartaColuna()
        => Assert.Equal(new[] { "Ana Lima", "Bruno" },
            LeitorDeLista.Limpar("| 1 | 10 | TI | Ana Lima |\n2 | 11 | RH | Bruno", "sistema"));

    [Fact]
    public void Limpar_Control_PegaASegundaColuna_ComLinhasQueComecamComPipe()
        => Assert.Equal(new[] { "Ana Lima", "Bruno" },
            LeitorDeLista.Limpar("| 1 | Ana Lima |\n| 2 | Bruno |", "control"));

    [Fact]
    public void Limpar_Control_ComTabEDoisEspacos()
        => Assert.Equal(new[] { "Ana Lima", "Bruno" },
            LeitorDeLista.Limpar("1\tAna Lima\n2    Bruno", "control"));

    [Fact]
    public void Limpar_Intelbras_PrimeiraColuna()
        => Assert.Equal(new[] { "Ana", "Bruno" }, LeitorDeLista.Limpar("Ana\nBruno", "intelbras"));

    [Fact]
    public void Limpar_Hikvision_IdEPipeNome()
        => Assert.Equal(new[] { "12 | Ana", "13 | Bruno" },
            LeitorDeLista.Limpar("Person ID | Employee | Name\n12 | x | Ana\n| 13 | y | Bruno |", "hikvision"));

    [Fact]
    public void Limpar_Hikvision_LinhaSemIdNumericoEhIgnorada()
        => Assert.Equal(new[] { "12 | Ana" },
            LeitorDeLista.Limpar("abc | x | Fulano\n12 | x | Ana", "hikvision"));

    [Fact]
    public void Limpar_RemoveDuplicatas_SemDiferenciarCaixa_EPulaCabecalhoESeparador()
        => Assert.Equal(new[] { "Ana", "Bruno" },
            LeitorDeLista.Limpar("Nome\n-----\nAna\nANA\nBruno\nana", "intelbras"));

    [Fact]
    public void Limpar_CelulaVazia_NaoDesloca()
        => Assert.Equal(new[] { "Ana" }, LeitorDeLista.Limpar("| 1 | | 3 | Ana |", "sistema"));

    [Fact]
    public void Limpar_ColunaDoNomeVazia_NaoPegaOutraColuna()
        => Assert.Empty(LeitorDeLista.Limpar("| 1 | | 3 | |", "sistema"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("xyz")]
    public void Limpar_TipoInvalido_Lanca(string? tipo)
        => Assert.Throws<ArgumentException>(() => LeitorDeLista.Limpar("Ana", tipo!));

    [Theory]
    [InlineData("SISTEMA")]
    [InlineData(" Control ")]
    public void Limpar_TipoNaoDiferenciaCaixa(string tipo)
        => Assert.NotNull(LeitorDeLista.Limpar("1 | 2 | 3 | Ana", tipo));
}
