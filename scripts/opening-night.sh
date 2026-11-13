#!/usr/bin/env bash
# Opening night: consigns three works and lets rival paddles fight over them, so there is
# something to watch at http://localhost:5080. Needs the AppHost running and curl.
set -euo pipefail

BASE="${MUSA_URL:-http://localhost:5080}/api"
SECONDS_OPEN="${1:-90}"

post() { curl -sS -X POST "$BASE/$1" -H 'Content-Type: application/json' -d "$2"; }
field() { python3 -c "import sys, json; print(json.load(sys.stdin).get('$1', ''))"; }

pieces=(
  0190a000-0000-7000-8000-000000000001   # Nocturne en bleu de Prusse
  0190a000-0000-7000-8000-000000000004   # La hora violeta
  0190a000-0000-7000-8000-000000000009   # Lámpara Halo
)
paddles=(P-404 P-505 P-606)

lots=()
for piece in "${pieces[@]}"; do
  lot=$(post auctions/lots "{\"pieceId\":\"$piece\",\"durationSeconds\":$SECONDS_OPEN}" | field lotNumber)
  if [[ -n "$lot" ]]; then
    lots+=("$lot")
    echo "Lote $lot en la tarima"
  fi
done
[[ ${#lots[@]} -gt 0 ]] || { echo "Ninguna obra disponible para subastar (¿ya se vendieron?)"; exit 0; }

round=0
while :; do
  open=0
  for lot in "${lots[@]}"; do
    state=$(curl -sS "$BASE/auctions/lots/$lot")
    [[ "$(field status <<< "$state")" == "Open" ]] || continue
    open=1
    paddle="${paddles[$(( (round + lot) % ${#paddles[@]} ))]}"
    [[ "$(field leadingPaddle <<< "$state")" == "$paddle" ]] && continue
    # The room thins out as the price approaches the high estimate.
    next=$(field nextMinimumBid <<< "$state"); high=$(field estimateHigh <<< "$state")
    if (( RANDOM % 100 < 70 )) && python3 -c "import sys; sys.exit(0 if $next <= $high * 1.15 else 1)"; then
      post "auctions/lots/$lot/bids" "{\"paddle\":\"$paddle\",\"amount\":$next}" > /dev/null
      echo "Lote $lot · $paddle ofrece $next"
    fi
  done
  (( open )) || break
  round=$(( round + 1 ))
  sleep $(( 2 + RANDOM % 4 ))
done
echo "La sala ha cerrado. Mira el diario y la procedencia de cada obra."
