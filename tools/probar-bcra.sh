#!/usr/bin/env bash
# Prueba de la API Central de Deudores del BCRA.
# Uso: ./probar-bcra.sh [CUIT] [proxy]
#   ej: ./probar-bcra.sh 30500010912
#       ./probar-bcra.sh 30500010912 http://10.2.248.246:8080
# Genera la carpeta bcra-salida/ con headers y bodies de cada caso (sirven como fixtures para los tests).

CUIT="${1:-30500010912}"
PROXY="${2:-}"
BASE="https://api.bcra.gob.ar"
OUT="bcra-salida"
mkdir -p "$OUT"

CURL_OPTS=(-s -m 30 -D -)
[ -n "$PROXY" ] && CURL_OPTS+=(-x "$PROXY")

probar () {
  local nombre="$1" path="$2"; shift 2
  echo "=================================================================="
  echo "== $nombre"
  echo "== GET $BASE$path"
  local res
  res=$(curl "${CURL_OPTS[@]}" "$@" -o "$OUT/$nombre.json" \
        -w "\n__HTTP=%{http_code} __TIEMPO=%{time_total}s __TLS_VERIFY=%{ssl_verify_result}" \
        "$BASE$path")
  echo "$res" | sed -n '/^__HTTP/!p' > "$OUT/$nombre.headers.txt"
  echo "$res" | grep '__HTTP'
  echo "-- headers interesantes:"
  grep -iE '^(content-type|cache-control|retry-after|x-ratelimit|ratelimit|x-correlation|server|date)' "$OUT/$nombre.headers.txt" || echo "   (ninguno)"
  echo "-- body (primeros 800 bytes):"
  head -c 800 "$OUT/$nombre.json"; echo
}

probar "01-deudas-ok"              "/CentralDeDeudores/v1.0/Deudas/$CUIT"
probar "02-historicas-ok"          "/CentralDeDeudores/v1.0/Deudas/Historicas/$CUIT"
probar "03-cheques-rechazados"     "/CentralDeDeudores/v1.0/Deudas/ChequesRechazados/$CUIT"
probar "04-deudas-404"             "/CentralDeDeudores/v1.0/Deudas/12345678912"
probar "05-deudas-400"             "/CentralDeDeudores/v1.0/Deudas/123456789"
probar "06-path-minuscula"         "/centraldedeudores/v1.0/deudas/$CUIT"
probar "07-con-correlation-id"     "/CentralDeDeudores/v1.0/Deudas/$CUIT" -H "X-Correlation-ID: prueba-$(date +%s)"

echo "=================================================================="
echo "== 08 latencia: 5 llamadas seguidas a Deudas"
for i in 1 2 3 4 5; do
  curl -s -m 30 ${PROXY:+-x "$PROXY"} -o /dev/null \
       -w "  intento $i: HTTP %{http_code} en %{time_total}s\n" \
       "$BASE/CentralDeDeudores/v1.0/Deudas/$CUIT"
done

echo
echo "Listo. Archivos en ./$OUT  (si __TLS_VERIFY != 0 hay un problema de certificado)"
