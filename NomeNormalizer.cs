using System.Globalization;
using System.Text;

/// <summary>
/// Normalização de nomes (funções puras).
/// <see cref="Normalizar"/>: forma "estrita" — minúsculas, sem acentos, espaços colapsados. Define quando dois nomes são IGUAIS.
/// <see cref="NormalizarParaComparacao"/>: forma usada para medir similaridade — também ignora partículas
/// ("de", "da", "do", "das", "dos", "e") e trata pontuação/hífen como separador.
/// </summary>
public static class NomeNormalizer
{
    /// <summary>Partículas de nomes brasileiros ignoradas ao medir similaridade.</summary>
    public static readonly IReadOnlySet<string> Particulas =
        new HashSet<string> { "de", "da", "do", "das", "dos", "e" };

    public static string Normalizar(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "";

        var decomposto = nome.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        var espacoPendente = false;

        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue; // descarta o acento (marca combinante)

            if (char.IsWhiteSpace(c))
            {
                espacoPendente = sb.Length > 0; // espaços no início são ignorados
                continue;
            }

            if (espacoPendente)
            {
                sb.Append(' ');
                espacoPendente = false;
            }
            sb.Append(c);
        }

        // espaços no fim nunca chegam a ser escritos (ficam pendentes)
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Forma usada para comparar por similaridade: <see cref="Normalizar"/>, pontuação e hífen viram espaço
    /// ("Maria-José" = "Maria José", "Jr." = "Jr") e as partículas são descartadas ("João da Silva" = "João Silva").
    /// Se não sobrar nenhuma palavra, devolve a normalização estrita (o nome nunca "some").
    /// </summary>
    public static string NormalizarParaComparacao(string nome)
    {
        var estrita = Normalizar(nome);
        if (estrita.Length == 0) return "";

        var tokens = Tokens(estrita);
        var semParticulas = tokens.Where(t => !Particulas.Contains(t)).ToList();
        if (semParticulas.Count == 0) semParticulas = tokens;
        if (semParticulas.Count == 0) return estrita;

        return string.Join(' ', semParticulas);
    }

    /// <summary>Quebra em palavras: tudo que não é letra ou dígito é separador.</summary>
    private static List<string> Tokens(string texto)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        foreach (var c in texto)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
                sb.Clear();
            }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }
}
