/**
 * Criação de elementos SEM innerHTML. Todo texto entra como nó de texto (equivale a textContent),
 * então nomes vindos de listas, arquivos ou do localStorage nunca são interpretados como HTML.
 * O ESLint proíbe innerHTML/outerHTML/insertAdjacentHTML no projeto.
 */

export type Filho = Node | string | null | undefined | false;

export interface OpcoesEl {
  classe?: string;
  atributos?: Record<string, string>;
}

// Manipuladores inline (onclick...) e style inline são bloqueados pela CSP e abrem brecha para XSS
const ATRIBUTO_PROIBIDO = /^(on|style$)/i;

export function el<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  opcoes: OpcoesEl = {},
  ...filhos: Filho[]
): HTMLElementTagNameMap[K] {
  const elemento = document.createElement(tag);
  if (opcoes.classe) elemento.className = opcoes.classe;

  for (const [nome, valor] of Object.entries(opcoes.atributos ?? {})) {
    if (ATRIBUTO_PROIBIDO.test(nome)) throw new Error(`Atributo não permitido: ${nome}`);
    elemento.setAttribute(nome, valor);
  }

  for (const filho of filhos) {
    if (filho === null || filho === undefined || filho === false) continue;
    elemento.append(filho); // string => nó de texto
  }
  return elemento;
}

export function limparFilhos(no: ParentNode): void {
  no.replaceChildren();
}

/** getElementById tipado; falha com mensagem clara se o elemento não existir. */
export function porId<T extends HTMLElement = HTMLElement>(id: string): T {
  const elemento = document.getElementById(id);
  if (!elemento) throw new Error(`Elemento #${id} não encontrado na página.`);
  return elemento as T;
}

/** Baixa um texto como arquivo .txt gerado no próprio navegador. */
export function baixarTexto(nomeDoArquivo: string, conteudo: string): void {
  const url = URL.createObjectURL(new Blob([conteudo], { type: 'text/plain;charset=utf-8' }));
  const link = el('a', { atributos: { href: url, download: nomeDoArquivo, hidden: '' } });
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

/** Copia para a área de transferência; false se o navegador não permitir (ex.: sem HTTPS/permissão). */
export async function copiarParaAreaDeTransferencia(texto: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(texto);
    return true;
  } catch {
    return false;
  }
}
