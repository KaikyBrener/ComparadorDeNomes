/// <summary>Corpo de POST /comparar.</summary>
public record ComparacaoRequest(string? Sistema, string? Control);

/// <summary>Corpo de POST /limpar.</summary>
public record LimpezaRequest(string? Dados, string? Tipo);

/// <summary>Par parecido devolvido por /comparar: nome do Control, nome do sistema e score em % (0 a 100).</summary>
public record SimilarDto(string Control, string Sistema, int Score);

/// <summary>Uma linha do histórico, devolvida ao navegador (que a guarda em localStorage).</summary>
public record RegistroComparacao(string Nome, string Acao, int Score);
