#!/usr/bin/env bash
# Teste de fumaça da aplicação em execução (usado pelo job "docker" do CI; também roda localmente).
#
#   bash .github/scripts/smoke-test.sh [URL_BASE]      (padrão: http://localhost:8080)
#
# Falha (código de saída != 0) se qualquer verificação não passar.
set -euo pipefail

BASE="${1:-http://localhost:8080}"
BASE="${BASE%/}"
ESPERA_MAXIMA=30

falhar() {
  echo "FALHA: $*" >&2
  exit 1
}

# 1) Espera /health responder 200 (até ESPERA_MAXIMA segundos)
echo "Aguardando ${BASE}/health (até ${ESPERA_MAXIMA}s)..."
pronto=0
codigo=""
SECONDS=0 # variável do bash: segundos desde esta linha (o prazo vale em tempo real, não em tentativas)
while [ "$SECONDS" -lt "$ESPERA_MAXIMA" ]; do
  codigo=$(curl -s -o /dev/null -w '%{http_code}' --max-time 2 "${BASE}/health" || true)
  if [ "$codigo" = "200" ]; then
    pronto=1
    echo "  /health respondeu 200 após ~${SECONDS}s"
    break
  fi
  sleep 1
done
[ "$pronto" = "1" ] || falhar "/health não respondeu 200 em ${ESPERA_MAXIMA}s (último código: ${codigo:-nenhum})"

# 2) GET / devolve 200 e contém o título da página
cabecalhos=$(mktemp)
corpo=$(mktemp)
trap 'rm -f "$cabecalhos" "$corpo"' EXIT

codigo=$(curl -s -D "$cabecalhos" -o "$corpo" -w '%{http_code}' --max-time 10 "${BASE}/")
[ "$codigo" = "200" ] || falhar "GET / devolveu ${codigo} (esperado 200)"
grep -q '<title>Comparador de Nomes' "$corpo" || falhar "GET / não contém o título 'Comparador de Nomes'"
echo "  GET / -> 200 com o título esperado"

# 3) Cabeçalhos de segurança
grep -qi '^content-security-policy:' "$cabecalhos" || falhar "cabeçalho Content-Security-Policy ausente"
grep -qi '^x-content-type-options: *nosniff' "$cabecalhos" || falhar "cabeçalho X-Content-Type-Options: nosniff ausente"
echo "  Content-Security-Policy e X-Content-Type-Options presentes"

# 4) POST /comparar com um exemplo mínimo
requisicao='{"sistema":"Ana Lima\nBruno Costa","control":"Ana Lima\nBruno Costta\nCarlos Souza"}'
codigo=$(curl -s -o "$corpo" -w '%{http_code}' --max-time 10 \
  -H 'Content-Type: application/json' --data-binary "$requisicao" "${BASE}/comparar")
[ "$codigo" = "200" ] || falhar "POST /comparar devolveu ${codigo} (esperado 200): $(cat "$corpo")"

json=$(cat "$corpo")
# "Carlos Souza" não existe no sistema: vai para remoção. "Bruno Costta" é parecido com "Bruno Costa": só é sugerido.
echo "$json" | grep -qF '"remover":["Carlos Souza"]' || falhar "campo 'remover' inesperado: $json"
echo "$json" | grep -qF '"control":"Bruno Costta","sistema":"Bruno Costa"' || falhar "campo 'similares' inesperado: $json"
echo "  POST /comparar -> remover e similares conforme esperado"

echo "OK: todas as verificações passaram em ${BASE}"
