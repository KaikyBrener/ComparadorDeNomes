export interface LeituraDeTexto {
  texto: string;
  codificacao: string;
  /** true quando o arquivo não era UTF-8 válido e foi lido pelo plano B (windows-1252). */
  alternativa: boolean;
}

/**
 * Decodifica os bytes de um .txt: UTF-16 (reconhecido pelo BOM), UTF-8 estrito ou, se houver bytes
 * inválidos (arquivo salvo em ANSI/Windows-1252), windows-1252. O modo estrito (fatal) detecta bytes
 * inválidos sem confundir com um U+FFFD legítimo dentro de um UTF-8 válido.
 */
export function decodificarTexto(buffer: ArrayBuffer): LeituraDeTexto {
  const bytes = new Uint8Array(buffer);

  if (bytes[0] === 0xff && bytes[1] === 0xfe) {
    return {
      texto: new TextDecoder('utf-16le').decode(bytes),
      codificacao: 'UTF-16 LE',
      alternativa: false,
    };
  }
  if (bytes[0] === 0xfe && bytes[1] === 0xff) {
    return {
      texto: new TextDecoder('utf-16be').decode(bytes),
      codificacao: 'UTF-16 BE',
      alternativa: false,
    };
  }

  try {
    // O BOM do UTF-8, se existir, é removido pelo próprio decoder
    const texto = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    return { texto, codificacao: 'UTF-8', alternativa: false };
  } catch {
    return {
      texto: new TextDecoder('windows-1252').decode(bytes),
      codificacao: 'windows-1252',
      alternativa: true,
    };
  }
}

export async function lerArquivoTexto(arquivo: File): Promise<LeituraDeTexto> {
  return decodificarTexto(await arquivo.arrayBuffer());
}

export function descreverCodificacao(leitura: LeituraDeTexto): string {
  return (
    `Codificação: ${leitura.codificacao}` +
    (leitura.alternativa ? ' (o arquivo não era UTF-8 válido)' : '')
  );
}
