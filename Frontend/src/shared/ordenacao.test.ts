import { describe, expect, it } from 'vitest';
import { ordenarPorScore, proximaDirecao } from './ordenacao';
import type { Similar } from './tipos';

const par = (control: string, score: number): Similar => ({ control, sistema: 's', score });

describe('ordenarPorScore', () => {
  const lista = [par('b', 80), par('a', 100), par('c', 92)];

  it('decrescente: maior score primeiro', () => {
    expect(ordenarPorScore(lista, 'desc').map((p) => p.score)).toEqual([100, 92, 80]);
  });

  it('crescente: menor score primeiro', () => {
    expect(ordenarPorScore(lista, 'asc').map((p) => p.score)).toEqual([80, 92, 100]);
  });

  it('sem direção mantém a ordem original', () => {
    expect(ordenarPorScore(lista, 'nenhuma')).toEqual(lista);
  });

  it('não altera a lista original e é estável nos empates', () => {
    const empate = [par('x', 90), par('y', 90), par('z', 90)];
    const copia = [...empate];

    expect(ordenarPorScore(empate, 'desc').map((p) => p.control)).toEqual(['x', 'y', 'z']);
    expect(empate).toEqual(copia);
  });
});

describe('proximaDirecao', () => {
  it('alterna: nenhuma -> desc -> asc -> desc', () => {
    expect(proximaDirecao('nenhuma')).toBe('desc');
    expect(proximaDirecao('desc')).toBe('asc');
    expect(proximaDirecao('asc')).toBe('desc');
  });
});
