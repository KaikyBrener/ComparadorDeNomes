/** Tipos que espelham os DTOs do back-end (Dtos.cs / Endpoints.cs). */

/** Par parecido: nome no Control ID, nome no sistema e score em % (0 a 100). */
export interface Similar {
  control: string;
  sistema: string;
  score: number;
}

/** Uma linha do histórico devolvida por /comparar. Em "Remover" o score é sempre 0. */
export interface Registro {
  nome: string;
  acao: 'Remover' | 'Similar';
  score: number;
}

/** Resposta de POST /comparar. */
export interface ComparacaoResponse {
  remover: string[];
  similares: Similar[];
  registros: Registro[];
}

/** Resposta de POST /limpar. */
export interface LimpezaResponse {
  limpo: string;
}

/** Fontes aceitas por POST /limpar. */
export type TipoFonte = 'sistema' | 'hikvision' | 'control' | 'intelbras';

/** Corpo de erro da API. */
export interface ErroApi {
  error?: string;
}

/** Entrada guardada no localStorage: um Registro + quando foi gerado (ISO 8601). */
export interface EntradaHistorico extends Registro {
  timestamp: string;
}
