#!/usr/bin/env bash
# Runs a batch of the editor bridge and prints the result: Tools/WebGLCheck/bridge.sh '<commands json>' [timeoutSec]
# The batch: {"commands":[{"op":"..."}]}. Waits until result.json is newer than the instructions.
set -u
root="$(cd "$(dirname "$0")/../.." && pwd)"
timeout="${2:-120}"
before=""
[ -f "$root/EditorBridge/result.json" ] && before="$(grep finishedUtc "$root/EditorBridge/result.json")"
printf '%s' "$1" > "$root/EditorBridge/instructions.json"
for _ in $(seq 1 $((timeout * 2))); do
  if [ -f "$root/EditorBridge/result.json" ]; then
    now="$(grep finishedUtc "$root/EditorBridge/result.json")"
    if [ -n "$now" ] && [ "$now" != "$before" ]; then
      cat "$root/EditorBridge/result.json"
      exit 0
    fi
  fi
  sleep 0.5
done
echo "bridge: no answer in ${timeout}s"
exit 2
