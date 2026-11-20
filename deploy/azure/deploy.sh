#!/usr/bin/env bash
# Deploys the whole system to Azure Container Apps. Safe to re-run (idempotent).
#
#   az login                                   # account that owns the target subscription
#   cp deploy/azure/config.env.example deploy/azure/config.env   # optional; created automatically
#   ./deploy/azure/deploy.sh
#
# Creates: resource group, container registry, PostgreSQL flexible server (B1ms), Container Apps
# environment and four container apps (kong = public gateway, mcp-server = public MCP endpoint,
# tuition-api and agent = internal only). Then it runs scripts/smoke-test.sh against the gateway.
set -euo pipefail

cd "$(dirname "$0")/../.."
CONFIG=deploy/azure/config.env
EXAMPLE=deploy/azure/config.env.example

log() { printf '\n==> %s\n' "$*"; }
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

command -v az >/dev/null || die "Azure CLI not found. Install it first (https://aka.ms/InstallAzureCLIDeb)."
az account show >/dev/null 2>&1 || die "Not logged in. Run: az login"

[[ -f $CONFIG ]] || { cp "$EXAMPLE" "$CONFIG"; chmod 600 "$CONFIG"; echo "Created $CONFIG from the example."; }
chmod 600 "$CONFIG"
set -a; . "$CONFIG"; set +a

# Writes KEY='value' into config.env (replace or append). Values must not contain a single quote.
persist() {
  local key=$1 value=$2
  if grep -q "^${key}=" "$CONFIG"; then
    local tmp; tmp=$(mktemp)
    awk -v k="$key" -v v="$value" 'index($0, k "=") == 1 { print k "=\047" v "\047"; next } { print }' "$CONFIG" > "$tmp"
    cat "$tmp" > "$CONFIG"; rm -f "$tmp"
  else
    printf "%s='%s'\n" "$key" "$value" >> "$CONFIG"
  fi
}

[[ -n ${AZ_SUFFIX:-} ]] || { AZ_SUFFIX=$(openssl rand -hex 3); persist AZ_SUFFIX "$AZ_SUFFIX"; }
[[ -n ${PG_ADMIN_PASSWORD:-} ]] || { PG_ADMIN_PASSWORD=$(openssl rand -hex 16); persist PG_ADMIN_PASSWORD "$PG_ADMIN_PASSWORD"; }
[[ -n ${MCP_API_KEY:-} ]] || { MCP_API_KEY=$(openssl rand -hex 24); persist MCP_API_KEY "$MCP_API_KEY"; }
if [[ -z ${OPENAI_API_KEY:-} ]]; then
  [[ -t 0 ]] || die "OPENAI_API_KEY is empty and there is no interactive terminal. Run this script in a normal terminal, or set OPENAI_API_KEY in $CONFIG."
  read -r -s -p "OpenAI API key (input hidden, stored only in $CONFIG): " OPENAI_API_KEY; echo
  [[ -n $OPENAI_API_KEY ]] || die "An OpenAI API key is required."
  persist OPENAI_API_KEY "$OPENAI_API_KEY"
fi

RG=$AZ_RESOURCE_GROUP
LOC=$AZ_LOCATION
ACR="acrse4458${AZ_SUFFIX}"
PG="${PG_SERVER_NAME:-pg-se4458-${AZ_SUFFIX}}"
PG_LOC="${AZ_PG_LOCATION:-$LOC}"   # PostgreSQL may live in another region than the apps
ENV_NAME="cae-se4458"
TAG=$(git rev-parse --short HEAD)

log "Subscription"
az account show --query "{name:name, state:state}" -o tsv
az extension add --name containerapp --upgrade --only-show-errors -y
for ns in Microsoft.App Microsoft.OperationalInsights Microsoft.DBforPostgreSQL Microsoft.ContainerRegistry; do
  az provider register --namespace "$ns" --wait -o none
done

log "Checking that PostgreSQL can be provisioned in $PG_LOC"
SUB_ID=$(az account show --query id -o tsv)
PG_CAPS_URL="https://management.azure.com/subscriptions/$SUB_ID/providers/Microsoft.DBforPostgreSQL/locations/$PG_LOC/capabilities?api-version=2024-08-01"
PG_RESTRICTED=$(az rest --method get --url "$PG_CAPS_URL" --query "value[0].restricted" -o tsv 2>/dev/null || true)
if [[ $PG_RESTRICTED == Enabled ]]; then
  die "PostgreSQL provisioning is restricted in $PG_LOC for this subscription. Set AZ_PG_LOCATION in $CONFIG to another allowed region."
