#!/bin/sh
# Java <-> .NET parity gate (plan section L4):
# runs both exporters against the same SQLite DB and diffs their /metrics
# output after label normalization. Blocks releases.
#
# Known, documented deviation: the Java single-row populator unregisters its
# gauges on a failed gather and never re-registers them (early-return on an
# empty gauge map); the .NET collector re-registers on the next successful
# gather. The "after recovery" round therefore compares everything except the
# Java-lost families and asserts .NET recovered them explicitly.
#
# Usage: tests/e2e/parity-check.sh   (from repo root; requires docker)
set -eu
cd "$(dirname "$0")/../.."
E2E=tests/e2e
FAIL=0

dbexec() {
  docker run --rm -v "$PWD/$E2E/db:/db" python:3.12-alpine python -c "$1"
}

# driver_class legitimately differs between the two implementations, and the
# Java simpleclient has legacy serialization quirks (trailing comma in label
# sets, integral values rendered as floats). Families, label sets, and numeric
# values must still match exactly.
normalize() {
  sed -e 's/driver_class="[^"]*"/driver_class="X"/g' \
      -e 's/,}/}/g' \
      -e 's/ \([0-9][0-9]*\)\.0$/ \1/' \
      | sed '/^# HELP/!{/^#/d}' | sort
}

diffdump() {
  cat /tmp/parity-status.log
  echo "--- java doc:"; cat /tmp/parity-java.txt
  echo "--- dotnet doc:"; cat /tmp/parity-dotnet.txt
  echo "--- dotnet app log tail:"
  docker compose -f $E2E/docker-compose.parity.yml logs --tail 30 dotnet-exporter 2>&1 | tail -30
}

# fetch URL outfile : writes body, records "URL code size time" in status log
fetch() {
  code=$(curl -s -o "$2" -w "%{http_code} %{size_download} %{time_total}" "$1")
  echo "$1 -> $code" >> /tmp/parity-status.log
}

round() {
  label=$1
  path=${2:-metrics}
  filt=${3:-}
  : > /tmp/parity-status.log
  fetch "http://127.0.0.1:19853/$path" /tmp/parity-java.raw
  fetch "http://127.0.0.1:19854/$path" /tmp/parity-dotnet.raw
  normalize < /tmp/parity-java.raw > /tmp/parity-java.txt
  normalize < /tmp/parity-dotnet.raw > /tmp/parity-dotnet.txt
  if [ -n "$filt" ]; then
    grep -v "$filt" /tmp/parity-java.txt > /tmp/parity-java-f.txt
    grep -v "$filt" /tmp/parity-dotnet.txt > /tmp/parity-dotnet-f.txt
    mv /tmp/parity-java-f.txt /tmp/parity-java.txt
    mv /tmp/parity-dotnet-f.txt /tmp/parity-dotnet.txt
  fi
  if diff -u /tmp/parity-java.txt /tmp/parity-dotnet.txt > /tmp/parity.diff; then
    echo "PASS [$label]"
  else
    echo "FAIL [$label] diff:"
    cat /tmp/parity.diff
    diffdump
    FAIL=1
  fi
}

cleanup() { docker compose -f $E2E/docker-compose.parity.yml down -v >/dev/null 2>&1 || true; }
trap cleanup EXIT
cleanup

mkdir -p $E2E/db
"$E2E/java-sqlite-image/fetch-drivers.sh"
docker compose -f $E2E/docker-compose.parity.yml up --build seed
docker compose -f $E2E/docker-compose.parity.yml up -d --build java-exporter dotnet-exporter
sleep 8

round "initial /metrics"

dbexec "import sqlite3; c=sqlite3.connect('/db/metrics.db'); c.execute('UPDATE stats SET CNT=77'); c.commit()"
sleep 6
round "after DB update /metrics"

sleep 6   # exceed the 5s gatherNow tolerance so the point-in-time query refreshes on both
round "after tolerance metrics_now" metrics_now

# error-eviction parity: drop stats -> both must drop the interval family;
# the on-demand family holds its stale last-known value on both.
dbexec "import sqlite3; c=sqlite3.connect('/db/metrics.db'); c.execute('DROP TABLE stats'); c.commit()"
sleep 6
round "after table drop /metrics"

# Recovery: .NET re-registers the interval family; Java does not (documented
# deviation) -> compare non-STAT families, then assert .NET recovered STAT.
dbexec "import sqlite3
c = sqlite3.connect('/db/metrics.db')
c.executescript(\"\"\"
CREATE TABLE stats(NAME TEXT, CNT INTEGER, RATE REAL, NOTE TEXT);
INSERT INTO stats VALUES('main', 42, 1.5, 'hello');
\"\"\")
c.commit()"
sleep 6
round "after recovery /metrics (excluding known Java non-recovery)" "" "STAT__"
curl -fs "http://127.0.0.1:19854/metrics" | grep -q 'STAT__CNT{[^}]*} 42' \
  && echo "PASS [.NET recovered STAT]" \
  || { echo "FAIL [.NET recovered STAT]"; FAIL=1; }

[ $FAIL -eq 0 ] && echo "PARITY OK" || { echo "PARITY FAILED"; exit 1; }
