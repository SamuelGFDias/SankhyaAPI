#!/usr/bin/env bash
# Gate local: build e testes fake nos dois SDKs (net8.0 no SDK 8, net10.0 no SDK 10), via podman quando não
# houver dotnet. Os testes reais ficam "skipped" sem credenciais (rode scripts/test-real.sh para eles).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
run() { # imagem, framework, propriedades extras
  podman run --rm -v "$ROOT":/src:Z -w /src -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 "$1" \
    bash -c "dotnet test -c Release -f $2 $3 -p:SANKHYA_SEM_CREDENCIAIS=1 2>&1 | grep -E 'warning|error|Failed |Passed!|Failed!|Assert|Expected|Actual|Exception :' | sort -u"
}
run mcr.microsoft.com/dotnet/sdk:10.0 net10.0 ""
run mcr.microsoft.com/dotnet/sdk:8.0 net8.0 "-p:SankhyaSoNet8=true"
