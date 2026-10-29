#!/usr/bin/env bash
# Starts the real Arcadia under Xvfb (1920 × 1080), optionally sends keys, and saves a screenshot: the Linux twin of
# Screenshot-Arcadia.ps1 for cloud threads (#180). Gamepad input isn't available here; the keys stand in for it.
#
#   tools/dev/screenshot-arcadia.sh --out /tmp/a.png                                  # library
#   tools/dev/screenshot-arcadia.sh --out /tmp/a.png --keys Return --after-keys-ms 1000   # settings
#   tools/dev/screenshot-arcadia.sh --out /tmp/a.png --keys "shift+e shift+e v"           # next tab twice, view dialog
#
# Keys are xdotool key names, separated by spaces, as in docs/product/controls.md: w a s d (move), space (A), Escape
# (B), f (X, search), v (Y, view), r (game menu), Return (settings), q/e (pages), shift+q/shift+e (tabs), 1 … 0 (tab).
# Build first: dotnet build Arcadia.slnx.
# Settings and logs go to a fresh folder per run (printed at the end), so runs don't change each other; --settings FILE
# starts that run with a settings.json of your own, e.g. a Steam folder picked by hand (#83).
set -euo pipefail

out=""
arguments="--demo"
settings=""
wait_ms=15000
keys=""
after_keys_ms=500
keep_running=false

usage() {
    sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'
    echo "Options: --out PATH (required) --arguments '--demo' --settings FILE --wait-ms 15000 --keys 'KEY …' --after-keys-ms 500 --keep-running"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --out) out="$2"; shift 2 ;;
        --arguments) arguments="$2"; shift 2 ;;
        --settings) settings="$(realpath "$2")"; shift 2 ;;
        --wait-ms) wait_ms="$2"; shift 2 ;;
        --keys) keys="$2"; shift 2 ;;
        --after-keys-ms) after_keys_ms="$2"; shift 2 ;;
        --keep-running) keep_running=true; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option $1" >&2; usage >&2; exit 2 ;;
    esac
done

[[ -n "$out" ]] || { usage >&2; exit 2; }
for tool in Xvfb xdotool import; do
    command -v "$tool" >/dev/null || { echo "$tool missing: apt-get install -y xvfb xdotool imagemagick" >&2; exit 1; }
done

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
exe="$repo/src/Arcadia.App/bin/Debug/net10.0/Arcadia"
[[ -x "$exe" ]] || { echo "No build at $exe: dotnet build Arcadia.slnx" >&2; exit 1; }
out="$(realpath -m "$out")"

# A display of our own, so a run never sends keys into another one.
display=99
while [[ -e "/tmp/.X11-unix/X$display" || -e "/tmp/.X$display-lock" ]]; do display=$((display + 1)); done
export DISPLAY=":$display"
Xvfb "$DISPLAY" -screen 0 1920x1080x24 -nolisten tcp >/dev/null 2>&1 &
xvfb_pid=$!

# settings.json and arcadia.log land under these folders instead of the user's own.
data="$(mktemp -d)"
export XDG_CONFIG_HOME="$data/config" XDG_DATA_HOME="$data/share"
mkdir -p "$XDG_CONFIG_HOME" "$XDG_DATA_HOME"
if [[ -n "$settings" ]]; then
    mkdir -p "$XDG_CONFIG_HOME/Arcadia"
    cp "$settings" "$XDG_CONFIG_HOME/Arcadia/settings.json"
fi

app_pid=""
cleanup() {
    if [[ -n "$app_pid" ]] && ! $keep_running; then
        kill "$app_pid" 2>/dev/null || true
        wait "$app_pid" 2>/dev/null || true
    fi
    $keep_running || kill "$xvfb_pid" 2>/dev/null || true
}
trap cleanup EXIT

for _ in $(seq 50); do xdotool getdisplaygeometry >/dev/null 2>&1 && break; sleep 0.1; done
# shellcheck disable=SC2086 # the arguments are split on purpose, as the app gets them.
(cd "$data" && exec "$exe" $arguments >"$data/stdout.log" 2>&1) &
app_pid=$!

# The window exists before Arcadia has drawn its library; the extra second covers the first frames.
window=""
deadline=$(( $(date +%s%N) / 1000000 + wait_ms ))
while [[ -z "$window" ]]; do
    window="$(xdotool search --onlyvisible --pid "$app_pid" 2>/dev/null | head -n 1 || true)"
    kill -0 "$app_pid" 2>/dev/null || { echo "Arcadia ended early, see $data/stdout.log" >&2; exit 1; }
    (( $(date +%s%N) / 1000000 < deadline )) || { echo "Arcadia has no window after ${wait_ms} ms: raise --wait-ms" >&2; exit 1; }
    [[ -n "$window" ]] || sleep 0.2
done
# Xvfb has no window manager: the window gets the whole screen by hand, as on a TV, and the pointer goes out of the
# way so it hovers nothing.
xdotool windowmove "$window" 0 0 windowsize "$window" 1920 1080 mousemove 1919 1079
sleep 1
xdotool windowfocus --sync "$window" 2>/dev/null || true

for key in $keys; do
    xdotool key --clearmodifiers "$key"
    sleep 0.35
done
sleep "$(awk "BEGIN { print $after_keys_ms / 1000 }")"

import -window root "$out"
echo "Saved $out (settings and logs: $data)"
$keep_running && echo "Arcadia keeps running on DISPLAY=$DISPLAY (pid $app_pid, Xvfb $xvfb_pid)"
exit 0
