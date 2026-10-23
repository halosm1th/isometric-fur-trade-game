#!/bin/sh
# A man's painted idle frames worn by the 3D doll, rendered in every animation.
#
#   sh projected.sh SHEET_DIR BRIM CHIN [OUT_DIR]
#
# SHEET_DIR holds idle_front.png, idle_side.png and idle_back.png; BRIM and
# CHIN are the rows of the front frame where the hat meets the brow and the
# chin ends (fit.py). Frames land in OUT_DIR (default out/proj_ink).
set -e
B=${BLENDER:-/c/Users/thoma/tools/blender-4.5.15-windows-x64/blender.exe}
cd "$(dirname "$0")"
SHEETS=$(cd "$1" && pwd); OUT=${4:-out/proj_ink}
quiet() { grep -E "Traceback|Error|line [0-9]" || true; }
"$B" -b out/doll.blend -P fit.py -- "$SHEETS" out/doll_fit.blend --brim=$2 --chin=$3 2>&1 | quiet
"$B" -b out/doll_fit.blend -P projected.py -- "$SHEETS" 2>&1 | quiet
rm -rf out/preview; mkdir -p out/preview
DOLL_SS=4 "$B" -b out/doll_proj.blend -P render.py -- preview 2>&1 | quiet
rm -rf "$OUT"
python outline.py out/preview "$OUT"