fi

log "Resource group and container registry"
az group create -n "$RG" -l "$LOC" -o none
az acr create -g "$RG" -n "$ACR" --sku Basic --admin-enabled true -o none
ACR_SERVER=$(az acr show -n "$ACR" --query loginServer -o tsv)
ACR_USER=$(az acr credential show -n "$ACR" --query username -o tsv)
ACR_PASS=$(az acr credential show -n "$ACR" --query 'passwords[0].value' -o tsv)

# name:dockerfile pairs; the build context is always the repository root.
IMAGES=(
  "tuition-api:src/TuitionAssistant.Api/Dockerfile"
  "mcp-server:src/TuitionAssistant.Mcp/Dockerfile"
  "agent:src/TuitionAssistant.Agent/Dockerfile"
  "kong:gateway/kong/Dockerfile"
)

log "Building images (tag $TAG)"
LOCAL_BUILD=0
for item in "${IMAGES[@]}"; do
  name=${item%%:*}; dockerfile=${item#*:}
  if [[ $LOCAL_BUILD -eq 0 ]]; then
    if az acr build -r "$ACR" -t "$name:$TAG" -t "$name:latest" -f "$dockerfile" . --no-logs -o none 2>"$(dirname "$0")/.acr-build.err"; then
      echo "  built $name in ACR"; continue
    fi
    echo "  ACR Tasks are not available on this subscription; falling back to local docker build + push."
    LOCAL_BUILD=1
    az acr login -n "$ACR" -o none
  fi
  docker build -f "$dockerfile" -t "$ACR_SERVER/$name:$TAG" -t "$ACR_SERVER/$name:latest" .
  docker push "$ACR_SERVER/$name:$TAG"
  docker push "$ACR_SERVER/$name:latest"
  echo "  built and pushed $name locally"
done
rm -f "$(dirname "$0")/.acr-build.err"

log "PostgreSQL flexible server (B1ms, v17) - this takes a few minutes"
if ! az postgres flexible-server show -g "$RG" -n "$PG" >/dev/null 2>&1; then
  az postgres flexible-server create -g "$RG" -n "$PG" -l "$PG_LOC" \
    --admin-user "$PG_ADMIN_USER" --admin-password "$PG_ADMIN_PASSWORD" \
    --sku-name Standard_B1ms --tier Burstable --version 17 --storage-size 32 \
    --public-access 0.0.0.0 --yes -o none
fi
az postgres flexible-server db create -g "$RG" -s "$PG" -d tuition -o none 2>/dev/null || true
PG_HOST="$PG.postgres.database.azure.com"
CONN="Host=$PG_HOST;Port=5432;Database=tuition;Username=$PG_ADMIN_USER;Password=$PG_ADMIN_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"

log "Container Apps environment (Consumption; the Express preview mode lacks internal app-to-app routing)"
az containerapp env show -g "$RG" -n "$ENV_NAME" >/dev/null 2>&1 \
  || az containerapp env create -g "$RG" -n "$ENV_NAME" -l "$LOC" --environment-mode ConsumptionOnly --logs-destination none -o none
DOMAIN=$(az containerapp env show -g "$RG" -n "$ENV_NAME" --query properties.defaultDomain -o tsv)

[[ -f secrets/jwt_private.pem ]] || ./scripts/gen-keys.sh >/dev/null
one_line() { awk 'NR > 1 { printf "\\n" } { printf "%s", $0 }' "$1"; }
JWT_PRIVATE=$(one_line secrets/jwt_private.pem)
JWT_PUBLIC=$(one_line secrets/jwt_public.pem)

# deploy_app NAME IMAGE INGRESS(internal|external) PORT CPU MEMORY  (uses the global SECRETS and ENVS arrays)
deploy_app() {
  local name=$1 image=$ACR_SERVER/$2:$TAG ingress=$3 port=$4 cpu=$5 mem=$6
  if az containerapp show -g "$RG" -n "$name" >/dev/null 2>&1; then
    az containerapp secret set -g "$RG" -n "$name" --secrets "${SECRETS[@]}" -o none
    az containerapp update -g "$RG" -n "$name" --image "$image" --replace-env-vars "${ENVS[@]}" -o none
  else
    az containerapp create -g "$RG" -n "$name" --environment "$ENV_NAME" --image "$image" \
      --registry-server "$ACR_SERVER" --registry-username "$ACR_USER" --registry-password "$ACR_PASS" \
      --ingress "$ingress" --target-port "$port" \
      --cpu "$cpu" --memory "$mem" --min-replicas 1 --max-replicas 1 \
      --secrets "${SECRETS[@]}" --env-vars "${ENVS[@]}" -o none
  fi
}

log "Deploying tuition-api (internal)"
SECRETS=("db-conn=$CONN" "jwt-private=$JWT_PRIVATE" "jwt-public=$JWT_PUBLIC")
ENVS=(
  "ConnectionStrings__Tuition=secretref:db-conn"
  "Jwt__PrivateKeyPem=secretref:jwt-private"
  "Jwt__PublicKeyPem=secretref:jwt-public"
  "Database__MigrateOnStartup=true"
  "Database__SeedOnStartup=true"
  "Seed__AdminPassword=$SEED_ADMIN_PASSWORD"
  "Seed__StudentPassword=$SEED_STUDENT_PASSWORD"
  "Seed__BankClientSecret=$SEED_BANK_CLIENT_SECRET"
)
deploy_app tuition-api tuition-api internal 8080 0.5 1.0Gi

log "Deploying mcp-server (public, API key protected)"
SECRETS=("mcp-key=$MCP_API_KEY")
ENVS=("Mcp__ApiKey=secretref:mcp-key")
deploy_app mcp-server mcp-server external 8080 0.5 1.0Gi

log "Deploying agent (internal)"
SECRETS=("mcp-key=$MCP_API_KEY" "jwt-public=$JWT_PUBLIC" "llm-key=$OPENAI_API_KEY")
ENVS=(
  "Llm__Provider=OpenAI"
  "Llm__Model=$LLM_MODEL"
  "Llm__AllowedModels=${LLM_ALLOWED_MODELS:-}"
  "Llm__BaseUrl=https://api.openai.com/v1"
  "Llm__ApiKey=secretref:llm-key"
  "Gateway__BaseUrl=https://kong.$DOMAIN"
  "Mcp__Url=https://mcp-server.$DOMAIN/mcp"
  "Mcp__ApiKey=secretref:mcp-key"
  "Jwt__PublicKeyPem=secretref:jwt-public"
)
deploy_app agent agent internal 8080 0.5 1.0Gi

log "Deploying kong (public gateway)"
SECRETS=("jwt-public=$JWT_PUBLIC")
ENVS=(
  "TUITION_API_UPSTREAM=http://tuition-api"
  "AGENT_UPSTREAM=http://agent"
  "JWT_PUBLIC_KEY_PEM=secretref:jwt-public"
)
deploy_app kong kong external 8000 0.5 1.0Gi

# Internal calls use plain http://<app-name>; without this Container Apps redirects them to https.
for app in tuition-api agent; do
  az containerapp ingress update -g "$RG" -n "$app" --allow-insecure true -o none
done

GATEWAY_URL="https://$(az containerapp show -g "$RG" -n kong --query properties.configuration.ingress.fqdn -o tsv)"
MCP_URL="https://$(az containerapp show -g "$RG" -n mcp-server --query properties.configuration.ingress.fqdn -o tsv)/mcp"

log "Waiting for the gateway"
for _ in $(seq 1 60); do
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$GATEWAY_URL/" || true)
  [[ $code == 200 ]] && break
  sleep 10
done
[[ $code == 200 ]] || die "Gateway did not become healthy (last status $code). Logs are disabled to save credit; inspect with: az containerapp revision list -g $RG -n kong -o table"

mkdir -p documentation/evidence
{
  echo "Deployed: $(date -u +%Y-%m-%dT%H:%MZ)  commit $TAG  region $LOC"
  echo "Gateway (chat UI):  $GATEWAY_URL/"
  echo "Swagger UI:         $GATEWAY_URL/swagger"
  echo "MCP endpoint:       $MCP_URL   (header X-Mcp-Api-Key)"
  echo "Resource group:     $RG"
} | tee documentation/evidence/azure-urls.txt

log "Smoke test against Azure"
SEED_ADMIN_PASSWORD=$SEED_ADMIN_PASSWORD SEED_STUDENT_PASSWORD=$SEED_STUDENT_PASSWORD \
SEED_BANK_CLIENT_SECRET=$SEED_BANK_CLIENT_SECRET \
  ./scripts/smoke-test.sh "$GATEWAY_URL" | tee documentation/evidence/smoke-test-azure.txt
