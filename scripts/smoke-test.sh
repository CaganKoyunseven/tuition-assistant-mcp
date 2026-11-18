#!/usr/bin/env bash
# Gateway smoke test: sends real requests through Kong and prints a PASS/FAIL table.
#
#   ./scripts/smoke-test.sh [gatewayUrl]          (default http://localhost:8000)
#   ./scripts/smoke-test.sh http://localhost:8000 | tee documentation/evidence/smoke-test-local.txt
#
# Requires bash and curl only. Exit code 0 = all checks passed.
#
# The mobile rate-limit check consumes that day's quota (3 per student per UTC day) of
# MOBILE_STUDENT and one query of MOBILE_OTHER_STUDENT. To re-run on the same day, pick other
# students (e.g. MOBILE_STUDENT=20239010 MOBILE_OTHER_STUDENT=20239011) or reset the stack with
# "docker compose down -v". The demo student 20230001 is never used for the rate-limit check.
set -uo pipefail
# Locale-independent matching (e.g. in tr_TR "grep -i" does not match "i" with "I").
export LC_ALL=C

BASE="${1:-http://localhost:8000}"
BASE="${BASE%/}"
MOBILE_STUDENT="${MOBILE_STUDENT:-20230004}"
MOBILE_OTHER_STUDENT="${MOBILE_OTHER_STUDENT:-20239001}"
PAYMENT_STUDENT="${PAYMENT_STUDENT:-20239002}"
TERM="${TERM_CODE:-2026-FALL}"
STUDENT_PASSWORD="${SEED_STUDENT_PASSWORD:-Student#2026}"
ADMIN_PASSWORD="${SEED_ADMIN_PASSWORD:-Admin#2026}"
BANK_SECRET="${SEED_BANK_CLIENT_SECRET:-BankSecret#2026}"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

PASSED=0
FAILED=0
ROWS=()

STATUS=""
BODY=""

# request METHOD PATH [TOKEN] [JSON_BODY]  -> sets STATUS, BODY; headers in $TMP/headers
request() {
  local method="$1" path="$2" token="${3:-}" body="${4:-}"
  local args=(-s -o "$TMP/body" -D "$TMP/headers" -w '%{http_code}' -X "$method" --max-time 30)
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n "$body" ]] && args+=(-H "Content-Type: application/json" --data "$body")
  STATUS="$(curl "${args[@]}" "$BASE$path" 2>/dev/null)" || STATUS="000"
  BODY="$(cat "$TMP/body" 2>/dev/null)"
}

# check NAME EXPECTED_STATUS [BODY_SUBSTRING]
check() {
  local name="$1" expected="$2" needle="${3:-}" result="PASS" detail="$STATUS"
  if [[ "$STATUS" != "$expected" ]]; then
    result="FAIL"
  elif [[ -n "$needle" && "$BODY" != *"$needle"* ]]; then
    result="FAIL"
    detail="$STATUS, body lacks '$needle'"
  fi
  if [[ "$result" == "PASS" ]]; then PASSED=$((PASSED + 1)); else FAILED=$((FAILED + 1)); fi
  ROWS+=("$(printf '%-4s | %-58s | %-8s | %s' "$result" "$name" "$expected" "$detail")")
}

