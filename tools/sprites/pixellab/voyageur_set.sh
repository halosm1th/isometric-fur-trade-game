#!/bin/sh
# The voyageur's whole set from PixelLab, posed by the doll's skeletons:
#   sh voyageur_set.sh [JOB ...]       (all jobs if none named)
# A job whose out/<job>.json is already there is skipped - rerun one by
# deleting its json. Needs PIXELLAB_API_KEY.
cd "$(dirname "$0")"
V=../../../assets/sprites/voyageur
job() {  # name ref ref_frame direction action holding frames...  (NOPIPE=1 job ... : no pipe)
    n=$1 ref=$2 rf=$3 dir=$4 act=$5 hold=$6; shift 6
    if [ -n "$ONLY" ] && ! echo " $ONLY " | grep -q " $n "; then return; fi
    [ -f out/$n.json ] && { echo "[set] $n: already done"; return; }
    python pixellab.py skeleton $n $V/$ref $rf "$@" --direction=$dir "--action=$act" ${hold:+"--holding=$hold"} ${NOPIPE:+--nopipe=1} 2>&1 | grep -v "processing\.\.\."
}
ONLY="$*"
f() { echo ${1}_0 ${1}_1 ${1}_2 ${1}_3; }
job walk        idle_front.png idle_0      south      walk   ""                                         $(f walk)
job walk_back   idle_back.png  idle_back_0 north      walk   ""                                         $(f walk_back)
job walk_side   idle_side.png  idle_side_0 south-east walk   ""                                         $(f walk_side)
job shoot       idle_side.png  idle_side_0 south-east "shoot a musket" "a long flintlock trade musket, butt at his shoulder" $(f shoot)
job paddle      idle_side.png  idle_side_0 south-east paddle "a long wooden canoe paddle with a red blade, gripped in both hands, the blade down in the water at his side" $(f paddle)
job portage     idle_side.png  idle_side_0 south-east "carry a heavy load" "two bales of fur packs on his back, held by a leather tumpline across his forehead" $(f portage)
NOPIPE=1 job eat   idle_side.png idle_side_0 south-east "eat bread" "a big round loaf of brown bannock bread in both hands, biting it, no pipe" $(f eat)
NOPIPE=1 job drink idle_side.png idle_side_0 south-east "drink from a cup" "a large shiny tin cup in his right hand, tipped up to his lips, no pipe" $(f drink)
job attack      idle_side.png  idle_side_0 south-east punch  "nothing, his fists bare"                   $(f attack)
job death       idle_side.png  idle_side_0 south-east "fall down dead" "nothing"                         $(f death)
