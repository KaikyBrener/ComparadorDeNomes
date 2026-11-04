using System.Text.RegularExpressions;

/// <summary>
/// Leitura das listas coladas pelo usuário (única implementação, usada por /comparar e /limpar).
/// <para>
/// Cada linha vira uma lista de células: se tem <c>|</c> (inclusive tabela markdown com pipe nas pontas), divide por pipe;
/// senão divide por tab ou por 2+ espaços. Células vazias são MANTIDAS, para que as colunas não se desloquem.
/// Linhas em branco e separadores de tabela (<c>|---|:---:|</c>) são ignorados, e a primeira linha é descartada se for cabeçalho.
/// </para>
/// </summary>
public static class LeitorDeLista
{
    public static readonly IReadOnlySet<string> TiposValidos =
        new HashSet<string> { "sistema", "hikvision", "control", "intelbras" };

    /// <summary>Palavras que denunciam uma linha de cabeçalho (comparadas sem acento e sem diferenciar caixa).</summary>
    private static readonly HashSet<string> PalavrasDeCabecalho = new()
    {
        "nome", "name", "codigo", "person", "id", "tipo", "type", "estado", "departamento",
        "user", "usuario", "floor", "matricula", "apto"
    };

    private static readonly Regex SeparadorDeColunas = new(@"\t|\s{2,}", RegexOptions.Compiled);
    private static readonly Regex NaoLetraNemDigito = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);

    private enum Separador { Nenhum, Pipe, Tab, Espacos }

    private sealed record Linha(string[] Celulas, Separador Separador);

    // ---------- Para /comparar: descobre onde está o nome ----------

    /// <summary>
    /// Extrai os nomes de uma lista. Se o cabeçalho tiver uma coluna "Nome"/"Name", é ela que vale;
    /// senão usa a heurística: coluna 4, 3, 2 ou 1 (a primeira preenchida e não numérica).
    /// </summary>
    public static List<string> ExtrairNomes(string texto)
    {
        var linhas = Ler(texto);
        var nomes = new List<string>();

        int? colunaDoNome = null;
        var inicio = 0;
        if (linhas.Count > 0 && EhCabecalho(linhas[0]))
        {
            colunaDoNome = IndiceDaColunaNome(linhas[0].Celulas);
            inicio = 1;
        }

        for (var i = inicio; i < linhas.Count; i++)
        {
            var nome = colunaDoNome is int coluna ? Celula(linhas[i], coluna) : Heuristica(linhas[i]);
            if (!string.IsNullOrWhiteSpace(nome)) nomes.Add(nome);
        }
        return nomes;
    }

    private static string? Heuristica(Linha linha)
    {
        var c = linha.Celulas;

        // Colunas separadas só por espaços: não dá para saber onde uma coluna termina ("José  Silva" é um nome com
        // espaço duplo). Descarta o que é só número (códigos) e junta o resto, em vez de truncar o nome.
        if (linha.Separador == Separador.Espacos)
        {
            var semNumeros = c.Where(x => !EhNumero(x)).ToList();
            return string.Join(' ', semNumeros.Count > 0 ? semNumeros : c.ToList());
        }

        foreach (var indice in new[] { 3, 2, 1 })
        {
            var valor = Celula(linha, indice);
            if (valor != null && !EhNumero(valor)) return valor;
        }
        return Celula(linha, 0);
    }

    // ---------- Para /limpar: coluna fixa por fabricante ----------

    /// <summary>Limpa a lista conforme o formato de cada fonte; sem duplicatas (ignorando a caixa).</summary>
    public static List<string> Limpar(string texto, string tipo)
    {
        tipo = (tipo ?? "").Trim().ToLowerInvariant();
        if (!TiposValidos.Contains(tipo))
            throw new ArgumentException("Fonte de dados inválida.", nameof(tipo));

        var linhas = Ler(texto);
        var resultado = new List<string>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < linhas.Count; i++)
        {
            if (i == 0 && EhCabecalho(linhas[0])) continue;
            var linha = linhas[i];

            var processada = tipo switch
            {
                "sistema" => Celula(linha, 3),
                "control" => Celula(linha, 1),
                "intelbras" => Celula(linha, 0),
                "hikvision" => Hikvision(linha),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(processada) && vistos.Add(processada))
                resultado.Add(processada);
        }
        return resultado;
    }

    /// <summary>Hikvision: "ID | Employee | Nome" => "ID | Nome". A primeira coluna precisa ser numérica.</summary>
    private static string? Hikvision(Linha linha)
    {
        var id = Celula(linha, 0);
        var nome = Celula(linha, 2);
        return id != null && EhNumero(id) && nome != null ? id + " | " + nome : null;
    }

    // ---------- Parsing comum ----------

    private static List<Linha> Ler(string texto)
    {
        var linhas = new List<Linha>();
        foreach (var bruta in (texto ?? "").Split('\n'))
        {
            var linha = bruta.Trim();
            if (linha.Length == 0 || EhSeparadorDeTabela(linha)) continue;
            linhas.Add(Dividir(linha));
        }
        return linhas;
    }

    private static Linha Dividir(string linha)
    {
        if (linha.Contains('|'))
        {
            // tabela markdown: um pipe opcional no começo e no fim não cria colunas
            if (linha.StartsWith('|')) linha = linha[1..];
            if (linha.EndsWith('|')) linha = linha[..^1];
            return new Linha(linha.Split('|').Select(c => c.Trim()).ToArray(), Separador.Pipe);
        }

        var celulas = SeparadorDeColunas.Split(linha).Select(c => c.Trim()).ToArray();
        if (celulas.Length == 1) return new Linha(celulas, Separador.Nenhum);
        return new Linha(celulas, linha.Contains('\t') ? Separador.Tab : Separador.Espacos);
    }

    /// <summary>Linha só de pipes, hífens, dois-pontos, "=" e espaços (ex.: "|---|:---:|", "-----", "| | |").</summary>
    private static bool EhSeparadorDeTabela(string linha) => linha.All(c => "|-:=+ \t".Contains(c));

    private static bool EhCabecalho(Linha linha) =>
        linha.Celulas.Any(c => Palavras(c).Any(PalavrasDeCabecalho.Contains));

    private static int? IndiceDaColunaNome(string[] cabecalho)
    {
        for (var i = 0; i < cabecalho.Length; i++)
        {
            var primeira = Palavras(cabecalho[i]).FirstOrDefault();
            if (primeira is "nome" or "name") return i;
        }
        return null;
    }

    private static IEnumerable<string> Palavras(string celula) =>
        NaoLetraNemDigito.Split(NomeNormalizer.Normalizar(celula)).Where(p => p.Length > 0);

    /// <summary>Célula na posição indicada, ou null se não existir ou estiver vazia.</summary>
    private static string? Celula(Linha linha, int indice) =>
        indice < linha.Celulas.Length && !string.IsNullOrWhiteSpace(linha.Celulas[indice]) ? linha.Celulas[indice] : null;

    private static bool EhNumero(string valor) => valor.Length > 0 && valor.All(char.IsAsciiDigit);
}
