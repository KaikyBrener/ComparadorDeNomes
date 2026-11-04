/// <summary>Par de nomes considerados parecidos: nome do Control, nome do sistema e score (0 a 1).</summary>
public record ParSimilar(string NomeControl, string NomeSistema, double Score);

/// <summary>Resultado: nomes do Control a remover e pares parecidos (para revisão manual).</summary>
public record ResultadoComparacao(IReadOnlyList<string> Remover, IReadOnlyList<ParSimilar> Similares);

/// <summary>
/// Compara a lista do Control com a do sistema.
/// <list type="bullet">
/// <item>Igual após normalização estrita (minúsculas, sem acento, espaços colapsados) => ok, não aparece em lugar nenhum.</item>
/// <item>Senão, procura o MELHOR candidato do sistema; se existir um "bom o bastante" => Similares (com o score real).</item>
/// <item>Caso contrário => Remover.</item>
/// </list>
/// "Bom o bastante" é uma destas regras:
/// <list type="number">
/// <item>Levenshtein normalizado >= limiar, medido sobre a forma de comparação (sem partículas "de/da/do/das/dos/e",
///   pontuação e hífen como espaço) e também sobre os tokens em ordem alfabética ("Silva João" x "João Silva").</item>
/// <item>Nome curto: de 3 a 5 letras e no máximo 1 edição ("Ana" x "Anna"), mesmo abaixo do limiar.</item>
/// <item>Iniciais: mesmo número de tokens e cada token igual ou inicial do outro ("João P. Silva" x "João Pedro Silva");
///   vale <see cref="ScoreIniciais"/> (nunca 100%).</item>
/// </list>
/// </summary>
public class NomeComparador
{
    public const double LimiarPadrao = 0.85;

    /// <summary>Score atribuído a um par que só difere por iniciais (não é o mesmo nome escrito, então não chega a 1).</summary>
    public const double ScoreIniciais = 0.95;

    /// <summary>Nomes com até esta quantidade de letras aceitam 1 edição como "similar".</summary>
    public const int LetrasNomeCurto = 5;

    /// <summary>...mas os dois nomes precisam ter pelo menos esta quantidade de letras ("Li" x "Lu" não conta).</summary>
    public const int MinimoLetrasNomeCurto = 3;

    private const double Epsilon = 1e-9;

    public double Limiar { get; }

    public NomeComparador(double limiar = LimiarPadrao)
    {
        if (double.IsNaN(limiar) || limiar < 0 || limiar > 1)
            throw new ArgumentOutOfRangeException(nameof(limiar), limiar, "O limiar deve estar entre 0 e 1.");
        Limiar = limiar;
    }

    private sealed record Candidato(
        string Original, string Comparavel, string Ordenado, ushort[] Histograma, int Letras, string[] Tokens);

    public ResultadoComparacao Comparar(IEnumerable<string> sistema, IEnumerable<string> control)
    {
        // Índices do sistema (sem duplicatas, mantendo a primeira grafia)
        var exatos = new HashSet<string>();
        var porTamanho = new SortedDictionary<int, List<Candidato>>();
        var porIniciais = new Dictionary<(int Tokens, char Primeira), List<Candidato>>();

        foreach (var nome in sistema ?? Enumerable.Empty<string>())
        {
            var estrito = NomeNormalizer.Normalizar(nome);
            if (estrito.Length == 0 || !exatos.Add(estrito)) continue;

            var comparavel = NomeNormalizer.NormalizarParaComparacao(nome);
            var tokens = comparavel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var candidato = new Candidato(nome.Trim(), comparavel, Ordenar(comparavel), Histograma(comparavel),
                                          Letras(comparavel), tokens);

            if (!porTamanho.TryGetValue(comparavel.Length, out var lista))
                porTamanho[comparavel.Length] = lista = new List<Candidato>();
            lista.Add(candidato);

            if (tokens.Length >= 2)
            {
                var chave = (tokens.Length, tokens[0][0]);
                if (!porIniciais.TryGetValue(chave, out var grupo))
                    porIniciais[chave] = grupo = new List<Candidato>();
                grupo.Add(candidato);
            }
        }

        var remover = new List<string>();
        var similares = new List<ParSimilar>();
        var jaVistos = new HashSet<string>();

        foreach (var nome in control ?? Enumerable.Empty<string>())
        {
            var estrito = NomeNormalizer.Normalizar(nome);
            if (estrito.Length == 0 || !jaVistos.Add(estrito)) continue;
            if (exatos.Contains(estrito)) continue; // igual após normalização

            var melhor = MelhorCandidato(NomeNormalizer.NormalizarParaComparacao(nome), porTamanho, porIniciais, out var score);
            if (melhor != null)
                similares.Add(new ParSimilar(nome.Trim(), melhor.Original, score));
            else
                remover.Add(nome.Trim());
        }

        return new ResultadoComparacao(remover, similares);
    }

