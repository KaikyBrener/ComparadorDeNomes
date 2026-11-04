# Comparador de Nomes — Control ID

Aplicação web que compara a lista de nomes do **sistema interno** com a lista de nomes cadastrados em um **dispositivo Control ID** (também aceita listas exportadas de Hikvision e Intelbras na página de limpeza) e mostra quais nomes do dispositivo **não existem no sistema**, ou seja, quais cadastros devem ser removidos.

Quem usa: quem administra o controle de acesso (portaria, condomínio, empresa) e precisa manter o dispositivo alinhado ao cadastro oficial sem conferir centenas de nomes um a um.

> **Atenção: itens "Similares" NÃO entram na lista de remoção.**
> Quando um nome do dispositivo é parecido com um nome do sistema (por exemplo "Bruno Costta" e "Bruno Costa"), ele aparece na aba **Similaridades**, com o percentual de semelhança, e **precisa de revisão manual**. Só os nomes da aba **Para Remover** não têm nenhum correspondente aceitável no sistema. Mesmo assim, confira a lista antes de apagar qualquer cadastro: remover um nome por engano revoga o acesso de uma pessoa real.

## Como funciona

1. Você cola (ou envia em arquivo `.txt`) as duas listas e clica em **Comparar**.
2. O navegador envia as listas para a API (`POST /comparar`), que devolve os nomes para remover e os similares.
3. Os resultados aparecem em duas abas, com botões para copiar e baixar a lista de remoção.

As páginas são:

- **Comparador** (`/`): a comparação em si.
- **Limpeza** (`/limpeza.html`): extrai só os nomes de listas copiadas de telas de sistemas (tabelas com `|`, colunas separadas por tab ou espaços, com ou sem cabeçalho), conforme o formato de cada fonte (sistema, Control ID, Hikvision, Intelbras).
- **Histórico** (`/historico.html`): registros das comparações anteriores, guardados no navegador.

### Regras da comparação

Para cada nome da lista do Control ID, nesta ordem:

1. **Igual após normalização** → está tudo certo; o nome não aparece em lugar nenhum. A normalização ignora maiúsculas/minúsculas, acentos e espaços repetidos ou nas pontas ("JOSÉ  souza" = "jose souza").
2. **Parecido com algum nome do sistema** → vai para **Similaridades**, com o score do melhor candidato (em caso de empate, vale o primeiro da lista do sistema).
3. **Caso contrário** → vai para **Para Remover**.

Um nome é considerado "parecido" se atender a pelo menos uma destas regras:

- **Similaridade de texto ≥ 0,85** (limiar padrão). Mede-se a distância de edição (Levenshtein) dividida pelo tamanho do maior nome, sobre uma forma simplificada do nome:
  - **partículas** `de`, `da`, `do`, `das`, `dos` e `e` são ignoradas ("João da Silva" ≈ "João Silva");
  - pontuação e hífen contam como espaço ("Maria-José" ≈ "Maria José", "Jr." ≈ "Jr");
  - a ordem das palavras também é testada em ordem alfabética, então "Silva João" ≈ "João Silva".
- **Iniciais:** mesmo número de palavras, e cada palavra é igual ou é só a inicial da outra ("João P. Silva" ≈ "João Pedro Silva"). O score é fixo em 95%, nunca 100%, porque não é o mesmo nome escrito.
- **Nomes curtos:** nomes de 3 a 5 letras aceitam uma edição, mesmo abaixo do limiar ("Ana" ≈ "Anna"). Com menos de 3 letras não há aproximação: "Li" e "Lu" são nomes diferentes.

O score exibido é um percentual inteiro, **truncado** (92,9% aparece como 92%), de modo que 100% só aparece quando a semelhança é total. O limiar pode ser mudado pela configuração `Comparacao:Limiar` (valor de 0 a 1; por variável de ambiente: `Comparacao__Limiar=0.9`).

## Privacidade

- **O histórico fica só no navegador** (`localStorage`), nas últimas 1000 entradas. Ele não é enviado a nenhum servidor e pode ser apagado na página Histórico. Em cada navegador/computador o histórico é separado.
- **O servidor não guarda nomes**: as listas são processadas na memória durante a requisição e descartadas. Não há banco de dados nem arquivo de log com nomes.
- **Sem CDN:** bibliotecas, fontes e ícones são empacotados no próprio build. A página não faz requisições a domínios externos, e a política de segurança (`Content-Security-Policy`) bloqueia qualquer carregamento externo.

## Como rodar

### Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22](https://nodejs.org/) (só para compilar o front-end)

### Execução

```bash
git clone https://github.com/KaikyBrener/comparador-nomes-control-id.git
cd comparador-nomes-control-id

# 1) Compila o front-end para wwwroot/ (pasta que não vai para o git)
cd Frontend
npm ci && npm run build
cd ..

# 2) Sobe a API, que também serve o front compilado
dotnet run

# 3) Abra http://localhost:5000
```

