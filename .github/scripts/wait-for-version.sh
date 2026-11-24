#!/usr/bin/env bash
# Espera o site informar, em /version, o commit que acabou de ser publicado.
# O /health não serve para isso: o Render mantém a versão antiga respondendo até a nova ficar pronta.
#
# Uso: bash .github/scripts/wait-for-version.sh <url-do-site> <commit> [tentativas] [segundos-entre-tentativas]
# Sai com 0 quando o site informa o commit esperado e com 1 se ele não aparecer a tempo.
set -euo pipefail

site="${1:?Informe a URL do site}"
expected="${2:?Informe o commit esperado}"
attempts="${3:-60}"
interval="${4:-20}"

for ((i = 1; i <= attempts; i++)); do
  # Enquanto a versão antiga está no ar (ou o app está subindo), a resposta falha ou não traz o commit: só tenta de novo.
  body="$(curl --silent --fail --max-time 30 "${site%/}/version" 2>/dev/null || true)"
  current="$(printf '%s' "$body" | sed -n 's/.*"commit"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"

  echo "[$i/$attempts] no ar: ${current:-sem resposta}"

  if [[ "$current" == "$expected" ]]; then
    echo "A versão nova está no ar: $expected"
    exit 0
  fi

  if ((i < attempts)); then sleep "$interval"; fi
done

echo "O commit $expected não apareceu em /version depois de $attempts tentativas." >&2
exit 1
