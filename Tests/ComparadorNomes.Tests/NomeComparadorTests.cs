using Xunit.Abstractions;

namespace ComparadorNomes.Tests;

public class NomeComparadorTests
{
    private readonly ITestOutputHelper _saida;
    public NomeComparadorTests(ITestOutputHelper saida) => _saida = saida;

    private static ResultadoComparacao Comparar(string[] sistema, string[] control, double limiar = NomeComparador.LimiarPadrao)
        => new NomeComparador(limiar).Comparar(sistema, control);

    // ---------- Iguais após normalização: não remove e não vira "similar" ----------

    [Theory]
    [InlineData("José Silva", "jose silva")]        // acento + caixa
    [InlineData("José  Silva", "jose silva")]       // espaço duplo
    [InlineData("MARIA DA SILVA", "Maria da Silva")]
    [InlineData("Ana\tPaula", "ana paula")]
    public void IguaisNormalizados_NaoRemoveENaoApareceEmSimilares(string sistema, string control)
    {
        var r = Comparar(new[] { sistema }, new[] { control });

        Assert.Empty(r.Remover);
        Assert.Empty(r.Similares);
    }

    // ---------- Ordem dos tokens ----------

    [Fact]
    public void OrdemDiferente_EhSimilarComScore100()
    {
        var r = Comparar(new[] { "João Silva" }, new[] { "Silva João" });

        Assert.Empty(r.Remover);
        var par = Assert.Single(r.Similares);
        Assert.Equal("Silva João", par.NomeControl);
        Assert.Equal("João Silva", par.NomeSistema);
        Assert.Equal(1.0, par.Score, 4);
    }

    // ---------- O caso que o Contains errava ----------

    [Fact]
    public void Ana_x_MarianaSilva_VaiParaRemover()
    {
        var r = Comparar(new[] { "Mariana Silva" }, new[] { "Ana" });

        Assert.Equal(new[] { "Ana" }, r.Remover);
        Assert.Empty(r.Similares);
    }

    [Fact]
    public void NomeContidoEmOutroMaior_NaoEhSimilar()
    {
        var r = Comparar(new[] { "Carlos Eduardo Pereira Lima" }, new[] { "Carlos Eduardo" });

        Assert.Equal(new[] { "Carlos Eduardo" }, r.Remover);
        Assert.Empty(r.Similares);
    }

    // ---------- Similaridade real ----------

    [Fact]
    public void ErroDeDigitacao_EhSimilar_ComScoreReal_ENomesDosDoisLados()
    {
        var r = Comparar(new[] { "Mariana Silva" }, new[] { "Mariana Silvia" });

        Assert.Empty(r.Remover);
        var par = Assert.Single(r.Similares);
        Assert.Equal("Mariana Silvia", par.NomeControl);
        Assert.Equal("Mariana Silva", par.NomeSistema);
        Assert.Equal(1.0 - 1.0 / 14, par.Score, 4);       // 1 edição em 14 caracteres
        Assert.InRange(par.Score, 0.85, 0.9999);
    }

    [Fact]
    public void Similaridade_Levenshtein_NormalizadoPeloMaiorTamanho()
    {
        Assert.Equal(1.0 - 3.0 / 7, NomeComparador.Similaridade("kitten", "sitting"), 6);
        Assert.Equal(1.0, NomeComparador.Similaridade("abc", "abc"), 6);
        Assert.Equal(0.0, NomeComparador.Similaridade("abc", "xyz"), 6);
        Assert.Equal(0.0, NomeComparador.Similaridade("", "abc"), 6);
    }

    [Fact]
    public void Similaridade_EhSimetrica_E_ConsideraOrdemDosTokens()
    {
        Assert.Equal(1.0, NomeComparador.Similaridade("ana paula souza", "souza ana paula"), 6);
        Assert.Equal(NomeComparador.Similaridade("carlos silva", "carlos silvio"),
                     NomeComparador.Similaridade("carlos silvio", "carlos silva"), 6);
    }

    // ---------- Melhor candidato, não o primeiro ----------

