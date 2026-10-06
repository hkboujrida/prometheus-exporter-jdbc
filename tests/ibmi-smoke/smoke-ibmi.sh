#!/bin/sh
# IBM i field-test: runs the exporter (image WITH the ACS ODBC driver,
# --build-arg ACS_ODBC=true) against a real IBM i system and asserts the
# QSYS2/SYSTOOLS gauges actually appear.
#
# Usage (from repo root):
#   IBMI_HOST=pub400.com IBMI_USER=profile IBMI_PASS=secret tests/ibmi-smoke/smoke-ibmi.sh
#
# Env:
#   IBMI_HOST (default pub400.com), IBMI_USER, IBMI_PASS (required)
#   IMAGE    image tag to build (default prometheus-exporter-jdbc:ibmi-smoke)
#   KEEP     set to 1 to leave the container running for inspection
set -eu

HOST="${IBMI_HOST:-pub400.com}"
[ -n "${IBMI_USER:-}" ] && [ -n "${IBMI_PASS:-}" ] || {
    echo "usage: IBMI_HOST=h IBMI_USER=u IBMI_PASS=p $0"; exit 2; }
IMAGE="${IMAGE:-prometheus-exporter-jdbc:ibmi-smoke}"
NAME=promexporter-ibmi-smoke
PORT=19855

cd "$(dirname "$0")/../.."
CFG="$(mktemp -d)/config.json"
sed -e "s|__HOST__|$HOST|g" -e "s|__USER__|$IBMI_USER|g" -e "s|__PASS__|$IBMI_PASS|g" \
    tests/ibmi-smoke/config-ibmi.json > "$CFG"

cleanup() {
    docker rm -f "$NAME" >/dev/null 2>&1 || true
    rm -f "$CFG"
}
[ "${KEEP:-0}" = 1 ] || trap cleanup EXIT

echo "== building image with ACS ODBC driver"
docker build -q --build-arg ACS_ODBC=true -t "$IMAGE" .

echo "== starting exporter against $HOST"
docker rm -f "$NAME" >/dev/null 2>&1 || true
docker run -d --rm --name "$NAME" \
    -p "127.0.0.1:$PORT:9853" \
    -e PROMCLIENT_NONINTERACTIVE=1 \
    -v "$CFG:/app/config.json:ro" \
    "$IMAGE" >/dev/null

i=0
until curl -fs "http://127.0.0.1:$PORT/metrics" -o /tmp/ibmi-scrape.txt 2>/dev/null; do
    i=$((i+1)); [ $i -gt 60 ] && { echo "FAIL: exporter never served /metrics"; docker logs "$NAME" 2>&1 | tail -20; exit 1; }
    sleep 1
done
# give the interval loop one more cycle
sleep 3
curl -fs "http://127.0.0.1:$PORT/metrics" -o /tmp/ibmi-scrape.txt

RC=0
check() { # name pattern [min]
    line=$(grep -E "^$2\{[^}]*\} [0-9]" /tmp/ibmi-scrape.txt | head -1 || true)
    [ -n "$line" ] || { echo "FAIL: $1 missing"; RC=1; return; }
    val=${line##* }
    min=${3:-0}
    ok=$(awk -v v="$val" -v m="$min" 'BEGIN{print (v+0>=m+0)?"y":"n"}')
    [ "$ok" = y ] && echo "ok:   $1 = $val" || { echo "FAIL: $1 = $val (expected >= $min)"; RC=1; }
}

echo "== asserting /metrics"
check "SYS_TABLE_COUNT (single-row)"   "SYS_TABLE_COUNT" 1
check "SYS_JOB_COUNT (table function)" "SYS_JOB_COUNT"   1
check "SCHEMA_*__CNT (multi-row)"      "SCHEMA_QSYS2__CNT"
curl -fs "http://127.0.0.1:$PORT/metrics_now" -o /tmp/ibmi-scrape-now.txt
echo "== asserting /metrics_now"
check "ENV_OS_RELEASE (on-demand)"     "ENV_OS_RELEASE"

if [ $RC -eq 0 ]; then
    echo "IBM i SMOKE OK ($HOST)"
else
    echo "-- exporter log tail --"
    docker logs "$NAME" 2>&1 | tail -20
    echo "IBM i SMOKE FAILED"
fi
exit $RC
