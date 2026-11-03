#!/usr/bin/env bash
# Helpers for self-checks in Play Mode: . Tools/WebGLCheck/selfcheck.sh, then is <InputScript method> [json args],
# db <DevBridge method> [json args], shot <name under Docs/Verification/stage-1>, play enter|exit [seconds], counts.
B="bash Tools/WebGLCheck/bridge.sh"
is() { $B "{\"commands\":[{\"op\":\"call\",\"type\":\"Core.EditorTools.InputScript, Assembly-CSharp-Editor\",\"member\":\"$1\",\"args\":${2:-[]}}]}" 30 | grep returned; }
db() { $B "{\"commands\":[{\"op\":\"call\",\"type\":\"Meta.DeveloperTools.Services.DevBridge, Assembly-CSharp\",\"member\":\"$1\",\"args\":${2:-[]}}]}" 30 | grep returned; }
shot() { $B "{\"commands\":[{\"op\":\"screenshot\",\"path\":\"../Docs/Verification/stage-1/$1.png\"}]}" 30 >/dev/null; sleep 0.5; }
play() { $B "{\"commands\":[{\"op\":\"setPlayMode\",\"value\":\"$1\"}]}" 30 >/dev/null; sleep ${2:-5}; }
counts() { $B '{"commands":[{"op":"consoleCounts"}]}' 30 | grep -E "errors|warnings"; }
