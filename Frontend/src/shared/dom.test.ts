import { describe, expect, it } from 'vitest';
import { el, limparFilhos, porId } from './dom';

describe('el', () => {
  it('um nome com <img onerror> aparece como texto, nunca como elemento', () => {
    const malicioso = '<img src=x onerror="window.__xss = true">';
    const celula = el('td', {}, malicioso);
    document.body.append(celula);

    expect(celula.textContent).toBe(malicioso);
    expect(celula.querySelector('img')).toBeNull();
    expect(celula.childElementCount).toBe(0);
    expect((window as unknown as { __xss?: boolean }).__xss).toBeUndefined();
  });

  it('também neutraliza <script> e entidades HTML', () => {
    const celula = el('span', {}, '<script>alert(1)</script> &amp; <b>negrito</b>');

    expect(celula.querySelector('script')).toBeNull();
    expect(celula.querySelector('b')).toBeNull();
    expect(celula.textContent).toBe('<script>alert(1)</script> &amp; <b>negrito</b>');
  });

  it('aplica classe e atributos', () => {
    const botao = el('button', {
      classe: 'btn btn-primary',
      atributos: { type: 'button', 'data-bs-toggle': 'collapse' },
    });

    expect(botao.className).toBe('btn btn-primary');
    expect(botao.getAttribute('type')).toBe('button');
    expect(botao.dataset.bsToggle).toBe('collapse');
  });

  it('aceita nós, textos e ignora null/undefined/false', () => {
    const filho = el('i', { classe: 'bi bi-search' });
    const pai = el('a', {}, filho, ' Buscar', null, undefined, false);

    expect(pai.firstElementChild).toBe(filho);
    expect(pai.textContent).toBe(' Buscar');
  });

  it.each(['onclick', 'onerror', 'ONLOAD', 'style'])(
    'recusa o atributo %s (manipulador inline / estilo inline)',
    (nome) => {
      expect(() => el('div', { atributos: { [nome]: 'alert(1)' } })).toThrow();
    },
  );
});

describe('limparFilhos', () => {
  it('remove todos os filhos', () => {
    const lista = el('ul', {}, el('li', {}, 'a'), el('li', {}, 'b'));
    limparFilhos(lista);
    expect(lista.childNodes.length).toBe(0);
  });
});

describe('porId', () => {
  it('devolve o elemento tipado', () => {
    document.body.append(el('input', { atributos: { id: 'campo-teste' } }));
    expect(porId<HTMLInputElement>('campo-teste').tagName).toBe('INPUT');
  });

  it('lança erro claro se o elemento não existir', () => {
    expect(() => porId('nao-existe')).toThrow(/nao-existe/);
  });
});
