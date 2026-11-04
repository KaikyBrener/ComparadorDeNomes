/// <summary>Configuração de hospedagem: em qual porta o servidor escuta.</summary>
public static class Hospedagem
{
    /// <summary>Porta usada quando nada é configurado (padrão do Kestrel: http://localhost:5000).</summary>
    public const int PortaPadrao = 5000;

    /// <summary>
    /// Decide a URL de escuta a partir da variável PORT (definida por Docker/Render):
    /// <list type="bullet">
    /// <item>URLs explícitas (ASPNETCORE_URLS ou --urls) têm prioridade: devolve null e nada muda.</item>
    /// <item>PORT válida: escuta em todas as interfaces nessa porta (necessário dentro de contêiner).</item>
    /// <item>PORT ausente: devolve null e vale o padrão do Kestrel, http://localhost:5000.</item>
    /// <item>PORT inválida: falha na inicialização com mensagem clara, em vez de subir na porta errada.</item>
    /// </list>
    /// </summary>
    public static string? ResolverUrl(string? urlsConfiguradas, string? port)
    {
        if (!string.IsNullOrWhiteSpace(urlsConfiguradas)) return null;
        if (string.IsNullOrWhiteSpace(port)) return null;

        if (!int.TryParse(port.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var numero)
            || numero is < 1 or > 65535)
        {
            throw new InvalidOperationException($"A variável de ambiente PORT é inválida: '{port}'. Use um número de 1 a 65535.");
        }

        return $"http://0.0.0.0:{numero}";
    }

    public static void ConfigurarPorta(this WebApplicationBuilder builder)
    {
        var url = ResolverUrl(builder.Configuration["urls"], Environment.GetEnvironmentVariable("PORT"));
        if (url is not null) builder.WebHost.UseUrls(url);
    }
}
