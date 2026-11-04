# Verificação manual no navegador

Roteiro para repetir a conferência que os testes automatizados (xUnit e Vitest) não cobrem: política de segurança (CSP), proteção contra XSS e layout em tela de celular (360 px). Faça sempre que mudar algo em `Frontend/`, nos cabeçalhos de segurança (`Middlewares.cs`) ou no Dockerfile.

Tempo estimado: 10 minutos à mão, ou 1 minuto com o script automatizado (seção final).

## 0. Preparação

```bash
cd Frontend && npm ci && npm run build && cd ..   # gera wwwroot/
dotnet run                                         # http://localhost:5000
```

Para validar o que vai para produção, use a imagem Docker no lugar do `dotnet run`:

```bash
docker build -t comparador . && docker run --rm -e PORT=8080 -p 8080:8080 comparador   # http://localhost:8080
```

Use uma janela do Chrome/Edge com as **Ferramentas do desenvolvedor** (F12) abertas. Marque **Preserve log** nas abas Console e Rede, e **Disable cache** na aba Rede, para não perder nada ao trocar de página.

> Não teste com o servidor de desenvolvimento do Vite (`npm run dev`): os cabeçalhos de segurança são enviados pelo ASP.NET Core, só o build servido por ele os tem.

## 1. CSP sem violações

1. Abra `http://localhost:5000/` com o Console aberto.
2. Percorra as três páginas (`/`, `/limpeza.html`, `/historico.html`) e use o menu.
3. Na aba **Rede**, recarregue cada página (Ctrl+F5) e clique no documento principal (o primeiro item). Em **Cabeçalhos de resposta**, confira:

   | Cabeçalho | Valor esperado |
   | --- | --- |
   | `Content-Security-Policy` | `default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'` |
   | `X-Content-Type-Options` | `nosniff` |
   | `Referrer-Policy` | `no-referrer` |

4. Faça uma comparação completa (passo 2 abaixo), abra a aba **Similaridades**, use **Copiar** e **Baixar .txt**, e abra/feche o menu (em tela estreita) e os alertas do SweetAlert2.

**Esperado:**

- O Console não mostra nenhuma linha vermelha ou amarela. Em particular, nada como `Refused to apply inline style`, `Refused to execute inline script` ou `Refused to load ... because it violates the following Content Security Policy directive`.
- A aba Rede lista só requisições para `localhost:5000` (ou a porta usada). Marque o filtro "Domínio" ou ordene por domínio: não pode haver nenhum outro (nada de `cdn.jsdelivr.net`, `fonts.googleapis.com` etc.). Requisições `data:` são permitidas (só imagens).

**Se falhar:** uma violação de CSP costuma significar um `style="..."`/`on*="..."` no HTML ou uma biblioteca que injeta `<style>`/`<script>`. Foi por isso que o SweetAlert2 é importado da build ESM sem CSS embutido (`sweetalert2/dist/sweetalert2.esm.js`) e o CSS entra pelo SCSS.

## 2. XSS neutralizado

Os nomes digitados ou lidos de arquivos nunca podem ser interpretados como HTML.

1. Em `/`, preencha:

   - **Lista do Sistema**
     ```
     Mariana Silva
     João Silva
     José Souza
     Ana Maria Souza
     ```
   - **Lista do Control ID**
     ```
     Mariana Silvia
     Silva João
     Carlos <img src=x onerror=window.__xss=1>
     jose  souza
     Ana M. Souza
     Pedro Alves
     ```

2. Clique em **Comparar**.

**Esperado:**

- O alerta "Comparação Concluída!" abre sem erro.
- Em **Para Remover** aparecem, como texto: `Carlos <img src=x onerror=window.__xss=1>` e `Pedro Alves`.
- Em **Similaridades**: `Mariana Silvia` x `Mariana Silva`, `Silva João` x `João Silva` e `Ana M. Souza` x `Ana Maria Souza` (este último com score 95%, regra das iniciais). `jose  souza` não aparece em lugar nenhum (igual a "José Souza" após normalização).
- No Console, digite `window.__xss`: o resultado deve ser `undefined`. Se for `1`, o código do nome foi executado: **falha grave**.
- Em **Elements**, procure por `<img src=x`: não deve existir nenhum elemento `img` na página (o texto aparece apenas dentro do `<textarea>` ou de células `<td>` como texto).
- Abra `/historico.html`: o mesmo nome aparece **como texto** na coluna Nome, sem imagem quebrada, e o Console continua limpo.

