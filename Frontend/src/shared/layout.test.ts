import { beforeEach, describe, expect, it } from 'vitest';
import { montarNavbar } from './layout';

beforeEach(() => {
  document.body.replaceChildren();
  const destino = document.createElement('header');
  destino.id = 'navbar';
  document.body.append(destino);
});

describe('montarNavbar', () => {
  it('cria os três links e marca só o da página atual como ativo', () => {
    montarNavbar('limpeza');

    const links = [...document.querySelectorAll<HTMLAnchorElement>('#navbar a.nav-link')];
    expect(links.map((a) => a.getAttribute('href'))).toEqual([
      '/',
      '/limpeza.html',
      '/historico.html',
    ]);

    const ativos = links.filter((a) => a.classList.contains('active'));
    expect(ativos).toHaveLength(1);
    expect(ativos[0]?.getAttribute('href')).toBe('/limpeza.html');
    expect(ativos[0]?.getAttribute('aria-current')).toBe('page');
    expect(links.filter((a) => a !== ativos[0]).every((a) => !a.hasAttribute('aria-current'))).toBe(
      true,
    );
  });

  it.each([
    ['comparador', '/'],
    ['historico', '/historico.html'],
  ] as const)('página %s ativa o link %s', (pagina, href) => {
    montarNavbar(pagina);
    expect(document.querySelector('#navbar a.nav-link.active')?.getAttribute('href')).toBe(href);
  });

  it('tem o botão que recolhe o menu no celular, ligado ao menu', () => {
    montarNavbar('comparador');

    const botao = document.querySelector('#navbar button.navbar-toggler');
    const alvo = botao?.getAttribute('data-bs-target');

    expect(alvo).toBe('#menuPrincipal');
    expect(document.querySelector(alvo ?? '')?.classList.contains('collapse')).toBe(true);
    expect(botao?.getAttribute('aria-label')).toBeTruthy();
  });

  it('não usa estilos nem manipuladores inline (CSP)', () => {
    montarNavbar('comparador');

    const todos = document.querySelectorAll('#navbar *');
    for (const elemento of todos) {
      expect(elemento.hasAttribute('style')).toBe(false);
      expect([...elemento.attributes].some((a) => a.name.startsWith('on'))).toBe(false);
    }
  });

  it('lança erro claro se a página não tiver o #navbar', () => {
    document.body.replaceChildren();
    expect(() => montarNavbar('comparador')).toThrow(/navbar/);
  });
});
