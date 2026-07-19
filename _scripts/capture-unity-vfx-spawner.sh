#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/_scripts/unity-vfx-spawner-probe"
UNITY_VERSION="${UNITY_EDITOR_VERSION:-2022.3.61f1}"
UNITY="${UNITY_EDITOR_PATH:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"
OUTPUT="${1:-$ROOT/parity-evidence/unity-vfx-spawner-$UNITY_VERSION.json}"
EDITOR_LOG="${2:-$ROOT/parity-evidence/unity-vfx-spawner-$UNITY_VERSION-editor.log}"
PLAYER_LOG="${3:-$ROOT/parity-evidence/unity-vfx-spawner-$UNITY_VERSION-player.log}"
BUILD="$PROJECT/Build/AnityVFXSpawnerProbe.app"

test -x "$UNITY" || { echo "Unity Editor is unavailable: $UNITY" >&2; exit 1; }
mkdir -p "$(dirname "$OUTPUT")" "$(dirname "$EDITOR_LOG")" "$(dirname "$PLAYER_LOG")"

"$UNITY" \
  -batchmode \
  -quit \
  -projectPath "$PROJECT" \
  -executeMethod Anity.VFXSpawnerProbe.Run \
  -anityBuildPath "$BUILD" \
  -logFile "$EDITOR_LOG"

PLAYER="$BUILD/Contents/MacOS/AnityVFXSpawnerProbe"
test -x "$PLAYER" || { echo "Unity VFX Spawner Player is unavailable: $PLAYER" >&2; exit 1; }
rm -f "$OUTPUT"
"$PLAYER" \
  -popupwindow \
  -screen-width 64 \
  -screen-height 64 \
  -anityOutput "$OUTPUT" \
  -logFile "$PLAYER_LOG"