Rode `npm ci && npm run build` **antes** do `dotnet run` e sempre que mudar algo em `Frontend/`. Sem o `wwwroot/` a API funciona, mas as páginas não existem.

### Desenvolvimento do front-end (`npm run dev` com proxy)

```bash
# terminal 1: API em http://localhost:5000
dotnet run --urls http://localhost:5000

# terminal 2: Vite com recarga automática (http://localhost:5173)
cd Frontend
npm run dev
```

O Vite repassa `/comparar`, `/limpar` e `/health` para a API em `localhost:5000`.

### Porta

Por padrão a aplicação escuta em `http://localhost:5000`. Se a variável de ambiente `PORT` existir, ela escuta em `0.0.0.0:PORT` (é o que o Docker e o Render usam). URLs definidas explicitamente (`--urls` ou `ASPNETCORE_URLS`) têm prioridade. Um `PORT` inválido (texto, 0, acima de 65535) faz a aplicação falhar ao iniciar, com uma mensagem clara.

### Executável para Windows

```bash
dotnet publish -c Release -r win-x64 --self-contained
# gerado em bin/Release/net10.0/win-x64/publish/ComparadorNomes.exe
```

Compile o front-end antes (`npm ci && npm run build`) para que o `wwwroot/` entre no pacote.

## Testes

Back-end (xUnit, na pasta `Tests/`; não há arquivo `.sln`, então informe o projeto):

```bash
# o mesmo que roda no CI (sem os testes de desempenho)
dotnet test Tests/ComparadorNomes.Tests --filter "Categoria!=Desempenho"

# só os testes de desempenho (comparação de 5000 x 5000 nomes; dependem da máquina)
dotnet test Tests/ComparadorNomes.Tests --filter "Categoria=Desempenho" --logger "console;verbosity=detailed"
```

Front-end (Vitest, ESLint, Prettier e TypeScript), dentro de `Frontend/`:

```bash
npm run lint    # ESLint + checagem do Prettier
npm test        # Vitest
npm run build   # checagem de tipos (tsc) + build de produção
```

A verificação feita no navegador (CSP, XSS, layout em 360 px) está descrita em [docs/verificacao-manual.md](docs/verificacao-manual.md).

## Docker e deploy no Render

A imagem tem três estágios: compila o front-end (Node 22), publica o back-end (.NET 10) e gera a imagem final (`aspnet:10.0`, executando como usuário sem privilégios).

```bash
docker build -t comparador .
docker run -d -e PORT=8080 -p 8080:8080 comparador
# http://localhost:8080
```

No **Render** (Web Service com Docker):

- o Render define a variável `PORT` sozinho; a aplicação a lê, sem configuração extra;
- configure o **Health Check Path** como `/health` (responde `{"status":"ok"}`);
- na **instância gratuita**, o serviço "dorme" após alguns minutos sem acesso, e a primeira requisição depois disso demora cerca de um minuto para acordar a aplicação.

## CI

O workflow [.github/workflows/ci.yml](.github/workflows/ci.yml) roda em push na `main` e em pull requests: `backend` (build + testes), `frontend` (lint, testes, build) e, se os dois passarem, `docker` (constrói a imagem, sobe o contêiner e executa [um teste de fumaça](.github/scripts/smoke-test.sh) em `/health`, `/`, cabeçalhos de segurança e `/comparar`). O Dependabot atualiza semanalmente NuGet, npm, GitHub Actions e a imagem base do Docker.

## Limitações conhecidas

- **A similaridade é textual, não semântica.** Apelidos ("Zé" para "José"), nomes de casada, abreviações além das iniciais e erros de digitação grandes não são reconhecidos como o mesmo nome; esses casos tendem a cair em **Para Remover**. Por isso a lista final precisa de conferência humana.
- **Nomes parecidos de pessoas diferentes** (por exemplo "Mariana Silva" e "Mariano Silva") podem aparecer em Similaridades. Elas não são removidas automaticamente, mas exigem decisão manual.
- **Tamanho máximo da requisição: 2 MB** (as duas listas somadas). Acima disso a API responde 413.
- **Arquivos `.txt`:** aceitos em UTF-8 e UTF-16 (com BOM); se não for UTF-8 válido, o arquivo é lido como Windows-1252. Outras codificações podem trazer caracteres trocados, e a página mostra qual codificação foi detectada.
- **O histórico é por navegador:** limpar os dados do site, usar janela anônima ou trocar de computador o faz desaparecer. Se o navegador bloquear o armazenamento local, a comparação funciona, mas o histórico não é gravado.
- **A página depende de JavaScript** (e de `localStorage` apenas para o histórico).
- **O arquivo gerado não é enviado ao dispositivo:** a aplicação só aponta o que remover; a remoção no Control ID é feita por você.
- **No Render gratuito**, a primeira requisição após inatividade é lenta (veja acima).
- **Sem autenticação:** quem acessa a URL usa a ferramenta. Em implantação pública, coloque-a atrás de algum controle de acesso (rede interna, proxy com senha etc.), já que as listas contêm dados de pessoas.
