import type { Similar } from './tipos';

export type Direcao = 'nenhuma' | 'asc' | 'desc';

/** Ordem do clique no cabeçalho "Score": primeiro maiores, depois menores, alternando. */
export function proximaDirecao(atual: Direcao): Direcao {
  return atual === 'desc' ? 'asc' : 'desc';
}

/** Nova lista ordenada por score (estável nos empates); 'nenhuma' mantém a ordem original. */
export function ordenarPorScore(lista: readonly Similar[], direcao: Direcao): Similar[] {
  const copia = [...lista];
  if (direcao === 'asc') return copia.sort((a, b) => a.score - b.score);
  if (direcao === 'desc') return copia.sort((a, b) => b.score - a.score);
  return copia;
}