    /// <summary>Busca o candidato elegível de MAIOR score (o primeiro em caso de empate).</summary>
    private Candidato? MelhorCandidato(
        string comparavel,
        SortedDictionary<int, List<Candidato>> porTamanho,
        Dictionary<(int Tokens, char Primeira), List<Candidato>> porIniciais,
        out double melhorScore)
    {
        var ordenado = Ordenar(comparavel);
        var histograma = Histograma(comparavel);
        var letras = Letras(comparavel);
        Candidato? melhor = null;
        melhorScore = -1;

        // 1) Levenshtein (texto direto ou tokens ordenados) com limiar, mais a regra de nome curto
        foreach (var (tamanho, candidatos) in porTamanho)
        {
            var maiorTamanho = Math.Max(comparavel.Length, tamanho);
            var limite = DistanciaMaximaPermitida(maiorTamanho);
            // nomes curtos (<= 5 letras) aceitam 1 edição; 9 caracteres cobre 5 letras separadas por espaços
            var limiteDaBusca = maiorTamanho <= 9 ? Math.Max(limite, 1) : limite;
            if (Math.Abs(comparavel.Length - tamanho) > limiteDaBusca) continue; // poda por tamanho

            foreach (var c in candidatos)
            {
                if (HistogramaExcede(histograma, c.Histograma, limiteDaBusca)) continue;

                var d = DistanciaLimitada(comparavel, c.Comparavel, limiteDaBusca);
                // só vale tentar a ordem alfabética dos tokens se ela muda alguma das duas strings
                if (d > 0 && (ordenado != comparavel || c.Ordenado != c.Comparavel))
                    d = Math.Min(d, DistanciaLimitada(ordenado, c.Ordenado, limiteDaBusca));

                var nomeCurto = Math.Max(letras, c.Letras) <= LetrasNomeCurto && Math.Min(letras, c.Letras) >= MinimoLetrasNomeCurto;
                var permitido = nomeCurto ? Math.Max(limite, 1) : limite;
                if (d > permitido) continue;

                var score = 1.0 - (double)d / maiorTamanho;
                if (score > melhorScore)
                {
                    melhor = c;
                    melhorScore = score;
                }
            }
        }

        // 2) Iniciais ("João P. Silva" x "João Pedro Silva")
        if (ScoreIniciais + Epsilon >= Limiar && ScoreIniciais > melhorScore)
        {
            var tokens = comparavel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2 && porIniciais.TryGetValue((tokens.Length, tokens[0][0]), out var grupo))
            {
                foreach (var c in grupo)
                {
                    if (!CompativelComIniciais(tokens, c.Tokens)) continue;
                    melhor = c;
                    melhorScore = ScoreIniciais;
                    break;
                }
            }
        }

