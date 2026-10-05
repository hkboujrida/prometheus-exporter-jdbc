#!/bin/sh
# E2E gate: exporters + real Prometheus + assert one-shot, with reliable
# teardown. Usage: tests/e2e/run-e2e.sh
set -eu
cd "$(dirname "$0")/../.."
E2E=tests/e2e
RC=0

"$E2E/java-sqlite-image/fetch-drivers.sh"
mkdir -p $E2E/db
docker compose -f $E2E/docker-compose.test.yml build
docker compose -f $E2E/docker-compose.test.yml up -d prometheus
docker compose -f $E2E/docker-compose.test.yml run --rm --no-deps assert \
  || RC=$?
docker compose -f $E2E/docker-compose.test.yml down -v >/dev/null 2>&1 || true
[ $RC -eq 0 ] && echo "E2E OK" || { echo "E2E FAILED"; exit $RC; }
