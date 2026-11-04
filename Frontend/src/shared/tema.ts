// Importado uma vez por página: estilos, fontes self-hosted e o JS do Bootstrap que usamos
// (abas e menu recolhível). Nada de CDN: tudo é empacotado pelo Vite e servido pela própria origem.
import '@fontsource/poppins/latin-400.css';
import '@fontsource/poppins/latin-600.css';
import '@fontsource/poppins/latin-700.css';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'sweetalert2/dist/sweetalert2.min.css';
import '../styles/main.scss';

// O Bootstrap ativa esses componentes pelos atributos data-bs-* (data-api) ao ser importado
import 'bootstrap/js/dist/collapse';
import 'bootstrap/js/dist/tab';