**Defesas em camadas** (qualquer uma sozinha já impediria a execução): a página nunca usa `innerHTML` (regra do ESLint e função `el()` em `dom.ts`, que recusa atributos `on*` e `style`), e a CSP bloqueia scripts inline e manipuladores `onerror`.

### Dados do histórico adulterados

1. No Console, em `/historico.html`:
   ```js
   localStorage.setItem('comparador.historico.v1', JSON.stringify([
     { nome: '<img src=x onerror=window.__xss=1>', acao: '<b>Remover</b>', score: 0, timestamp: '2025-01-01T10:00:00.000Z' },
     { nome: 'Normal', acao: 'Similar', score: 92, timestamp: '2025-01-01T10:00:01.000Z' }
   ]))
   ```
   Recarregue. **Esperado:** só a linha "Normal" aparece (a entrada com `acao` inválida é descartada) e `window.__xss` continua `undefined`.
2. Corrompa o JSON:
   ```js
   localStorage.setItem('comparador.historico.v1', '{lixo')
   ```
   Recarregue. **Esperado:** a página abre normalmente com o histórico vazio e sem erros no Console.
3. Limpe tudo pelo botão **Limpar histórico** da própria página.

## 3. Layout em 360 px

1. Nas Ferramentas do desenvolvedor, ative o modo dispositivo (Ctrl+Shift+M), escolha **Responsive** e defina a largura em **360** (altura 800).
2. Nas três páginas, confira:

   - **Sem rolagem horizontal** na página inteira. No Console: `document.documentElement.scrollWidth <= innerWidth` deve devolver `true`.
   - **Barra de navegação:** a marca ("Comparador de Nomes") cabe sem quebrar o botão do menu; o botão abre e fecha o menu com os três links.
   - **Comparador:** os dois campos de texto ficam um sobre o outro; as abas "Para Remover" e "Similaridades" aparecem completas (ícones escondidos nesta largura), com os contadores visíveis.
   - **Similaridades:** a tabela usa a largura toda; textos longos quebram em várias linhas (sem estourar a tela); o botão "Score (%)" ordena (1º clique: maior para menor; os seguintes alternam com menor para maior).
   - **Histórico:** a coluna "Nome" tem largura mínima legível; se a tabela for mais larga que a tela, a rolagem horizontal fica **dentro** da tabela, não da página.
   - **Alertas (SweetAlert2):** cabem na tela, e os botões ficam clicáveis.
3. Repita em 1280 px: o conteúdo fica centralizado e os dois campos ficam lado a lado.

## 4. Conferências extras rápidas

- **Arquivo `.txt` em ANSI:** salve um arquivo com "José" em Windows-1252 (Bloco de Notas → Salvar como → ANSI), envie no campo de arquivo e confira se aparece "Codificação: windows-1252 (o arquivo não era UTF-8 válido)" e se o nome sai com acento correto.
- **Armazenamento bloqueado:** nas configurações do site, bloqueie cookies/dados do site e recarregue. A comparação deve funcionar normalmente; só o histórico deixa de ser gravado, sem erros na página.
- **Erros da API:** com o back-end parado, clique em Comparar. Deve aparecer um alerta de erro em português ("Não foi possível conectar ao servidor."), sem detalhes técnicos.

## Script automatizado (opcional)

[verificar-navegador.mjs](verificar-navegador.mjs) repete os passos 1 a 3 com o Chrome em modo headless (via `puppeteer-core`) e sai com código 1 se algo falhar. Ele não é dependência do projeto, então rode fora do repositório:

```bash
mkdir /tmp/verificacao && cd /tmp/verificacao
npm init -y && npm i puppeteer-core
cp <caminho-do-repo>/docs/verificar-navegador.mjs .

# com a aplicação rodando em http://localhost:5000; capturas de tela opcionais no 2º argumento
CHROME_PATH="C:/Program Files/Google/Chrome/Application/chrome.exe" \
  node verificar-navegador.mjs http://localhost:5000 ./capturas
```

O script abre um perfil temporário do Chrome, então não mexe no histórico nem nos dados do seu navegador. Ele não substitui o passo 3 por completo (por exemplo, a aparência dos textos quebrados): olhe as capturas geradas.
