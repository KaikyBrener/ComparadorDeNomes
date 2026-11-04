import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  LIMITE_HISTORICO,
  limparHistorico,
  lerHistorico,
  registrarNoHistorico,
  textoDoHistorico,
} from './historico';
import type { Registro } from './tipos';

const registro = (nome: string, acao: Registro['acao'] = 'Remover', score = 0): Registro => ({
  nome,
  acao,
  score,
});

beforeEach(() => localStorage.clear());
afterEach(() => vi.restoreAllMocks());

describe('histórico', () => {
  it('guarda os registros e devolve os mais recentes primeiro', () => {
    registrarNoHistorico([registro('Ana')], new Date('2025-01-01T10:00:00Z'));
    registrarNoHistorico([registro('Bruno', 'Similar', 92)], new Date('2025-01-01T11:00:00Z'));

    const historico = lerHistorico();

    expect(historico.map((h) => h.nome)).toEqual(['Bruno', 'Ana']);
    expect(historico[0]).toEqual({
      nome: 'Bruno',
      acao: 'Similar',
      score: 92,
      timestamp: '2025-01-01T11:00:00.000Z',
    });
  });

  it('o teto é de 1000 entradas: descarta as mais antigas', () => {
    expect(LIMITE_HISTORICO).toBe(1000);
    const muitos = Array.from({ length: 1500 }, (_, i) => registro(`n${i}`));

    registrarNoHistorico(muitos);
    expect(lerHistorico()).toHaveLength(1000);
    expect(lerHistorico()[0]?.nome).toBe('n1499'); // o mais recente
    expect(lerHistorico().at(-1)?.nome).toBe('n500'); // o mais antigo que sobrou

    registrarNoHistorico([registro('extra')]);
    const depois = lerHistorico();
    expect(depois).toHaveLength(1000);
    expect(depois[0]?.nome).toBe('extra');
    expect(depois.at(-1)?.nome).toBe('n501');
  });

  it('registrar uma lista vazia não altera nada', () => {
    registrarNoHistorico([]);
    expect(localStorage.length).toBe(0);
  });

  it('limpar apaga tudo', () => {
    registrarNoHistorico([registro('Ana')]);
    limparHistorico();
    expect(lerHistorico()).toEqual([]);
  });

  it('JSON corrompido no localStorage vira histórico vazio', () => {
    localStorage.setItem('comparador.historico.v1', '{lixo');
    expect(lerHistorico()).toEqual([]);
  });

  it('valor que não é lista vira histórico vazio', () => {
    localStorage.setItem('comparador.historico.v1', '{"nome":"x"}');
    expect(lerHistorico()).toEqual([]);
  });

  it('ignora entradas com formato inválido, mantendo as válidas', () => {
    localStorage.setItem(
      'comparador.historico.v1',
      JSON.stringify([
        { nome: 'Ok', acao: 'Remover', score: 0, timestamp: '2025-01-01T10:00:00.000Z' },
        { nome: 123, acao: 'Remover', score: 0, timestamp: 'x' },
        null,
        'texto',
        { nome: 'Sem acao', score: 1, timestamp: 'x' },
      ]),
    );

    expect(lerHistorico().map((h) => h.nome)).toEqual(['Ok']);
  });

  it('guarda nomes com HTML como texto puro (a renderização é por textContent)', () => {
    registrarNoHistorico([registro('<img src=x onerror=alert(1)>')]);
    expect(lerHistorico()[0]?.nome).toBe('<img src=x onerror=alert(1)>');
  });

  it('o download tem cabeçalho, uma linha por entrada e score em %', () => {
    registrarNoHistorico([registro('Ana', 'Similar', 92)], new Date('2025-01-01T10:00:00Z'));

    const linhas = textoDoHistorico().split('\n');

    expect(linhas[0]).toBe('HISTORICO DE COMPARACOES');
    expect(linhas[1]).toBe('========================');
    expect(linhas.at(-1)).toMatch(/^Ana \| Similar \| 92% \| /);
  });
});

describe('localStorage indisponível', () => {
  const erro = () => {
    throw new DOMException('bloqueado', 'SecurityError');
  };

  it('getItem/setItem lançando não quebram nada', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(erro);
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(erro);
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(erro);

    expect(() => registrarNoHistorico([registro('Ana')])).not.toThrow();
    expect(lerHistorico()).toEqual([]);
    expect(() => limparHistorico()).not.toThrow();
    expect(textoDoHistorico()).toContain('HISTORICO DE COMPARACOES');
  });

  it('até o acesso a window.localStorage lançando (cookies bloqueados) é tolerado', () => {
    vi.spyOn(window, 'localStorage', 'get').mockImplementation(erro);

    expect(() => registrarNoHistorico([registro('Ana')])).not.toThrow();
    expect(lerHistorico()).toEqual([]);
    expect(() => limparHistorico()).not.toThrow();
  });

  it('cota cheia (QuotaExceededError) ao gravar é tolerada', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('cheio', 'QuotaExceededError');
    });

    expect(() => registrarNoHistorico([registro('Ana')])).not.toThrow();
  });
});
