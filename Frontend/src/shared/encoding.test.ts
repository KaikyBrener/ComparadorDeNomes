import { describe, expect, it } from 'vitest';
import { decodificarTexto, descreverCodificacao, lerArquivoTexto } from './encoding';

const buffer = (bytes: number[]): ArrayBuffer => Uint8Array.from(bytes).buffer;
const utf8 = (texto: string) => [...new TextEncoder().encode(texto)];
const utf16le = (texto: string) =>
  [...texto].flatMap((c) => [c.charCodeAt(0) & 0xff, c.charCodeAt(0) >> 8]);
const utf16be = (texto: string) =>
  [...texto].flatMap((c) => [c.charCodeAt(0) >> 8, c.charCodeAt(0) & 0xff]);

describe('decodificarTexto: os 4 formatos de bytes', () => {
  it('UTF-8 válido', () => {
    const r = decodificarTexto(buffer(utf8('José Açaí')));
    expect(r).toEqual({ texto: 'José Açaí', codificacao: 'UTF-8', alternativa: false });
  });

  it('UTF-8 com BOM: o BOM não vira caractere', () => {
    const r = decodificarTexto(buffer([0xef, 0xbb, 0xbf, ...utf8('José')]));
    expect(r.texto).toBe('José');
    expect(r.codificacao).toBe('UTF-8');
  });

  it('windows-1252 (ANSI do Windows): cai para o plano B e avisa', () => {
    // "José Açaí" em windows-1252: é = E9, ç = E7, í = ED
    const r = decodificarTexto(buffer([0x4a, 0x6f, 0x73, 0xe9, 0x20, 0x41, 0xe7, 0x61, 0xed]));
    expect(r).toEqual({ texto: 'José Açaí', codificacao: 'windows-1252', alternativa: true });
  });

  it('UTF-16 LE com BOM', () => {
    const r = decodificarTexto(buffer([0xff, 0xfe, ...utf16le('José')]));
    expect(r).toEqual({ texto: 'José', codificacao: 'UTF-16 LE', alternativa: false });
  });

  it('UTF-16 BE com BOM', () => {
    const r = decodificarTexto(buffer([0xfe, 0xff, ...utf16be('José')]));
    expect(r).toEqual({ texto: 'José', codificacao: 'UTF-16 BE', alternativa: false });
  });

  it('um U+FFFD legítimo dentro de UTF-8 válido NÃO dispara o plano B', () => {
    const r = decodificarTexto(buffer(utf8('a\uFFFDb')));
    expect(r.codificacao).toBe('UTF-8');
    expect(r.texto).toBe('a\uFFFDb');
  });

  it('arquivo vazio é UTF-8 vazio', () => {
    expect(decodificarTexto(buffer([]))).toEqual({
      texto: '',
      codificacao: 'UTF-8',
      alternativa: false,
    });
  });
});

describe('lerArquivoTexto / descreverCodificacao', () => {
  it('lê os bytes do arquivo e decodifica', async () => {
    const arquivo = { arrayBuffer: async () => buffer([0x4a, 0xe9]) } as unknown as File;
    const r = await lerArquivoTexto(arquivo);
    expect(r.texto).toBe('Jé');
    expect(r.alternativa).toBe(true);
  });

  it('descreve a codificação, destacando o plano B', () => {
    expect(descreverCodificacao({ texto: '', codificacao: 'UTF-8', alternativa: false })).toBe(
      'Codificação: UTF-8',
    );
    expect(
      descreverCodificacao({ texto: '', codificacao: 'windows-1252', alternativa: true }),
    ).toBe('Codificação: windows-1252 (o arquivo não era UTF-8 válido)');
  });
});