test -s "$OUTPUT" || { echo "Unity VFX Spawner probe did not produce $OUTPUT" >&2; exit 1; }
command -v jq >/dev/null || { echo "jq is required to validate Unity VFX Spawner evidence" >&2; exit 1; }
jq -e --arg unityVersion "$UNITY_VERSION" '
  .editorVersion == $unityVersion and
  .visualEffectGraphVersion == "14.0.11" and
  .graphicsDevice == "Metal" and
  .resetSeedOnPlay == false and
  (.vfxMaxDeltaTime > 0.04999 and .vfxMaxDeltaTime < 0.05001) and
  (.records | length) == 582 and
  (.outputEvents | length) == 100 and
  (.callbackRecords | length) == 24 and
  (.builtInRecords | length) == 66 and
  (.manualControlRecords | length) == 17 and
  ([.records[] | select(.sleeping == false)] | length) > 0 and
  ([.records[] | select(.scenario == "constant_finite" and .loopState == "Finished")] | length) > 0 and
  ([.records[] | select(.scenario == "zero_count" and .loopCount == 0 and .loopIndex == 1 and .loopState == "Finished")] | length) > 0 and
  ([.outputEvents[] | select(
      .scenario == "set_scalar_order" and .hasSize and .size == 7.25 and
      .hasMeshIndex and .meshIndex == 4294967294 and .hasAlive and .alive and
      .hasSpawnTime and .spawnTime == 42.5)] | length) == 5 and
  ([.outputEvents[] | select(
      .scenario == "set_vector_off" and .hasPosition and
      .positionX == -1 and .positionY == 2.25 and .positionZ == 3.5)] | length) == 5 and
  ([.records[] | select(
      .scenario == "set_spawn_count_after_rate" and .phase == "tick" and
      .spawnCount == 3 and .hasEventSpawnCount and .eventSpawnCount == 3)] | length) == 8 and
  ([.outputEvents[] | select(.scenario == "set_spawn_count_after_rate" and .spawnCount == 3)] | length) == 12 and
  ([.outputEvents[] | select(
      .scenario == "set_spawn_count_before_rate" and .sequence == 0 and
      .spawnCount > 3.79999 and .spawnCount < 3.80001)] | length) == 1 and
  ([.outputEvents[] | select(
      .scenario == "set_spawn_count_before_rate" and .sequence == 1 and
      .spawnCount > 4.59999 and .spawnCount < 4.60001)] | length) == 1 and
  ([.outputEvents[] | select(.scenario == "set_random_per_component" and .hasPosition)] | length) == 25 and
  ([.outputEvents[] | select(.scenario == "set_random_uniform" and .hasPosition)] | length) == 25 and
  ([.callbackRecords[] | select(.method == "OnPlay" and .spawnCountBefore == 1 and .spawnCountAfter == 1)] | length) == 6 and
  ([.callbackRecords[] | select(.method == "OnStop" and .spawnCountBefore == 1 and .spawnCountAfter == 1 and .playing == false and .loopState == "Finished")] | length) == 2 and
  ([.callbackRecords[] | select(.method == "OnUpdate" and .playing == false and .loopState == "Finished" and .spawnCountBefore == 0 and .spawnCountAfter == 2)] | length) == 2 and
  ([.callbackRecords[] | select(
      .effectName == "probe-callback_after_rate-1" and .method == "OnUpdate" and
      .marker == 100 and .spawnDelta == 2 and
      .spawnCountBefore > 0.79999 and .spawnCountBefore < 0.80001 and
      .spawnCountAfter > 2.79999 and .spawnCountAfter < 2.80001)] | length) == 7 and
  ([.callbackRecords[] | select(
      .effectName == "probe-callback_before_rate-1" and .method == "OnUpdate" and
      .marker == 200 and .spawnDelta == 2 and
      .spawnCountBefore == 0 and .spawnCountAfter == 2)] | length) == 8 and
  ([.outputEvents[] | select(
      .scenario == "callback_after_rate" and .sequence == 0 and .size == 100 and
      .spawnCount > 2.79999 and .spawnCount < 2.80001)] | length) == 1 and
  ([.outputEvents[] | select(
      .scenario == "callback_before_rate" and .sequence == 0 and .size == -1 and
      .spawnCount > 2.79999 and .spawnCount < 2.80001)] | length) == 1 and
  ([.builtInRecords[] | select(.effectName == "probe-callback_builtins-17" and .method == "OnPlay")] | length) == 3 and
  ([.builtInRecords[] | select(.effectName == "probe-callback_builtins-17" and .method == "OnUpdate")] | length) == 8 and
  ([.builtInRecords[] | select(.effectName == "probe-callback_builtins-17" and .method == "OnStop")] | length) == 1 and
  ([.builtInRecords[] | select(
      .effectName == "probe-callback_builtins-17" and
      .effectPlayRate == 1.75 and .vfxPlayRate == 1.75 and
      .effectStartSeed == 17 and .effectResetSeedOnPlay == false and
      .systemSeed == 17 and .gameTimeScale == .timeScale and
      .vfxManagerFixedTimeStep == .managerFixedTimeStep and
      .vfxManagerMaxDeltaTime == .managerMaxDeltaTime and
      .gameDeltaTime == .timeDeltaTime and
      .gameUnscaledDeltaTime == .timeUnscaledDeltaTime and
      .gameSmoothDeltaTime == .timeSmoothDeltaTime and
      .gameTotalTime == .timeTotalTime and
      .gameUnscaledTotalTime == .timeUnscaledTotalTime and
      .gameTotalTimeSinceSceneLoad == .timeSinceSceneLoad)] | length) == 12 and
  ([.builtInRecords[] | select(
      .effectName == "probe-callback_builtins-17" and
      .method == "OnUpdate" and .vfxDeltaTime == .stateDeltaTime and
      (.vfxUnscaledDeltaTime * .vfxPlayRate - .vfxDeltaTime > -0.000001) and
      (.vfxUnscaledDeltaTime * .vfxPlayRate - .vfxDeltaTime < 0.000001))] | length) == 8 and
  ([.builtInRecords[] | select(
      .effectName == "probe-callback_builtins-17" and
      .method == "OnPlay" and .stateDeltaTime == 0 and .vfxDeltaTime > 0 and
      .vfxTotalTime >= .stateTotalTime)] | length) == 2 and
  ([.builtInRecords[] | select(
      .effectName == "probe-callback_builtins-17" and
      .method == "OnPlay" and .vfxDeltaTime > 0)] | length) == 3 and
  ([.builtInRecords[] | select(
      .sequence == 9 and .method == "OnPlay" and
      .stateDeltaTime > .vfxDeltaTime and .vfxTotalTime > .stateTotalTime)] | length) == 1 and
  ([.builtInRecords[] | select(
      .effectName == "probe-callback_builtins-17" and
      .method == "OnStop" and .stateTotalTime == 0 and .vfxTotalTime > 0.32)] | length) == 1 and
  ([.builtInRecords[] | select(
      .effectName == "probe-callback_builtins-17" and
      .localToWorld.m03 == 3 and .localToWorld.m13 == -2 and .localToWorld.m23 == 5 and
      .localToWorld.m00 == .componentLocalToWorld.m00 and
      .localToWorld.m11 == .componentLocalToWorld.m11 and
      .localToWorld.m22 == .componentLocalToWorld.m22 and
      (.worldToLocal.m03 - .componentWorldToLocal.m03 > -0.000001) and
      (.worldToLocal.m03 - .componentWorldToLocal.m03 < 0.000001) and
      (.worldToLocal.m13 - .componentWorldToLocal.m13 > -0.000001) and
      (.worldToLocal.m13 - .componentWorldToLocal.m13 < 0.000001) and
      (.worldToLocal.m23 - .componentWorldToLocal.m23 > -0.000001) and
      (.worldToLocal.m23 - .componentWorldToLocal.m23 < 0.000001))] | length) == 12 and
  ([.builtInRecords[] | select(.effectName == "probe-callback_builtins-17")] |
      group_by(.timeFrameCount - .vfxFrameIndex) | length) == 1 and
  ([.builtInRecords[] | select(.effectName == "probe-callback_builtins-17" and .sequence > 2) |
      .vfxFrameIndex] | unique | length) >= 7 and
  ([.builtInRecords[] | select(.effectName == "frame-semantics-effect-a" and .method == "OnPlay")] | length) == 2 and
  ([.builtInRecords[] | select(.effectName == "frame-semantics-effect-a" and .method == "OnUpdate")] | length) == 12 and
  ([.builtInRecords[] | select(.effectName == "frame-semantics-effect-a" and .method == "OnStop")] | length) == 1 and
  ([.builtInRecords[] | select(.effectName == "frame-semantics-effect-b" and .method == "OnPlay")] | length) == 2 and
  ([.builtInRecords[] | select(.effectName == "frame-semantics-effect-b" and .method == "OnUpdate")] | length) == 15 and
  ([.builtInRecords[] | select(.effectName == "frame-semantics-effect-b" and .method == "OnStop")] | length) == 1 and
  ([.builtInRecords[] | select(
      (.effectName == "frame-semantics-effect-a" or .effectName == "frame-semantics-effect-b") and
      (.cameraCount != 3 or .effectCulled != false))] | length) == 0 and
  ([.builtInRecords[] | select(
      .effectName == "frame-semantics-effect-a" and
      (.effectPlayRate != 1.25 or .effectStartSeed != 101))] | length) == 0 and
  ([.builtInRecords[] | select(
      .effectName == "frame-semantics-effect-b" and
      (.effectPlayRate != 2 or .effectStartSeed != 202))] | length) == 0 and
  ([.builtInRecords[] | select(
      .effectName == "frame-semantics-effect-a" and .method == "OnUpdate" and
      .effectPause == true and .vfxDeltaTime == 0)] | length) == 3 and
  ([.builtInRecords[] | select(
      .effectName == "frame-semantics-effect-a" and .method == "OnUpdate" and
      .effectPause == true) | .vfxTotalTime] | unique | length) == 1 and
  ([.builtInRecords[] | select(
      .effectName == "frame-semantics-effect-a" and .method == "OnUpdate" and
      .effectPause == true) | .vfxFrameIndex] | unique | length) == 3 and
  ([.builtInRecords[] | select(
      .effectName == "frame-semantics-effect-b" and .method == "OnUpdate" and
      .vfxDeltaTime <= 0)] | length) == 0 and
  ([.builtInRecords[] | select(
      (.effectName == "frame-semantics-effect-a" or .effectName == "frame-semantics-effect-b") and
      .method == "OnUpdate")] | group_by(.timeFrameCount) | length) == 15 and
  ([.builtInRecords[] | select(
      (.effectName == "frame-semantics-effect-a" or .effectName == "frame-semantics-effect-b") and
      .method == "OnUpdate")] | group_by(.timeFrameCount) | map(select(length == 2)) | length) == 12 and
  ([.builtInRecords[] | select(
      (.effectName == "frame-semantics-effect-a" or .effectName == "frame-semantics-effect-b") and
      .method == "OnUpdate")] | group_by(.timeFrameCount) | map(select(length == 1)) | length) == 3 and
  ([[.builtInRecords[] | select(
      (.effectName == "frame-semantics-effect-a" or .effectName == "frame-semantics-effect-b") and
      .method == "OnUpdate")] | group_by(.timeFrameCount)[] | select(length == 1) | .[] |
      select(.effectName != "frame-semantics-effect-b")] | length) == 0 and
  ([[.builtInRecords[] | select(
      (.effectName == "frame-semantics-effect-a" or .effectName == "frame-semantics-effect-b") and
      .method == "OnUpdate")] | group_by(.timeFrameCount)[] | select(length == 2) |
      select((map(.vfxFrameIndex) | unique | length) != 1)] | length) == 0 and
  ([.builtInRecords[] | select(
      .effectName == "frame-semantics-effect-a" and .method == "OnUpdate" and
      .localToWorld.m03 == 100000)] | length) == 1 and
  ([.builtInRecords[] | select(
      .effectName == "frame-semantics-effect-a" and .method == "OnUpdate")]) as $effectAUpdates |
  ($effectAUpdates[10].timeFrameCount - $effectAUpdates[9].timeFrameCount) == 4 and
  ((($effectAUpdates[10].vfxTotalTime - $effectAUpdates[9].vfxTotalTime) -
      $effectAUpdates[9].vfxDeltaTime) | fabs) < 0.000001
  and ([.manualControlRecords[] | select(
      .action == "advance_one_frame_paused" and .phase == "immediate" and
      .callbackDelta == 0 and .pause == true)] | length) == 1
  and ([.manualControlRecords[] | select(
      .action == "advance_one_frame_paused" and .phase == "next_player_frame" and
      .callbackDelta == 1 and .deltaTime > 0.07499 and .deltaTime < 0.07501 and
      .totalTime > 0.07499 and .totalTime < 0.07501)] | length) == 1
  and ([.manualControlRecords[] | select(
      .action == "advance_one_frame_unpaused" and .phase == "immediate" and
      .callbackDelta == 0 and .exceptionType == "")] | length) == 1
  and ([.manualControlRecords[] | select(
      .action == "simulate_zero_steps" and .phase == "next_player_frame" and
      .callbackDelta == 1 and .totalTime > 0.22499 and .totalTime < 0.22501)] | length) == 1
  and ([.manualControlRecords[] | select(
      .action == "simulate_one_step" and .phase == "next_player_frame" and
      .callbackDelta == 2 and .totalTime > 0.34999 and .totalTime < 0.35001)] | length) == 1
  and ([.manualControlRecords[] | select(
      .action == "simulate_three_steps" and .phase == "next_player_frame" and
      .callbackDelta == 4 and .totalTime > 0.72499 and .totalTime < 0.72501)] | length) == 1
  and ([.manualControlRecords[] | select(
      .action == "simulate_negative_delta" and .phase == "next_player_frame" and
      .callbackDelta == 2 and .exceptionType == "" and
      .totalTime > 0.59999 and .totalTime < 0.60001)] | length) == 1
  and ([.manualControlRecords[] | select(
      .action == "simulate_nan_delta" and .phase == "next_player_frame" and
      .callbackDelta == 2 and .exceptionType == "" and (.totalTime | isnan))] | length) == 1
  and ([.manualControlRecords[] | select(
      .action == "reinit" and .phase == "immediate" and .callbackDelta == 0 and
      .playing == false and .loopState == "Finished" and .totalTime == 0)] | length) == 1
  and ([.manualControlRecords[] | select(
      .action == "reinit" and .phase == "next_player_frame" and .callbackDelta == 2 and
      .playing == true and .loopState == "Looping" and .totalTime == 0)] | length) == 1
' "$OUTPUT" >/dev/null || { echo "Unity VFX Spawner evidence failed semantic validation" >&2; exit 1; }
echo "Unity VFX Spawner evidence → $OUTPUT"
