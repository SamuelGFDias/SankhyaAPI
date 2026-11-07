#!/usr/bin/env bash
# Sonda o Sankhya real para conferir o protocolo (ver docs/sankhya-protocolo.md).
# Lê URL, usuário e senha do arquivo de credenciais (padrão: ~/.config/sankhyaapi/teste.env) e nunca
# imprime credenciais nem o jsessionid. Abre uma sessão, faz a chamada e encerra a sessão no fim.
#
#   sondar-sankhya.sh sql "SELECT ..."                  executeQuery em JSON (resposta formatada)
#   sondar-sankhya.sh xml <Servico> [content-type]      corpo XML lido da entrada padrão
#   sondar-sankhya.sh json <Servico>                    corpo JSON lido da entrada padrão (outputType=json)
#   sondar-sankhya.sh morta-sql "SELECT ..."            executeQuery com uma sessão já encerrada
#
# Escrita só na tabela AD_TESTE da homologação.
set -euo pipefail

CRED="${SANKHYA_CREDENTIALS_FILE:-$HOME/.config/sankhyaapi/teste.env}"
[[ -f "$CRED" ]] || { echo "Arquivo de credenciais não encontrado: $CRED" >&2; exit 1; }
valor() { grep "^$1=" "$CRED" | cut -d= -f2- | tr -d '\n'; }
BASE="$(valor SANKHYA_BASE_URL)"

login() {
  { printf '<serviceRequest serviceName="MobileLoginSP.login"><requestBody><NOMUSU>'
    valor SANKHYA_USER
    printf '</NOMUSU><INTERNO>'
    valor SANKHYA_PASSWORD
    printf '</INTERNO></requestBody></serviceRequest>'; } |
    curl -s -X POST -H 'Content-Type: text/xml; charset=UTF-8' --data-binary @- \
      "$BASE/service.sbr?serviceName=MobileLoginSP.login" |
    sed -n 's:.*<jsessionid>\([^<]*\)</jsessionid>.*:\1:p'
}

logout() {
  curl -s -o /dev/null -X POST -H "Cookie: JSESSIONID=$1" "$BASE/service.sbr?serviceName=MobileLoginSP.logout"
}

sql() {
  python3 -c 'import json,sys; print(json.dumps({"serviceName":"DbExplorerSP.executeQuery","requestBody":{"sql":sys.argv[1]}}))' "$2" |
    curl -s -X POST -H "Cookie: JSESSIONID=$1" -H 'Content-Type: application/json; charset=UTF-8' --data-binary @- \
      "$BASE/service.sbr?serviceName=DbExplorerSP.executeQuery&outputType=json" |
    python3 -c '
import json, sys
d = json.load(sys.stdin)
rb = d.get("responseBody") or {}
print("status", d.get("status"), "| burstLimit", rb.get("burstLimit"), "|", d.get("statusMessage") or "")
if rb.get("fieldsMetadata"):
    print([f["name"] for f in rb["fieldsMetadata"]])
    for r in rb.get("rows", [])[:50]: print(r)
    if len(rb.get("rows", [])) > 50: print("...", len(rb["rows"]), "linhas")'
}

# A resposta XML declara ISO-8859-1 ou UTF-8 conforme o serviço: decodifica pelo que ela declara.
decodificar_xml() {
  python3 -c '
import re, sys
b = sys.stdin.buffer.read()
m = re.search(rb"encoding=\"([^\"]+)\"", b[:100])
print(b.decode(m.group(1).decode() if m else "utf-8", errors="replace"))'
}

[[ $# -ge 1 ]] || { sed -n '2,11p' "$0"; exit 1; }
SESSAO="$(login)"
[[ -n "$SESSAO" ]] || { echo "Login falhou." >&2; exit 1; }
trap 'logout "$SESSAO"' EXIT

case "$1" in
  sql) sql "$SESSAO" "$2" ;;
  xml)
    curl -s -X POST -H "Cookie: JSESSIONID=$SESSAO" -H "Content-Type: ${3:-text/xml; charset=UTF-8}" --data-binary @- \
      "$BASE/service.sbr?serviceName=$2" | decodificar_xml ;;
  json)
    curl -s -X POST -H "Cookie: JSESSIONID=$SESSAO" -H 'Content-Type: application/json; charset=UTF-8' --data-binary @- \
      "$BASE/service.sbr?serviceName=$2&outputType=json"; echo ;;
  morta-sql)
    MORTA="$(login)"; logout "$MORTA"
    sql "$MORTA" "$2" ;;
  *) echo "Comando desconhecido: $1" >&2; exit 1 ;;
esac
