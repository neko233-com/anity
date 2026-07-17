using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Anity
{
    public sealed class VFXProbeSpawnerCallbacks : VFXSpawnerCallbacks
    {
        public sealed class InputProperties
        {
            public float SpawnDelta = 2f;
            public float Marker = 100f;
        }

        private static readonly int SpawnDeltaId = Shader.PropertyToID("SpawnDelta");
        private static readonly int MarkerId = Shader.PropertyToID("Marker");
        private static readonly int SizeId = Shader.PropertyToID("size");
        private int _updateIndex;

        public override void OnPlay(
            VFXSpawnerState state,
            VFXExpressionValues vfxValues,
            VisualEffect vfxComponent)
        {
            _updateIndex = 0;
            Record("OnPlay", state, vfxValues, vfxComponent, state.spawnCount);
        }

        public override void OnUpdate(
            VFXSpawnerState state,
            VFXExpressionValues vfxValues,
            VisualEffect vfxComponent)
        {
            float before = state.spawnCount;
            float marker = vfxValues.GetFloat(MarkerId);
            state.spawnCount += vfxValues.GetFloat(SpawnDeltaId);
            state.vfxEventAttribute.SetFloat(SizeId, marker + _updateIndex);
            Record("OnUpdate", state, vfxValues, vfxComponent, before);
            _updateIndex++;
        }

        public override void OnStop(
            VFXSpawnerState state,
            VFXExpressionValues vfxValues,
            VisualEffect vfxComponent)
        {
            Record("OnStop", state, vfxValues, vfxComponent, state.spawnCount);
        }

        private static void Record(
            string method,
            VFXSpawnerState state,
            VFXExpressionValues values,
            VisualEffect effect,
            float spawnCountBefore)
        {
            VFXProbeCallbackLog.Add(new ProbeCallbackRecord
            {
                effectName = effect.gameObject.name,
                method = method,
                spawnCountBefore = spawnCountBefore,
                spawnCountAfter = state.spawnCount,
                playing = state.playing,
                loopState = state.loopState.ToString(),
                deltaTime = state.deltaTime,
                totalTime = state.totalTime,
                marker = values.GetFloat(MarkerId),
                spawnDelta = values.GetFloat(SpawnDeltaId),
                hasSize = state.vfxEventAttribute.HasFloat(SizeId),
                size = state.vfxEventAttribute.HasFloat(SizeId)
                    ? state.vfxEventAttribute.GetFloat(SizeId)
                    : 0f
            });
        }
    }

    public static class VFXProbeCallbackLog
    {
        private static readonly object Sync = new object();
        private static readonly List<ProbeCallbackRecord> Records = new List<ProbeCallbackRecord>();
        private static int _sequence;

        public static void Clear()
        {
            lock (Sync)
            {
                Records.Clear();
                _sequence = 0;
            }
        }

        public static void Add(ProbeCallbackRecord record)
        {
            lock (Sync)
            {
                record.sequence = _sequence++;
                Records.Add(record);
            }
        }

        public static ProbeCallbackRecord[] Snapshot()
        {
            lock (Sync)
                return Records.ToArray();
        }
    }

    [Serializable]
    public sealed class ProbeCallbackRecord
    {
        public int sequence;
        public string effectName;
        public string method;
        public float spawnCountBefore;
        public float spawnCountAfter;
        public bool playing;
        public string loopState;
        public float deltaTime;
        public float totalTime;
        public float marker;
        public float spawnDelta;
        public bool hasSize;
        public float size;
    }
}
