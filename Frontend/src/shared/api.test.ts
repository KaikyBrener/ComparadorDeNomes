import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, comparar, limpar } from './api';

const json = (corpo: unknown, status: number) =>
  new Response(JSON.stringify(corpo), { status, headers: { 'Content-Type': 'application/json' } });

const simular = (resposta: Response | Error) => {
  const fetchFalso = vi.fn(async () => {
    if (resposta instanceof Error) throw resposta;
    return resposta;
  });
  vi.stubGlobal('fetch', fetchFalso);
  return fetchFalso;
};

afterEach(() => vi.unstubAllGlobals());

const falhaCom = async (acao: () => Promise<unknown>) => {
  try {
    await acao();
  } catch (e) {
    return e as ApiError;
  }
  throw new Error('era esperado um erro');
};

describe('requisições', () => {
  it('comparar envia POST com JSON e devolve a resposta tipada', async () => {
    const corpo = {
      remover: ['Carlos'],
      similares: [{ control: 'Silva João', sistema: 'João Silva', score: 100 }],
      registros: [{ nome: 'Carlos', acao: 'Remover', score: 0 }],
    };
    const fetchFalso = simular(json(corpo, 200));

    const r = await comparar('lista sistema', 'lista control');

    expect(r).toEqual(corpo);
    expect(fetchFalso).toHaveBeenCalledOnce();
    const [url, init] = fetchFalso.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe('/comparar');
    expect(init.method).toBe('POST');
    expect(new Headers(init.headers).get('Content-Type')).toBe('application/json');
    expect(JSON.parse(init.body as string)).toEqual({
      sistema: 'lista sistema',
      control: 'lista control',
    });
  });

  it('limpar envia dados e tipo e devolve o texto limpo', async () => {
    const fetchFalso = simular(json({ limpo: 'Ana\nBruno' }, 200));

    expect(await limpar('dados', 'control')).toEqual({ limpo: 'Ana\nBruno' });
    const [url, init] = fetchFalso.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe('/limpar');
    expect(JSON.parse(init.body as string)).toEqual({ dados: 'dados', tipo: 'control' });
  });
});

describe('erros HTTP: mostra a mensagem do JSON quando existir', () => {
  it.each([
    [400, 'Informe as duas listas (Sistema e Control ID).'],
    [413, 'O conteúdo enviado excede o limite de 2 MB.'],
    [415, 'Formato não suportado. Envie os dados em JSON.'],
    [500, 'Erro interno ao processar a requisição.'],
  ])('HTTP %i usa o campo "error" da resposta', async (status, mensagem) => {
    simular(json({ error: mensagem }, status));

    const erro = await falhaCom(() => comparar('a', 'b'));

    expect(erro).toBeInstanceOf(ApiError);
    expect(erro.message).toBe(mensagem);
    expect(erro.status).toBe(status);
  });

  it.each([
    [400, /inválida/i],
    [413, /2 MB/],
    [415, /JSON/],
    [500, /erro interno/i],
    [502, /HTTP 502/],
  ])('HTTP %i sem corpo JSON usa uma mensagem padrão', async (status, esperado) => {
    simular(new Response('<html>Bad Gateway</html>', { status }));

    const erro = await falhaCom(() => comparar('a', 'b'));

    expect(erro.message).toMatch(esperado);
    expect(erro.status).toBe(status);
  });

  it('corpo de erro vazio (413/415 do Kestrel) também usa a mensagem padrão', async () => {
    simular(new Response(null, { status: 413 }));

    expect((await falhaCom(() => comparar('a', 'b'))).message).toMatch(/2 MB/);
  });

  it('JSON de erro sem o campo "error" usa a mensagem padrão', async () => {
    simular(json({ title: 'Bad Request', status: 400 }, 400));

    expect((await falhaCom(() => limpar('x', 'control'))).message).toMatch(/inválida/i);
  });

  it('campo "error" vazio ou não textual é ignorado', async () => {
    simular(json({ error: '   ' }, 500));
    expect((await falhaCom(() => comparar('a', 'b'))).message).toMatch(/erro interno/i);

    simular(json({ error: { detalhe: 'x' } }, 500));
    expect((await falhaCom(() => comparar('a', 'b'))).message).toMatch(/erro interno/i);
  });

  it('falha de rede vira ApiError com status 0', async () => {
    simular(new TypeError('Failed to fetch'));

    const erro = await falhaCom(() => comparar('a', 'b'));

    expect(erro).toBeInstanceOf(ApiError);
    expect(erro.status).toBe(0);
    expect(erro.message).toMatch(/conectar ao servidor/i);
  });

  it('resposta 200 com formato inesperado é rejeitada', async () => {
    simular(json({ remover: 'não é lista' }, 200));

    const erro = await falhaCom(() => comparar('a', 'b'));

    expect(erro).toBeInstanceOf(ApiError);
    expect(erro.message).toMatch(/inesperada/i);
  });
});
