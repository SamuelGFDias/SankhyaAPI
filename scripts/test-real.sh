#!/usr/bin/env bash
# Roda os testes contra o Sankhya real (padrão: Category=Real, só leitura; a escrita na AD_TESTE é
# Category=RealEscrita, e "Category=Real|Category=RealEscrita" roda os dois), lendo URL, usuário e senha do arquivo de
# credenciais (padrão: ~/.config/sankhyaapi/teste.env, permissão 600). Nada é impresso.
# Usa o dotnet local; sem ele, roda num container do SDK .NET 10 com podman.
set -euo pipefail

CRED="${SANKHYA_CREDENTIALS_FILE:-$HOME/.config/sankhyaapi/teste.env}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FILTER="${1:-Category=Real}"

if [[ ! -f "$CRED" ]]; then
  echo "Arquivo de credenciais não encontrado: $CRED" >&2
  exit 1
fi

if [[ "$(stat -c '%a' "$CRED")" != "600" ]]; then
  echo "Aviso: $CRED não está com permissão 600 (chmod 600 \"$CRED\")." >&2
fi

if command -v dotnet >/dev/null 2>&1; then
  SANKHYA_CREDENTIALS_FILE="$CRED" dotnet test "$ROOT" -f net10.0 --filter "$FILTER" --logger "console;verbosity=normal"
else
  podman run --rm \
    -v "$ROOT":/src:Z -w /src \
    -v "$CRED":/run/sankhya.env:ro,Z \
    -e SANKHYA_CREDENTIALS_FILE=/run/sankhya.env \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    dotnet test -f net10.0 --filter "$FILTER" --logger "console;verbosity=normal"
fi
