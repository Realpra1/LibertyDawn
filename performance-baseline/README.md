# CNC repeatable performance baseline

This repository-owned workload generates a deterministic copy of Empire Earth,
creates 550 mixed released CNC mobile actors for each of six ordinary SkyNet and
Brutalis AIs, and leaves every normal AI module active. The acceptance gate
requires every bot to retain at least 300 live in-world mobile actors at every
100-tick sample from tick 500 through tick 3000.

Build CNC, locate an installed CNC content directory, and run the golden batch
from the repository root. The output path must not already exist.

```sh
make check
python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py \
  --lock-dir /path/to/round/locks \
  --resource game --capacity 2 --slots 2 -- \
  python3 run-cnc-performance-baseline.py \
    --content /path/to/runtime-content \
    --output /new/path/cnc47-golden \
    --repetitions 3 \
    --build-configuration Debug
```

The six golden runs are serial and interleaved between actual Normal (40 ms)
and Fastest (20 ms). A nonzero result means at least one run failed; inspect
`batch-summary.json` and each run's `summary.json` rather than discarding the
attempt. The output records the requested and engine-accepted speed, fixed tick
window, per-bot counts/activity, ratios, all benchmark streams, separate measured
checkout and workload-source revision/dirty state, requested build configuration,
verified launcher/engine hashes, host/runtime identity, workload/map/manifest
hashes, and a SHA-256 inventory of run artifacts.

Run the separately labeled bounded profile into another new directory:

```sh
python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py \
  --lock-dir /path/to/round/locks \
  --resource game --capacity 2 --slots 2 -- \
  python3 run-cnc-performance-baseline.py \
    --content /path/to/runtime-content \
    --output /new/path/cnc47-profile \
    --profile-only \
    --build-configuration Debug
```

Profile runs use a 100 ms simulation threshold, enforce a 16 MiB raw-log cap,
and rank hotspots by count, cumulative time, and maximum time. They are never
included in golden timing distributions.

For CNC-48/CNC-49 comparisons, pass `--launcher` pointing at the other Git
checkout's built `launch-game.sh`. The command fails before launching if it cannot
verify that checkout's Git revision, dirty paths, CNC version, launcher, and
`bin/OpenRA.dll`; `--build-configuration` records the requested label while the
engine hash identifies what actually ran. Keep the same checked-in workload/
configuration and content, reserve both game slots, use a new output directory,
and compare only runs whose workload/map hashes, tick bounds, roster, and accepted
speeds match. MAX remains a compatibility control and is not equivalent to Fastest.
