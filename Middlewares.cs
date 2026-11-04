using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;

/// <summary>Middlewares transversais: cabeçalhos de segurança, tratamento de erros e limite do corpo.</summary>
public static class Middlewares
{
    public const int LimiteCorpoBytes = 2 * 1024 * 1024; // 2 MB

    /// <summary>
    /// Política restritiva: tudo só da própria origem. O front é compilado sem estilos/scripts inline
    /// (se algo quebrar a CSP, o ajuste é no build do front, nunca afrouxar com 'unsafe-inline').
    /// </summary>
    public const string PoliticaDeSeguranca =
        "default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self' data:; " +
        "connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";

    /// <summary>
    /// CSP, nosniff e Referrer-Policy em TODAS as respostas. Usa OnStarting porque o handler de exceções
    /// limpa os cabeçalhos antes de escrever a resposta de erro.
    /// </summary>
    public static IApplicationBuilder UsarCabecalhosDeSeguranca(this IApplicationBuilder app) =>
        app.Use(async (contexto, next) =>
        {
            contexto.Response.OnStarting(() =>
            {
                var cabecalhos = contexto.Response.Headers;
                cabecalhos.ContentSecurityPolicy = PoliticaDeSeguranca;
                cabecalhos.XContentTypeOptions = "nosniff";
                cabecalhos["Referrer-Policy"] = "no-referrer";
                return Task.CompletedTask;
            });
            await next();
        });

    /// <summary>
    /// Tratamento global de erros: o cliente só recebe mensagem genérica em { "error": "..." };
    /// o detalhe fica no log do servidor (o próprio middleware registra a exceção).
    /// </summary>
    public static IApplicationBuilder UsarTratamentoDeErros(this IApplicationBuilder app) =>
        app.UseExceptionHandler(erroApp => erroApp.Run(async contexto =>
        {
            var erro = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;

            var status = erro switch
            {
                BadHttpRequestException bad when bad.StatusCode is >= 400 and < 500 => bad.StatusCode,
                JsonException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };

            var mensagem = status switch
            {
                StatusCodes.Status413PayloadTooLarge => "O conteúdo enviado excede o limite de 2 MB.",
                StatusCodes.Status415UnsupportedMediaType => "Formato não suportado. Envie os dados em JSON.",
                >= 400 and < 500 => "Requisição inválida. Verifique os dados enviados.",
                _ => "Erro interno ao processar a requisição."
            };

            contexto.Response.StatusCode = status;
            await contexto.Response.WriteAsJsonAsync(new { error = mensagem });
        }));

    /// <summary>Rejeita cedo corpos que já declaram tamanho acima do limite (os demais caem no limite do Kestrel).</summary>
    public static IApplicationBuilder UsarLimiteDeCorpo(this IApplicationBuilder app) =>
        app.Use(async (contexto, next) =>
        {
            if (contexto.Request.ContentLength > LimiteCorpoBytes)
                throw new BadHttpRequestException("Corpo da requisição acima do limite.", StatusCodes.Status413PayloadTooLarge);
            await next();
        });
}
