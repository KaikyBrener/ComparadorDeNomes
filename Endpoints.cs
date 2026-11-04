/// <summary>Rotas da API: /health, /comparar e /limpar.</summary>
public static class Endpoints
{
    public static void MapearEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/comparar", (ComparacaoRequest? req, NomeComparador comparador) =>
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Sistema) || string.IsNullOrWhiteSpace(req.Control))
                return Results.BadRequest(new { error = "Informe as duas listas (Sistema e Control ID)." });

            var resultado = comparador.Comparar(
                LeitorDeLista.ExtrairNomes(req.Sistema),
                LeitorDeLista.ExtrairNomes(req.Control));

            var remover = resultado.Remover;
            var similares = resultado.Similares
                .Select(p => new SimilarDto(p.NomeControl, p.NomeSistema, PercentualDe(p.Score)))
                .ToList();

            // Registros para o histórico, que o navegador guarda em localStorage.
            // Em "Remover" o score é 0 (não houve candidato aceitável).
            var registros = similares
                .Select(s => new RegistroComparacao(s.Control, "Similar", s.Score))
                .Concat(remover.Select(n => new RegistroComparacao(n, "Remover", 0)))
                .ToList();

            return Results.Json(new { remover, similares, registros });
        });

        app.MapPost("/limpar", (LimpezaRequest? req) =>
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Dados))
                return Results.BadRequest(new { error = "Informe os dados a serem limpos." });

            // tipo nulo, vazio ou desconhecido => 400
            var tipo = (req.Tipo ?? "").Trim().ToLowerInvariant();
            if (!LeitorDeLista.TiposValidos.Contains(tipo))
                return Results.BadRequest(new { error = "Fonte de dados inválida." });

            return Results.Json(new { limpo = string.Join("\n", LeitorDeLista.Limpar(req.Dados, tipo)) });
        });
    }

    /// <summary>Score (0 a 1) em percentual inteiro, truncado: só é 100% quando a similaridade é de fato total.</summary>
    private static int PercentualDe(double score) => (int)Math.Floor(score * 100 + 1e-9);
}
