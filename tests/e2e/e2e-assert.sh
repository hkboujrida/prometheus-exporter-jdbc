#!/bin/sh
# Asserts the compose harness is actually serving metrics through a real
# Prometheus: all targets UP, families present, point-in-time endpoint works.
set -eu

PROM=http://prometheus:9090

echo "== waiting for targets UP =="
i=0
while [ $i -lt 60 ]; do
  up=$(curl -fs "$PROM/api/v1/targets" | grep -o '"health":"up"' | wc -l)
  if [ "$up" -ge 3 ]; then echo "targets up: $up"; break; fi
  i=$((i + 1))
  sleep 2
done
[ "$i" -lt 60 ] || { echo "FAIL: targets never came up"; exit 1; }

check_series() {
  name=$1
  i=0
  while [ $i -lt 30 ]; do
    n=$(curl -fs -G --data-urlencode "match[]=$name" "$PROM/api/v1/series" | grep -c "$name" || true)
    [ "$n" -ge 1 ] && { echo "OK series $name"; return 0; }
    i=$((i + 1))
    sleep 2
  done
  echo "FAIL: series $name never appeared"
  exit 1
}

check_series 'STAT__CNT'
check_series 'POOL__QWORK__SIZE'
check_series 'NOWONLY__CNT'

echo "== value sanity =="
v=$(curl -fs "$PROM/api/v1/query?query=STAT__CNT" | sed -nE 's/.*"value":\[[^,]*,"([0-9.]+)"\].*/\1/p' | head -1)
[ "$v" = "42" ] || { echo "FAIL: expected STAT__CNT=42 got '$v'"; exit 1; }

echo "== metrics_now path produced samples =="
n=$(curl -fs -G --data-urlencode 'match[]=NOWONLY__CNT{job="dotnet-metrics-now"}' "$PROM/api/v1/series" | grep -c NOWONLY || true)
[ "$n" -ge 1 ] || { echo "FAIL: no NOWONLY samples from /metrics_now job"; exit 1; }

echo "ALL E2E ASSERTIONS PASSED"
