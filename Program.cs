var builder = WebApplication.CreateBuilder(args);

// Porta: variável PORT (Docker/Render); sem ela, o padrão é http://localhost:5000
builder.ConfigurarPorta();

// Limita o tamanho do corpo das requisições (vale também para requisições chunked)
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = Middlewares.LimiteCorpoBytes);

// Requisições malformadas passam pelo handler de exceções (resposta JSON uniforme)
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

// Comparador de nomes; o limiar de similaridade vem de "Comparacao:Limiar" (padrão 0,85)
builder.Services.AddSingleton(sp =>
    new NomeComparador(sp.GetRequiredService<IConfiguration>().GetValue("Comparacao:Limiar", NomeComparador.LimiarPadrao)));

var app = builder.Build();

app.UsarCabecalhosDeSeguranca();
app.UsarTratamentoDeErros();
app.UsarLimiteDeCorpo();

// Front-end compilado (Frontend/ -> wwwroot/): index.html em "/" e as demais páginas pelo nome
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapearEndpoints();

app.Run();

// Necessário para os testes de integração (WebApplicationFactory<Program>)
public partial class Program { }
