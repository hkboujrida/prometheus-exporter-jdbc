#!/bin/sh
# Fetches the reference JDBC driver jars used by the parity harness.
set -eu
cd "$(dirname "$0")"
base=https://repo1.maven.org/maven2
[ -f sqlite-jdbc.jar ] || curl -fsSL -o sqlite-jdbc.jar "$base/org/xerial/sqlite-jdbc/3.45.3.0/sqlite-jdbc-3.45.3.0.jar"
[ -f slf4j-api.jar ]   || curl -fsSL -o slf4j-api.jar   "$base/org/slf4j/slf4j-api/2.0.13/slf4j-api-2.0.13.jar"
[ -f slf4j-nop.jar ]   || curl -fsSL -o slf4j-nop.jar   "$base/org/slf4j/slf4j-nop/2.0.13/slf4j-nop-2.0.13.jar"