        return melhor;
    }

    /// <summary>
    /// Mesmo número de tokens; cada par é igual ou um deles é uma letra só que inicia o outro,
    /// com pelo menos uma inicial (senão seria o mesmo nome, tratado pela regra 1).
    /// </summary>
    private static bool CompativelComIniciais(string[] a, string[] b)
    {
        if (a.Length != b.Length) return false;
        var usouInicial = false;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i]) continue;
            if (a[i].Length == 1 && b[i].StartsWith(a[i], StringComparison.Ordinal)) usouInicial = true;
            else if (b[i].Length == 1 && a[i].StartsWith(b[i], StringComparison.Ordinal)) usouInicial = true;
            else return false;
        }
        return usouInicial;
    }

    /// <summary>Maior número de edições que ainda mantém score >= limiar para strings de até <paramref name="tamanho"/>.</summary>
    private int DistanciaMaximaPermitida(int tamanho) => (int)Math.Floor((1.0 - Limiar) * tamanho + Epsilon);

    /// <summary>
    /// Similaridade (0 a 1) entre dois textos JÁ preparados (ver <see cref="NomeNormalizer"/>):
    /// o melhor entre o texto direto e os tokens em ordem alfabética.
    /// </summary>
    public static double Similaridade(string a, string b)
    {
        a ??= "";
        b ??= "";
        var maior = Math.Max(a.Length, b.Length);
        if (maior == 0) return 1.0;

        var d = DistanciaLimitada(a, b, maior);
        if (d > 0)
            d = Math.Min(d, DistanciaLimitada(Ordenar(a), Ordenar(b), maior));
        return 1.0 - (double)d / maior;
    }

    /// <summary>Tokens em ordem alfabética (ordinal), separados por um espaço. Mantém o tamanho da string.</summary>
    private static string Ordenar(string normalizado)
    {
        var tokens = normalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2) return normalizado;
        Array.Sort(tokens, StringComparer.Ordinal);
        return string.Join(' ', tokens);
    }

    private static int Letras(string texto) => texto.Count(c => c != ' ');

    private const int Baldes = 32;

    /// <summary>Contagem de caracteres em 32 baldes (c &amp; 31). Serve só para podar candidatos.</summary>
    private static ushort[] Histograma(string s)
    {
        var h = new ushort[Baldes];
        foreach (var c in s) h[c & (Baldes - 1)]++;
        return h;
    }

    /// <summary>
    /// Limite inferior da distância de Levenshtein: cada edição muda a contagem de caracteres em no máximo 2
    /// (soma das diferenças), então distância >= soma / 2. Se isso já passa do máximo, nem vale rodar o DP.
    /// </summary>
    private static bool HistogramaExcede(ushort[] a, ushort[] b, int distanciaMaxima)
    {
        var soma = 0;
        for (var i = 0; i < Baldes; i++) soma += Math.Abs(a[i] - b[i]);
        return soma > 2 * distanciaMaxima;
    }

    /// <summary>
    /// Distância de Levenshtein limitada: devolve a distância exata se for &lt;= <paramref name="maximo"/>,
    /// ou <c>maximo + 1</c> caso contrário (com saída antecipada).
    /// </summary>
    public static int DistanciaLimitada(string a, string b, int maximo)
    {
        if (maximo < 0) maximo = 0;
        var estouro = maximo + 1;
        if (a == b) return 0;
        if (Math.Abs(a.Length - b.Length) > maximo) return estouro;

        int n = a.Length, m = b.Length;
        if (n == 0) return Math.Min(m, estouro);
        if (m == 0) return Math.Min(n, estouro);

        var largura = m + 2;
        Span<int> buffer = largura * 2 <= 512 ? stackalloc int[largura * 2] : new int[largura * 2];
        var anterior = buffer.Slice(0, largura);
        var atual = buffer.Slice(largura, largura);
        for (var j = 0; j <= m; j++) anterior[j] = Math.Min(j, estouro);

        for (var i = 1; i <= n; i++)
        {
            var inicio = Math.Max(1, i - maximo);
            var fim = Math.Min(m, i + maximo);

            atual[inicio - 1] = inicio == 1 ? Math.Min(i, estouro) : estouro;
            if (fim < m) atual[fim + 1] = estouro;

            var minimoDaLinha = atual[inicio - 1];
            for (var j = inicio; j <= fim; j++)
            {
                var custo = a[i - 1] == b[j - 1] ? 0 : 1;
                var v = Math.Min(Math.Min(anterior[j] + 1, atual[j - 1] + 1), anterior[j - 1] + custo);
                if (v > estouro) v = estouro;
                atual[j] = v;
                if (v < minimoDaLinha) minimoDaLinha = v;
            }

            if (minimoDaLinha >= estouro) return estouro;
            var troca = anterior;
            anterior = atual;
            atual = troca;
        }

        return Math.Min(anterior[m], estouro);
    }
}