token_of() { sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p' <<<"$BODY"; }

echo "Smoke test against $BASE ($(date -u '+%Y-%m-%d %H:%M:%S UTC'))"
echo

# --- Authentication -------------------------------------------------------------------------
request POST /api/v1/auth/token "" "{\"grantType\":\"password\",\"username\":\"20230001\",\"password\":\"$STUDENT_PASSWORD\"}"
check "Login student 20230001" 200 accessToken
STUDENT_TOKEN="$(token_of)"

request POST /api/v1/auth/token "" "{\"grantType\":\"client_credentials\",\"clientId\":\"bank-app\",\"clientSecret\":\"$BANK_SECRET\"}"
check "Login banking app (client credentials)" 200 accessToken
BANK_TOKEN="$(token_of)"

request POST /api/v1/auth/token "" "{\"grantType\":\"password\",\"username\":\"admin\",\"password\":\"$ADMIN_PASSWORD\"}"
check "Login admin" 200 accessToken
ADMIN_TOKEN="$(token_of)"

request POST /api/v1/auth/token "" '{"grantType":"password","username":"20230001","password":"wrong-password"}'
check "Login with wrong password" 401 INVALID_CREDENTIALS

# --- Mobile query: 3 per student per day, keyed by student number --------------------------
for i in 1 2 3; do
  request GET "/api/v1/mobile/tuition/$MOBILE_STUDENT"
  check "Mobile query $MOBILE_STUDENT #$i (no auth)" 200 "$MOBILE_STUDENT"
done
request GET "/api/v1/mobile/tuition/$MOBILE_STUDENT"
# The API has its own persistent daily counter too; the X-RateLimit-*-Day headers prove that
# this 429 was produced by Kong.
if grep -qi '^x-ratelimit-remaining-day: 0' "$TMP/headers"; then BODY="$BODY [kong-limit-headers]"; fi
check "Mobile query $MOBILE_STUDENT #4 -> 429 from Kong rate limit" 429 "[kong-limit-headers]"
request GET "/api/v1/mobile/tuition/$MOBILE_OTHER_STUDENT"
check "Mobile query other student $MOBILE_OTHER_STUDENT (same IP)" 200 "$MOBILE_OTHER_STUDENT"

# --- Banking query: JWT at the gateway, ownership in the API --------------------------------
request GET /api/v1/banking/tuition/20230001
check "Banking query without token" 401 Unauthorized
request GET /api/v1/banking/tuition/20230001 "not-a-jwt"
check "Banking query with invalid token" 401
request GET /api/v1/banking/tuition/20230001 "$STUDENT_TOKEN"
check "Banking query own record (student token)" 200 '"studentNo":"20230001"'
request GET /api/v1/banking/tuition/20230002 "$STUDENT_TOKEN"
check "Banking query other student (student token)" 403 FORBIDDEN_NOT_OWNER
request GET "/api/v1/banking/tuition/20230001?term=$TERM" "$BANK_TOKEN"
check "Banking query by term (bank token)" 200 "$TERM"

# --- Pay tuition ----------------------------------------------------------------------------
TXN="SMOKE-$(date -u +%Y%m%d%H%M%S)-$RANDOM"
PAYMENT="{\"studentNo\":\"$PAYMENT_STUDENT\",\"term\":\"$TERM\",\"amount\":1.00,\"transactionId\":\"$TXN\"}"
request POST /api/v1/banking/payments "$BANK_TOKEN" "$PAYMENT"
check "Pay tuition 1.00 for $PAYMENT_STUDENT (bank token)" 201 SUCCESS
request POST /api/v1/banking/payments "$BANK_TOKEN" "$PAYMENT"
check "Pay tuition again with same transactionId" 409 DUPLICATE_TRANSACTION
request POST /api/v1/banking/payments "$BANK_TOKEN" \
  "{\"studentNo\":\"$PAYMENT_STUDENT\",\"term\":\"$TERM\",\"amount\":999999.00,\"transactionId\":\"$TXN-BIG\"}"
check "Pay more than the balance (ProblemDetails via Kong)" 422 AMOUNT_EXCEEDS_BALANCE
request POST /api/v1/banking/payments "" "$PAYMENT"
check "Pay tuition without token" 401

# --- Admin ----------------------------------------------------------------------------------
request GET "/api/v1/admin/tuition/unpaid?term=$TERM&page=1&pageSize=5" "$ADMIN_TOKEN"
check "Unpaid tuition report (admin token)" 200 items
request GET "/api/v1/admin/tuition/unpaid?term=$TERM" "$STUDENT_TOKEN"
check "Unpaid tuition report with student token" 403 FORBIDDEN

# --- Gateway behaviour ----------------------------------------------------------------------
request GET /swagger/v1/swagger.json
check "Swagger/OpenAPI document via gateway" 200 openapi
if grep -qi '^x-request-id:' "$TMP/headers"; then STATUS="present"; else STATUS="missing"; fi
check "Correlation header X-Request-ID on responses" present
request GET /api/v1/does-not-exist
check "Unknown route" 404

# --- Report ---------------------------------------------------------------------------------
printf '%-4s | %-58s | %-8s | %s\n' "RES" "CHECK" "EXPECTED" "ACTUAL"
printf '%s\n' "-----+------------------------------------------------------------+----------+----------------"
printf '%s\n' "${ROWS[@]}"
echo
echo "Passed: $PASSED  Failed: $FAILED"
[[ "$FAILED" -eq 0 ]]
