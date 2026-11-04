import '../shared/tema';
import { confirmar, informacao, sucessoRapido } from '../shared/alertas';
import { baixarTexto, el, limparFilhos, porId } from '../shared/dom';
import {
  formatarDataHora,
  lerHistorico,
  limparHistorico,
  textoDoHistorico,
} from '../shared/historico';
import { montarNavbar } from '../shared/layout';

const classeDoScore = (score: number) =>
  score >= 100 ? 'score-100' : score > 0 ? 'score-parcial' : 'score-zero';

/** Redesenha a tabela. Todo conteúdo entra por textContent (via dom.ts), nunca como HTML. */
function desenhar(): void {
  const entradas = lerHistorico();
  const corpo = porId('corpoHistorico');
  limparFilhos(corpo);
  porId('contagemHistorico').textContent = String(entradas.length);

  if (entradas.length === 0) {
    corpo.append(
      el(
        'tr',
        {},
        el(
          'td',
          { classe: 'text-center text-secondary', atributos: { colspan: '4' } },
          'Nenhum registro ainda.',
        ),
      ),
    );
    return;
  }

  for (const entrada of entradas) {
    corpo.append(
      el(
        'tr',
        {},
        el('td', { classe: 'texto-quebra col-nome' }, entrada.nome),
        el('td', {}, entrada.acao),
        el('td', {}, el('span', { classe: classeDoScore(entrada.score) }, `${entrada.score}%`)),
        el('td', { classe: 'text-nowrap' }, formatarDataHora(entrada.timestamp)),
      ),
    );
  }
}

async function aoBaixar(): Promise<void> {
  if (lerHistorico().length === 0) {
    await informacao('Nada para Baixar', 'O histórico está vazio.');
    return;
  }
  baixarTexto('log_comparacoes.txt', textoDoHistorico());
}

async function aoLimpar(): Promise<void> {
  if (lerHistorico().length === 0) {
    await informacao('Histórico vazio', 'Não há nada para limpar.');
    return;
  }
  if (!(await confirmar('Limpar histórico?', 'Esta ação não pode ser desfeita.', 'Sim, limpar')))
    return;

  limparHistorico();
  desenhar();
  await sucessoRapido('Histórico limpo!', 'Os registros foram apagados deste navegador.');
}

montarNavbar('historico');
desenhar();
porId('btnBaixarHistorico').addEventListener('click', () => void aoBaixar());
porId('btnLimparHistorico').addEventListener('click', () => void aoLimpar());
