#!/usr/bin/env bash
set -euo pipefail
BASE="http://localhost:8080"
EMAIL="smoke_$(date +%s)@test.com"

echo "== Register =="
REG=$(curl -s -X POST "$BASE/api/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"fullName\":\"Smoke Test\",\"email\":\"$EMAIL\",\"password\":\"secret123\",\"phoneNumber\":\"0900000000\"}")
echo "$REG"
echo "$REG" | grep -q '"token"' || { echo "FAIL: no token on register"; exit 1; }

echo "== Login =="
LOGIN=$(curl -s -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"secret123\"}")
echo "$LOGIN"
TOKEN=$(echo "$LOGIN" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
[ -n "$TOKEN" ] || { echo "FAIL: no token on login"; exit 1; }

echo "== GetMe (qua gateway, JWT) =="
ME=$(curl -s "$BASE/api/auth/me" -H "Authorization: Bearer $TOKEN")
echo "$ME"
echo "$ME" | grep -q "$EMAIL" || { echo "FAIL: /me không trả đúng user"; exit 1; }

echo "ALL AUTH SMOKE TESTS PASSED"
