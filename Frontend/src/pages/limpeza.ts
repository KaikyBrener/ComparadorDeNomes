import '../shared/tema';
import { aviso, carregando, erro, informacao, sucessoRapido } from '../shared/alertas';
import { ApiError, limpar } from '../shared/api';
import { copiarParaAreaDeTransferencia, porId } from '../shared/dom';
import { montarNavbar } from '../shared/layout';
import type { TipoFonte } from '../shared/tipos';

async function aoLimpar(): Promise<void> {
  const dados = porId<HTMLTextAreaElement>('dadosLimpeza').value;
  const tipo = porId<HTMLSelectElement>('tipoFabricante').value as TipoFonte;
  const saida = porId<HTMLTextAreaElement>('resultadoLimpeza');

  if (!dados.trim()) {
    await aviso('Dados Vazios', 'Cole os dados que deseja limpar.');
    return;
  }

  void carregando('Limpando Dados...', 'Processando os dados, aguarde');

  try {
    const { limpo } = await limpar(dados, tipo);
    saida.value = limpo;
    const total = limpo.trim() === '' ? 0 : limpo.split('\n').length;
    porId('contagemLimpeza').textContent = String(total);

    if (total === 0) {
      await aviso(
        'Nenhum Dado Encontrado',
        'Nenhum dado válido foi encontrado para este formato. Verifique a fonte selecionada.',
      );
      return;
    }
    await sucessoRapido('Dados Limpos!', `${total} linha(s) pronta(s) para copiar.`);
  } catch (e) {
    saida.value = '';
    porId('contagemLimpeza').textContent = '0';
    await erro(
      'Erro ao Processar',
      e instanceof ApiError ? e.message : 'Houve um problema ao limpar os dados.',
    );
  }
}

async function aoCopiar(): Promise<void> {
  const texto = porId<HTMLTextAreaElement>('resultadoLimpeza').value;
  if (!texto) {
    await informacao('Nada para Copiar', 'Realize a limpeza de dados primeiro.');
    return;
  }
  if (await copiarParaAreaDeTransferencia(texto)) {
    await sucessoRapido('Copiado!', 'Dados copiados para a área de transferência.');
  } else {
    await erro('Não foi possível copiar', 'Selecione o texto e copie manualmente (Ctrl+C).');
  }
}

montarNavbar('limpeza');
porId('btnLimpar').addEventListener('click', () => void aoLimpar());
porId('btnCopiar').addEventListener('click', () => void aoCopiar());
