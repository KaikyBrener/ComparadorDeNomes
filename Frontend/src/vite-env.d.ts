/// <reference types="vite/client" />

// Usamos a build ESM do SweetAlert2 SEM o CSS embutido: a versão "all" injeta uma tag <style> em
// tempo de execução, o que a CSP (style-src 'self') bloqueia. O CSS vem como arquivo, via import.
declare module 'sweetalert2/dist/sweetalert2.esm.js' {
  import Swal from 'sweetalert2';
  export default Swal;
}
