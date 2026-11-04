import Swal from 'sweetalert2/dist/sweetalert2.esm.js';

/**
 * Wrappers do SweetAlert2. O tema escuro vem do CSS (classes .swal2-*, em main.scss).
 * Só texto puro (`text`) ou elementos DOM (`html: HTMLElement`): nunca strings HTML com dados do usuário.
 */

const BASE = { allowOutsideClick: false } as const;

export function aviso(titulo: string, texto: string) {
  return Swal.fire({
    ...BASE,
    icon: 'warning',
    title: titulo,
    text: texto,
    confirmButtonText: 'Entendi',
  });
}

export function informacao(titulo: string, texto: string) {
  return Swal.fire({ ...BASE, icon: 'info', title: titulo, text: texto, confirmButtonText: 'Ok' });
}

export function erro(titulo: string, texto: string) {
  return Swal.fire({
    ...BASE,
    icon: 'error',
    title: titulo,
    text: texto,
    confirmButtonText: 'Fechar',
  });
}

export function sucessoRapido(titulo: string, texto: string) {
  return Swal.fire({
    icon: 'success',
    title: titulo,
    text: texto,
    timer: 1500,
    timerProgressBar: true,
    showConfirmButton: false,
  });
}

/** Sucesso com conteúdo montado em DOM (use dom.ts para criá-lo). */
export function sucessoComConteudo(titulo: string, conteudo: HTMLElement, botao: string) {
  return Swal.fire({
    ...BASE,
    icon: 'success',
    title: titulo,
    html: conteudo,
    confirmButtonText: botao,
  });
}

export function carregando(titulo: string, texto: string) {
  return Swal.fire({
    ...BASE,
    icon: 'info',
    title: titulo,
    text: texto,
    didOpen: () => Swal.showLoading(),
  });
}

export function fecharAlerta() {
  Swal.close();
}

export async function confirmar(
  titulo: string,
  texto: string,
  rotuloConfirmar: string,
): Promise<boolean> {
  const resultado = await Swal.fire({
    ...BASE,
    icon: 'warning',
    title: titulo,
    text: texto,
    showCancelButton: true,
    confirmButtonText: rotuloConfirmar,
    cancelButtonText: 'Cancelar',
  });
  return resultado.isConfirmed;
}