    [Fact]
    public void EscolheOMelhorCandidato_NaoOPrimeiro()
    {
        // "Silva" (3 edições = 0,85) vem primeiro e já passa do limiar; "Sousa" (1 edição = 0,95) é melhor.
        var r = Comparar(
            new[] { "Carlos Eduardo Silva", "Carlos Eduardo Sousa" },
            new[] { "Carlos Eduardo Souza" });

        var par = Assert.Single(r.Similares);
        Assert.Equal("Carlos Eduardo Sousa", par.NomeSistema);
        Assert.Equal(0.95, par.Score, 4);
    }

    // ---------- Partículas, hífen, pontuação e iniciais ----------

    [Theory]
    [InlineData("João Silva", "João da Silva")]                 // partícula só no Control
    [InlineData("João da Silva", "João Silva")]                 // partícula só no sistema
    [InlineData("Maria de Souza e Silva", "Maria Souza Silva")]
    [InlineData("Maria José", "Maria-José")]
    [InlineData("Maria-José", "Maria José")]
    [InlineData("Carlos Jr", "Carlos Jr.")]
    [InlineData("Carlos Jr.", "Carlos Jr")]
    public void ParticulasHifenEPontuacao_SaoSimilares_ComScoreCheio(string sistema, string control)
    {
        var r = Comparar(new[] { sistema }, new[] { control });

        Assert.Empty(r.Remover);
        var par = Assert.Single(r.Similares);
        Assert.Equal(control, par.NomeControl);
        Assert.Equal(sistema, par.NomeSistema);
        Assert.Equal(1.0, par.Score, 4);
    }

    [Theory]
    [InlineData("João Pedro Silva", "João P. Silva")]
    [InlineData("João P. Silva", "João Pedro Silva")]
    [InlineData("João P Silva", "João Pedro Silva")]
    [InlineData("J. Silva", "João Silva")]
    [InlineData("Ana Maria Souza", "Ana M. Souza")]
    public void Iniciais_CasamComONomeCompleto_ComoSimilar(string sistema, string control)
    {
        var r = Comparar(new[] { sistema }, new[] { control });

        Assert.Empty(r.Remover);
        var par = Assert.Single(r.Similares);
        Assert.Equal(sistema, par.NomeSistema);
        Assert.Equal(control, par.NomeControl);
        Assert.InRange(par.Score, 0.85, 0.99);   // nunca 100%: não é o mesmo nome escrito
    }

    [Theory]
    [InlineData("João Carlos Silva", "João P. Silva")]     // P não é inicial de Carlos
    [InlineData("João Pedro Silva", "João P. Souza")]      // sobrenome diferente
    [InlineData("João Pedro Silva", "P. Silva")]           // número de tokens diferente
    public void Iniciais_QueNaoBatem_VaoParaRemover(string sistema, string control)
    {
        var r = Comparar(new[] { sistema }, new[] { control });

        Assert.Equal(new[] { control }, r.Remover);
        Assert.Empty(r.Similares);
    }

    [Fact]
    public void NomeSimplesComUmaLetra_NaoViraInicialDeQualquerNome()
    {
        var r = Comparar(new[] { "Joana" }, new[] { "J" });

        Assert.Equal(new[] { "J" }, r.Remover);
    }

    // ---------- Nomes curtos ----------

    [Theory]
    [InlineData("Ana", "Anna")]       // 1 edição, 0,75: abaixo do limiar, mas é nome curto
    [InlineData("Anna", "Ana")]
    [InlineData("Ana", "Ane")]
    [InlineData("Paulo", "Paula")]    // 5 letras: ainda vale a regra
    public void NomeCurtoComUmaEdicao_EhSimilar_ComScoreReal(string sistema, string control)
    {
        var r = Comparar(new[] { sistema }, new[] { control });

        Assert.Empty(r.Remover);
        var par = Assert.Single(r.Similares);
        Assert.Equal(sistema, par.NomeSistema);
        Assert.Equal(control, par.NomeControl);
        Assert.True(par.Score < NomeComparador.LimiarPadrao, "o score continua sendo o real, abaixo do limiar");
        Assert.True(par.Score > 0);
    }

