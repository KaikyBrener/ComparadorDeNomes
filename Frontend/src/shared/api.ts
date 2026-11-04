import type { ComparacaoResponse, LimpezaResponse, TipoFonte } from './tipos';

/** Erro de chamada à API. status 0 = sem resposta (rede/servidor fora do ar). */
export class ApiError extends Error {
  readonly status: number;

  constructor(mensagem: string, status: number) {
    super(mensagem);
    this.name = 'ApiError';
    this.status = status;
  }
}

/** Mensagens usadas quando o servidor não manda um JSON { error } (ex.: 413/415 do Kestrel têm corpo vazio). */
const MENSAGENS_PADRAO: Record<number, string> = {
  400: 'Requisição inválida. Verifique os dados enviados.',
  413: 'O conteúdo enviado excede o limite de 2 MB.',
  415: 'Formato não suportado. Envie os dados em JSON.',
  500: 'Erro interno ao processar a requisição.',
};

async function mensagemDoErro(resposta: Response): Promise<string> {
  try {
    const corpo: unknown = await resposta.json();
    if (typeof corpo === 'object' && corpo !== null && 'error' in corpo) {
      const { error } = corpo;
      if (typeof error === 'string' && error.trim() !== '') return error;
    }
  } catch {
    // corpo vazio ou que não é JSON
  }
  return MENSAGENS_PADRAO[resposta.status] ?? `Falha na requisição (HTTP ${resposta.status}).`;
}

async function postar(caminho: string, corpo: unknown): Promise<unknown> {
  let resposta: Response;
  try {
    resposta = await fetch(caminho, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(corpo),
    });
  } catch {
    throw new ApiError('Não foi possível conectar ao servidor.', 0);
  }

  if (!resposta.ok) throw new ApiError(await mensagemDoErro(resposta), resposta.status);

  try {
    return await resposta.json();
  } catch {
    throw new ApiError('Resposta inesperada do servidor.', resposta.status);
  }
}

const ehListaDeTextos = (v: unknown): v is string[] =>
  Array.isArray(v) && v.every((x) => typeof x === 'string');

const ehObjeto = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null;

function ehComparacao(v: unknown): v is ComparacaoResponse {
  return (
    ehObjeto(v) &&
    ehListaDeTextos(v.remover) &&
    Array.isArray(v.similares) &&
    v.similares.every(
      (s) =>
        ehObjeto(s) &&
        typeof s.control === 'string' &&
        typeof s.sistema === 'string' &&
        typeof s.score === 'number',
    ) &&
    Array.isArray(v.registros) &&
    v.registros.every(
      (r) =>
        ehObjeto(r) &&
        typeof r.nome === 'string' &&
        (r.acao === 'Remover' || r.acao === 'Similar') &&
        typeof r.score === 'number',
    )
  );
}

/** POST /comparar */
export async function comparar(sistema: string, control: string): Promise<ComparacaoResponse> {
  const resposta = await postar('/comparar', { sistema, control });
  if (!ehComparacao(resposta)) throw new ApiError('Resposta inesperada do servidor.', 200);
  return resposta;
}

/** POST /limpar */
export async function limpar(dados: string, tipo: TipoFonte): Promise<LimpezaResponse> {
  const resposta = await postar('/limpar', { dados, tipo });
  if (!ehObjeto(resposta) || typeof resposta.limpo !== 'string') {
    throw new ApiError('Resposta inesperada do servidor.', 200);
  }
  return { limpo: resposta.limpo };
}
