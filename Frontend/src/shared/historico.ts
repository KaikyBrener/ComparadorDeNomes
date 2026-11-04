import type { EntradaHistorico, Registro } from './tipos';

/** Histórico guardado só no navegador (localStorage). Mesma chave da versão anterior da página. */
const CHAVE = 'comparador.historico.v1';
export const LIMITE_HISTORICO = 1000;

/** O acesso a window.localStorage pode lançar (cookies bloqueados, modo privado, iframe sandbox). */
function armazenamento(): Storage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

function ehEntrada(valor: unknown): valor is EntradaHistorico {
  if (typeof valor !== 'object' || valor === null) return false;
  const v = valor as Record<string, unknown>;
  return (
    typeof v.nome === 'string' &&
    (v.acao === 'Remover' || v.acao === 'Similar') &&
    typeof v.score === 'number' &&
    typeof v.timestamp === 'string'
  );
}

/** Entradas na ordem em que foram gravadas (mais antiga primeiro). */
function lerGravado(): EntradaHistorico[] {
  try {
    const bruto = armazenamento()?.getItem(CHAVE);
    if (!bruto) return [];
    const lista: unknown = JSON.parse(bruto);
    return Array.isArray(lista) ? lista.filter(ehEntrada) : [];
  } catch {
    return [];
  }
}

/** Histórico com os registros mais recentes primeiro. */
export function lerHistorico(): EntradaHistorico[] {
  return lerGravado().reverse();
}

/** Acrescenta registros ao histórico, mantendo só as últimas LIMITE_HISTORICO entradas. */
export function registrarNoHistorico(registros: Registro[], agora: Date = new Date()): void {
  if (registros.length === 0) return;

  const momento = agora.toISOString();
  const novos: EntradaHistorico[] = registros.map((r) => ({
    nome: String(r.nome),
    acao: r.acao,
    score: Number(r.score) || 0,
    timestamp: momento,
  }));

  try {
    const todos = lerGravado().concat(novos).slice(-LIMITE_HISTORICO);
    armazenamento()?.setItem(CHAVE, JSON.stringify(todos));
  } catch {
    // cota cheia ou armazenamento indisponível: o histórico simplesmente não é guardado
  }
}

export function limparHistorico(): void {
  try {
    armazenamento()?.removeItem(CHAVE);
  } catch {
    // nada a limpar se o armazenamento não está disponível
  }
}

export function formatarDataHora(iso: string): string {
  const data = new Date(iso);
  return Number.isNaN(data.getTime()) ? iso : data.toLocaleString('pt-BR');
}

/** Conteúdo do .txt de download (mais recentes primeiro). */
export function textoDoHistorico(): string {
  const linhas = ['HISTORICO DE COMPARACOES', '========================', ''];
  for (const e of lerHistorico()) {
    linhas.push(`${e.nome} | ${e.acao} | ${e.score}% | ${formatarDataHora(e.timestamp)}`);
  }
  return linhas.join('\n');
}