    [Theory]
    [InlineData("Li", "Lu")]             // 1 edição, mas só 2 letras: abaixo do mínimo de 3
    [InlineData("Jo", "Joa")]            // 1 edição, mas o nome tem 2 letras
    [InlineData("Jo", "Joao")]           // 2 edições
    [InlineData("Ana", "Bia")]           // 2 edições
    [InlineData("Carlos", "Carlas")]     // 1 edição, mas 6 letras e score 0,83 < limiar
    [InlineData("Marcos", "Marcus")]     // 1 edição, mas 6 letras e score 0,83 < limiar
    public void NomeCurtoComDuasEdicoes_OuNomeLongo_VaiParaRemover(string sistema, string control)
    {
        var r = Comparar(new[] { sistema }, new[] { control });

        Assert.Equal(new[] { control }, r.Remover);
        Assert.Empty(r.Similares);
    }

    [Fact]
    public void RegraDeNomeCurto_NaoDependeDoLimiar()
    {
        var r = Comparar(new[] { "Ana" }, new[] { "Anna" }, limiar: 0.99);

        Assert.Single(r.Similares);
    }

    [Fact]
    public void NomeCurtoIgual_NaoRemove()
    {
        var r = Comparar(new[] { "Li" }, new[] { "LI" });

        Assert.Empty(r.Remover);
        Assert.Empty(r.Similares);
    }

    // ---------- Limiar configurável ----------

    [Fact]
    public void LimiarMaisAlto_MandaParaRemoverOQueAntesEraSimilar()
    {
        var sistema = new[] { "Mariana Silva" };
        var control = new[] { "Mariana Silvia" };

        Assert.Single(Comparar(sistema, control, 0.85).Similares);

        var r = Comparar(sistema, control, 0.95);
        Assert.Equal(new[] { "Mariana Silvia" }, r.Remover);
        Assert.Empty(r.Similares);
    }

    [Fact]
    public void ScoreExatamenteNoLimiar_ContaComoSimilar()
    {
        // 3 edições em 20 caracteres = exatamente 0,85 (sem erro de ponto flutuante)
        var r = Comparar(new[] { "Carlos Eduardo Silva" }, new[] { "Carlos Eduardo Souza" });

        Assert.Single(r.Similares);
        Assert.Empty(r.Remover);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void LimiarInvalido_Lanca(double limiar)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new NomeComparador(limiar));

    // ---------- Duplicatas ----------

    [Fact]
    public void DuplicatasNaSaida_SaoRemovidas()
    {
        var r = Comparar(
            new[] { "Mariana Silva" },
            new[] { "Pedro Souza", "pedro  souza", "PEDRO SOUZA", "Mariana Silvia", "mariana silvia" });

        Assert.Equal(new[] { "Pedro Souza" }, r.Remover);       // mantém a primeira grafia
        var par = Assert.Single(r.Similares);
        Assert.Equal("Mariana Silvia", par.NomeControl);
    }

    [Fact]
    public void DuplicatasNoSistema_NaoAfetamResultado()
    {
        var r = Comparar(new[] { "Ana Lima", "ana lima", "ANA LIMA" }, new[] { "Ana Lima", "Bia Costa" });

        Assert.Equal(new[] { "Bia Costa" }, r.Remover);
        Assert.Empty(r.Similares);
    }

    [Fact]
    public void Remover_PreservaOrdemDeEntrada()
    {
        var r = Comparar(new[] { "Zeca" }, new[] { "Bruno Dias", "Alice Rocha", "Carla Nunes" });

        Assert.Equal(new[] { "Bruno Dias", "Alice Rocha", "Carla Nunes" }, r.Remover);
    }

    // ---------- Listas vazias / entradas vazias ----------

    [Fact]
    public void ControlVazio_SemResultados()
    {
        var r = Comparar(new[] { "Ana" }, Array.Empty<string>());

        Assert.Empty(r.Remover);
        Assert.Empty(r.Similares);
    }

    [Fact]
    public void SistemaVazio_TodoControlVaiParaRemover()
    {
        var r = Comparar(Array.Empty<string>(), new[] { "Ana", "Bruno" });

        Assert.Equal(new[] { "Ana", "Bruno" }, r.Remover);
        Assert.Empty(r.Similares);
    }

