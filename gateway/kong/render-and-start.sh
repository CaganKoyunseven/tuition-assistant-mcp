#!/bin/sh
# Renders /kong/kong.yml from kong.yml.template using environment variables, validates it,
# then starts Kong in DB-less mode. The same image works locally (docker compose) and in Azure.
set -eu

TEMPLATE=/kong/kong.yml.template
OUTPUT=/kong/kong.yml

for var in TUITION_API_UPSTREAM AGENT_UPSTREAM JWT_PUBLIC_KEY_PEM; do
  eval "value=\${$var:-}"
  if [ -z "$value" ]; then
    echo "render-and-start: environment variable $var is required" >&2
    exit 1
  fi
done

# The PEM may arrive with real newlines (Azure secret) or with literal "\n" escapes (.env).
# Normalise to one line with "\n" escapes: inside a double-quoted YAML string these become newlines.
JWT_PUBLIC_KEY_PEM_ESCAPED=$(printf '%s\n' "$JWT_PUBLIC_KEY_PEM" | tr -d '\r' \
  | awk 'NR > 1 { printf "\\n" } { printf "%s", $0 }')
export JWT_PUBLIC_KEY_PEM_ESCAPED

# Plain string replacement (no regex), so characters such as "/", "+" and "\" in values are safe.
awk '
  function replace_all(text, key, value,    out, pos) {
    out = ""
    while ((pos = index(text, key)) > 0) {
      out = out substr(text, 1, pos - 1) value
      text = substr(text, pos + length(key))
    }
    return out text
  }
  {
    line = $0
    line = replace_all(line, "${TUITION_API_UPSTREAM}", ENVIRON["TUITION_API_UPSTREAM"])
    line = replace_all(line, "${AGENT_UPSTREAM}", ENVIRON["AGENT_UPSTREAM"])
    line = replace_all(line, "${JWT_PUBLIC_KEY_PEM}", ENVIRON["JWT_PUBLIC_KEY_PEM_ESCAPED"])
    print line
  }
' "$TEMPLATE" > "$OUTPUT"

# Fail fast on an invalid configuration instead of starting a broken gateway.
kong config parse "$OUTPUT"

exec /docker-entrypoint.sh kong docker-start
