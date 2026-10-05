#!/usr/bin/env python3
"""Seed the shared SQLite metrics database used by the e2e/parity harness."""
import sqlite3
import sys

db = sys.argv[1] if len(sys.argv) > 1 else "/db/metrics.db"
conn = sqlite3.connect(db)
conn.executescript("""
CREATE TABLE IF NOT EXISTS stats(NAME TEXT, CNT INTEGER, RATE REAL, NOTE TEXT);
DELETE FROM stats;
INSERT INTO stats VALUES('main', 42, 1.5, 'hello');
INSERT INTO stats VALUES('two', 99, 9.9, 'ignored');
CREATE TABLE IF NOT EXISTS pools(POOL TEXT, SIZE INTEGER);
DELETE FROM pools;
INSERT INTO pools VALUES('QWORK', 100);
INSERT INTO pools VALUES('QINTER', 200);
""")
conn.commit()
print(f"seeded {db}")
