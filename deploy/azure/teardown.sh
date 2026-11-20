#!/usr/bin/env bash
# Deletes the whole resource group (all Azure resources of this project). Asks for confirmation.
set -euo pipefail

cd "$(dirname "$0")/../.."
CONFIG=deploy/azure/config.env
[[ -f $CONFIG ]] || { echo "No $CONFIG found; nothing to tear down." >&2; exit 1; }
set -a; . "$CONFIG"; set +a

read -r -p "Delete resource group '$AZ_RESOURCE_GROUP' and EVERYTHING in it? Type the name to confirm: " answer
[[ $answer == "$AZ_RESOURCE_GROUP" ]] || { echo "Aborted."; exit 1; }

az group delete -n "$AZ_RESOURCE_GROUP" --yes --no-wait
echo "Deletion started (runs in the background, a few minutes)."
