import '../shared/tema';
import {
  aviso,
  carregando,
  erro,
  informacao,
  sucessoComConteudo,
  sucessoRapido,
} from '../shared/alertas';
import { ApiError, comparar } from '../shared/api';
import { baixarTexto, copiarParaAreaDeTransferencia, el, limparFilhos, porId } from '../shared/dom';
import { descreverCodificacao, lerArquivoTexto } from '../shared/encoding';
import { registrarNoHistorico } from '../shared/historico';
import { montarNavbar } from '../shared/layout';
import { type Direcao, ordenarPorScore, proximaDirecao } from '../shared/ordenacao';
import type { Similar } from '../shared/tipos';

let similares: Similar[] = [];
let direcao: Direcao = 'nenhuma';

const classeDoScore = (score: number) =>
  score >= 100 ? 'score-100' : score > 0 ? 'score-parcial' : 'score-zero';

/** Mostra, sob o campo de arquivo, qual codificação foi detectada assim que o arquivo é escolhido. */
function monitorarArquivo(idInput: string, idAviso: string): void {
  const input = porId<HTMLInputElement>(idInput);
  const avisoDeArquivo = porId(idAviso);

  input.addEventListener('change', async () => {
    avisoDeArquivo.textContent = '';
    const arquivo = input.files?.[0];
    if (!arquivo) return;
    try {
      avisoDeArquivo.textContent = descreverCodificacao(await lerArquivoTexto(arquivo));
    } catch {
      avisoDeArquivo.textContent = 'Não foi possível ler o arquivo.';
    }
  });
}

function desenharSimilares(comparado: boolean): void {
  const corpo = porId('corpoSimilares');
  limparFilhos(corpo);

  if (similares.length === 0) {
    const texto = comparado ? 'Nenhum item parecido.' : 'Nenhuma comparação realizada ainda.';
    corpo.append(
      el(
        'tr',
        {},
        el('td', { classe: 'text-center text-secondary', atributos: { colspan: '3' } }, texto),
      ),
    );
  } else {
    for (const par of ordenarPorScore(similares, direcao)) {
      corpo.append(
        el(
          'tr',
          {},
          el('td', { classe: 'texto-quebra' }, par.control),
          el('td', { classe: 'texto-quebra' }, par.sistema),
          el('td', {}, el('span', { classe: classeDoScore(par.score) }, `${par.score}%`)),
        ),
      );
    }
  }

  const aria = { nenhuma: 'none', asc: 'ascending', desc: 'descending' } as const;
  const icone = {
    nenhuma: 'bi-arrow-down-up',
    asc: 'bi-sort-numeric-down',
    desc: 'bi-sort-numeric-up',
  } as const;
  porId('colunaScore').setAttribute('aria-sort', aria[direcao]);
  porId('iconeOrdem').className = `bi ${icone[direcao]}`;
}

function resumo(
  totalRemover: number,
  totalSimilares: number,
  avisosDeArquivo: string[],
): HTMLElement {
  return el(
    'div',
    { classe: 'text-start' },
    el('p', {}, el('strong', {}, 'Para remover: '), String(totalRemover)),
    el(
      'p',
      {},
      el('strong', {}, 'Similaridades: '),
      String(totalSimilares),
      ' (não entram na remoção; revise)',
    ),
    ...avisosDeArquivo.map((texto) => el('p', { classe: 'small text-info mb-1' }, texto)),
  );
}

async function aoComparar(): Promise<void> {
  const arquivoSistema = porId<HTMLInputElement>('fileSistema').files?.[0];
  const arquivoControl = porId<HTMLInputElement>('fileControl').files?.[0];
  let sistema = porId<HTMLTextAreaElement>('sistema').value;
  let control = porId<HTMLTextAreaElement>('control').value;
  const avisosDeArquivo: string[] = [];

  try {
    if (arquivoSistema) {
      const leitura = await lerArquivoTexto(arquivoSistema);
      sistema = leitura.texto;
      avisosDeArquivo.push(`Sistema — ${descreverCodificacao(leitura)}`);
    }
    if (arquivoControl) {
      const leitura = await lerArquivoTexto(arquivoControl);
      control = leitura.texto;
      avisosDeArquivo.push(`Control ID — ${descreverCodificacao(leitura)}`);
    }
  } catch {
    await erro('Erro ao Ler o Arquivo', 'Não foi possível ler o arquivo selecionado.');
    return;
  }

  if (!sistema.trim() || !control.trim()) {
    await aviso('Dados Incompletos', 'Preencha ou envie os dois lados (Sistema e Control ID).');
    return;
  }

  void carregando('Processando...', 'Comparando as listas, aguarde um momento');

  try {
    const resultado = await comparar(sistema, control);

    porId<HTMLTextAreaElement>('resultadoRemover').value = resultado.remover.join('\n');
    porId('contagemRemover').textContent = String(resultado.remover.length);
    porId('contagemSimilares').textContent = String(resultado.similares.length);
    similares = resultado.similares;
    desenharSimilares(true);
    registrarNoHistorico(resultado.registros);

    await sucessoComConteudo(
      'Comparação Concluída!',
      resumo(resultado.remover.length, resultado.similares.length, avisosDeArquivo),
      'Ver Resultados',
    );
  } catch (e) {
    await erro(
      'Erro ao Processar',
      e instanceof ApiError ? e.message : 'Houve um problema ao comparar as listas.',
    );
  }
}

async function aoCopiar(): Promise<void> {
  const texto = porId<HTMLTextAreaElement>('resultadoRemover').value;
  if (!texto) {
    await informacao('Nada para Copiar', 'Faça uma comparação primeiro.');
    return;
  }
  if (await copiarParaAreaDeTransferencia(texto)) {
    await sucessoRapido('Copiado!', 'Lista copiada para a área de transferência.');
  } else {
    await erro('Não foi possível copiar', 'Selecione o texto e copie manualmente (Ctrl+C).');
  }
}

async function aoBaixar(): Promise<void> {
  const texto = porId<HTMLTextAreaElement>('resultadoRemover').value;
  if (!texto) {
    await informacao('Nada para Baixar', 'Faça uma comparação primeiro.');
    return;
  }
  baixarTexto('para_remover.txt', texto);
}

montarNavbar('comparador');
monitorarArquivo('fileSistema', 'avisoFileSistema');
monitorarArquivo('fileControl', 'avisoFileControl');
desenharSimilares(false);

porId('btnComparar').addEventListener('click', () => void aoComparar());
porId('btnCopiarRemover').addEventListener('click', () => void aoCopiar());
porId('btnBaixarRemover').addEventListener('click', () => void aoBaixar());
porId('btnOrdenarScore').addEventListener('click', () => {
  direcao = proximaDirecao(direcao);
  desenharSimilares(true);
});