    [Fact]
    public void AmbasVazias()
    {
        var r = Comparar(Array.Empty<string>(), Array.Empty<string>());

        Assert.Empty(r.Remover);
        Assert.Empty(r.Similares);
    }

    [Fact]
    public void EntradasEmBrancoOuNulas_SaoIgnoradas()
    {
        var r = Comparar(new[] { "  ", "Ana Lima", null! }, new[] { "", "\t", null!, "Ana Lima" });

        Assert.Empty(r.Remover);
        Assert.Empty(r.Similares);
    }

    [Fact]
    public void NomeSoComEspacosNoSistema_NaoCasaComNada()
    {
        var r = Comparar(new[] { "   " }, new[] { "Ana" });

        Assert.Equal(new[] { "Ana" }, r.Remover);
    }

    // ---------- Distância limitada == Levenshtein completo (truncado em máximo + 1) ----------

    private static int LevenshteinIngenuo(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                                   d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    [Fact]
    public void DistanciaLimitada_BateComLevenshteinCompleto()
    {
        var rnd = new Random(7);
        string Aleatoria() => new string(Enumerable.Range(0, rnd.Next(0, 13)).Select(_ => "abc "[rnd.Next(4)]).ToArray());

        for (var i = 0; i < 4000; i++)
        {
            var a = Aleatoria();
            var b = Aleatoria();
            var max = rnd.Next(0, 8);
            var esperado = Math.Min(LevenshteinIngenuo(a, b), max + 1);

            Assert.True(esperado == NomeComparador.DistanciaLimitada(a, b, max),
                $"a='{a}' b='{b}' max={max}: esperado {esperado}, obtido {NomeComparador.DistanciaLimitada(a, b, max)}");
        }
    }

    // ---------- As podas (tamanho/histograma/banda) não mudam o resultado ----------

    private static string OrdenarTokens(string s) =>
        string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries).OrderBy(t => t, StringComparer.Ordinal));

    [Theory]
    [InlineData(0.85)]
    [InlineData(0.7)]
    [InlineData(0.5)]
    public void ResultadoIgualAoDeUmaBuscaExaustiva(double limiar)
    {
        // Alfabeto só de consoantes e tokens com 2+ letras: não ativa partículas nem iniciais,
        // então o gabarito é só "Levenshtein (texto ou tokens ordenados) + regra de nome curto".
        var rnd = new Random(123);
        string Nome() => string.Join(' ', Enumerable.Range(0, rnd.Next(1, 4)).Select(_ =>
            new string(Enumerable.Range(0, rnd.Next(2, 7)).Select(__ => "bcdfg"[rnd.Next(5)]).ToArray())));
        var sistema = Enumerable.Range(0, 150).Select(_ => Nome()).ToArray();
        var control = Enumerable.Range(0, 150).Select(_ => Nome()).ToArray();

        var r = new NomeComparador(limiar).Comparar(sistema, control);

        var exatos = sistema.Select(NomeNormalizer.Normalizar).ToHashSet();
        var formasSistema = sistema.Select(NomeNormalizer.NormalizarParaComparacao).Distinct().ToList();

        var esperadoRemover = new List<string>();
        var esperadoSimilar = new Dictionary<string, double>();
        foreach (var nome in control.DistinctBy(NomeNormalizer.Normalizar))
        {
            if (exatos.Contains(NomeNormalizer.Normalizar(nome))) continue;
            var forma = NomeNormalizer.NormalizarParaComparacao(nome);

            double? melhor = null;
            foreach (var s in formasSistema)
            {
                var maior = Math.Max(forma.Length, s.Length);
                var d = Math.Min(LevenshteinIngenuo(forma, s), LevenshteinIngenuo(OrdenarTokens(forma), OrdenarTokens(s)));
                var score = 1.0 - (double)d / maior;
                var letrasA = forma.Replace(" ", "").Length;
                var letrasB = s.Replace(" ", "").Length;
                var elegivel = score + 1e-9 >= limiar || (Math.Max(letrasA, letrasB) <= 5 && Math.Min(letrasA, letrasB) >= 3 && d <= 1);
                if (elegivel && (melhor is null || score > melhor)) melhor = score;
            }

            if (melhor is not null) esperadoSimilar[nome] = melhor.Value;
            else esperadoRemover.Add(nome);
        }

        Assert.Equal(esperadoRemover, r.Remover);
        Assert.Equal(esperadoSimilar.Keys, r.Similares.Select(p => p.NomeControl));
        foreach (var par in r.Similares)
            Assert.Equal(esperadoSimilar[par.NomeControl], par.Score, 9);
    }

    // ---------- Escala (categoria Desempenho: fora do CI) ----------

    private static readonly string[] PrimeirosNomes =
    {
        "Ana","Maria","João","José","Pedro","Paulo","Carlos","Lucas","Marcos","Mariana","Fernanda","Juliana","Rafael",
        "Gabriel","Bruno","Camila","Patricia","Roberto","Ricardo","Eduardo","Felipe","Rodrigo","Luciana","Aline",
        "Amanda","Beatriz","Larissa","Thiago","Diego","Vinicius","Leandro","Sandra","Cristina","Renata","Tatiane",
        "Daniel","Fabio","Sergio","Marcelo","Claudia","Adriana","Vanessa","Gustavo","Henrique","Leonardo","Matheus",
        "Natalia","Priscila","Raquel","Simone"
    };

    private static readonly string[] Sobrenomes =
    {
        "Silva","Souza","Santos","Oliveira","Pereira","Lima","Costa","Ferreira","Rodrigues","Almeida","Nascimento",
        "Carvalho","Araujo","Ribeiro","Gomes","Martins","Rocha","Barbosa","Moreira","Cardoso","Teixeira","Correia",
        "Mendes","Nunes","Freitas","Pinto","Moura","Cavalcanti","Dias","Castro","Campos","Andrade","Monteiro",
        "Fernandes","Machado","Lopes","Vieira","Reis","Duarte","Azevedo"
    };

    /// <summary>Nomes parecidos com os reais: 1–2 nomes, 1–2 sobrenomes, às vezes uma partícula.</summary>
    private static string[] NomesRealistas(int quantidade, int semente)
    {
        var rnd = new Random(semente);
        string Um(string[] fonte) => fonte[rnd.Next(fonte.Length)];
        string Nome()
        {
            var partes = new List<string> { Um(PrimeirosNomes) };
            if (rnd.Next(3) == 0) partes.Add(Um(PrimeirosNomes));
            if (rnd.Next(4) == 0) partes.Add("da");
            partes.Add(Um(Sobrenomes));
            if (rnd.Next(2) == 0) partes.Add(Um(Sobrenomes));
            return string.Join(' ', partes);
        }
        return Enumerable.Range(0, quantidade).Select(_ => Nome()).ToArray();
    }

    [Fact]
    [Trait("Categoria", "Desempenho")]
    public void ListasGrandes_TerminamEmTempoRazoavel()
    {
        var rnd = new Random(42);
        string Nome() => string.Join(' ', Enumerable.Range(0, 3).Select(_ =>
            new string(Enumerable.Range(0, rnd.Next(4, 9)).Select(__ => (char)('a' + rnd.Next(26))).ToArray())));
        var sistema = Enumerable.Range(0, 5000).Select(_ => Nome()).ToArray();
        var control = Enumerable.Range(0, 5000).Select(_ => Nome()).ToArray();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = Comparar(sistema, control);
        sw.Stop();
        _saida.WriteLine($"5000x5000 aleatório, limiar 0,85: {sw.ElapsedMilliseconds} ms");

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"demorou {sw.Elapsed}");
        Assert.NotEmpty(r.Remover);
    }

    [Fact]
    [Trait("Categoria", "Desempenho")]
    public void ListasGrandes_NomesRealistas_TerminamEmTempoRazoavel()
    {
        var sistema = NomesRealistas(5000, 1);
        var control = NomesRealistas(5000, 2);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = Comparar(sistema, control);
        sw.Stop();
        _saida.WriteLine($"5000x5000 realista, limiar 0,85: {sw.ElapsedMilliseconds} ms " +
                         $"(remover={r.Remover.Count}, similares={r.Similares.Count})");

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"demorou {sw.Elapsed}");
    }
}
