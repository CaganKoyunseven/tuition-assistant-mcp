#!/usr/bin/env bash
# Generates the RS256 key pair used to sign (Tuition API) and verify (Kong, Agent) JWTs.
# Output: secrets/jwt_private.pem and secrets/jwt_public.pem (git-ignored), plus single-line
# values with "\n" escapes that can be pasted into .env.
set -euo pipefail

cd "$(dirname "$0")/.."
mkdir -p secrets

if [[ -f secrets/jwt_private.pem && "${1:-}" != "--force" ]]; then
  echo "secrets/jwt_private.pem already exists (use --force to overwrite)." >&2
else
  openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out secrets/jwt_private.pem 2>/dev/null
  openssl rsa -in secrets/jwt_private.pem -pubout -out secrets/jwt_public.pem 2>/dev/null
  chmod 600 secrets/jwt_private.pem
  echo "Created secrets/jwt_private.pem and secrets/jwt_public.pem" >&2
fi

one_line() { awk 'NR > 1 { printf "\\n" } { printf "%s", $0 }' "$1"; }

echo
echo "# Paste into .env:"
echo "JWT_PRIVATE_KEY_PEM=$(one_line secrets/jwt_private.pem)"
echo "JWT_PUBLIC_KEY_PEM=$(one_line secrets/jwt_public.pem)"
