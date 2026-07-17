using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Anity
{
    public sealed class VFXBuiltInProbeSpawnerCallbacks : VFXSpawnerCallbacks
    {
        public sealed class InputProperties
        {
            public float VfxDeltaTime;
            public float VfxUnscaledDeltaTime;
            public float VfxTotalTime;
            public uint VfxFrameIndex;
            public float VfxPlayRate;
            public float VfxManagerFixedTimeStep;
            public float VfxManagerMaxDeltaTime;
            public float GameDeltaTime;
            public float GameUnscaledDeltaTime;
            public float GameSmoothDeltaTime;
            public float GameTotalTime;
            public float GameUnscaledTotalTime;
            public float GameTotalTimeSinceSceneLoad;
            public float GameTimeScale;
            public Matrix4x4 LocalToWorld = Matrix4x4.identity;
            public Matrix4x4 WorldToLocal = Matrix4x4.identity;
            public uint SystemSeed;
        }

        private static readonly int VfxDeltaTimeId = Shader.PropertyToID("VfxDeltaTime");
        private static readonly int VfxUnscaledDeltaTimeId = Shader.PropertyToID("VfxUnscaledDeltaTime");
        private static readonly int VfxTotalTimeId = Shader.PropertyToID("VfxTotalTime");
        private static readonly int VfxFrameIndexId = Shader.PropertyToID("VfxFrameIndex");
        private static readonly int VfxPlayRateId = Shader.PropertyToID("VfxPlayRate");
        private static readonly int VfxManagerFixedTimeStepId = Shader.PropertyToID("VfxManagerFixedTimeStep");
        private static readonly int VfxManagerMaxDeltaTimeId = Shader.PropertyToID("VfxManagerMaxDeltaTime");
        private static readonly int GameDeltaTimeId = Shader.PropertyToID("GameDeltaTime");
        private static readonly int GameUnscaledDeltaTimeId = Shader.PropertyToID("GameUnscaledDeltaTime");
        private static readonly int GameSmoothDeltaTimeId = Shader.PropertyToID("GameSmoothDeltaTime");
        private static readonly int GameTotalTimeId = Shader.PropertyToID("GameTotalTime");
        private static readonly int GameUnscaledTotalTimeId = Shader.PropertyToID("GameUnscaledTotalTime");
        private static readonly int GameTotalTimeSinceSceneLoadId = Shader.PropertyToID("GameTotalTimeSinceSceneLoad");
        private static readonly int GameTimeScaleId = Shader.PropertyToID("GameTimeScale");
        private static readonly int LocalToWorldId = Shader.PropertyToID("LocalToWorld");
        private static readonly int WorldToLocalId = Shader.PropertyToID("WorldToLocal");
        private static readonly int SystemSeedId = Shader.PropertyToID("SystemSeed");

        public override void OnPlay(VFXSpawnerState state, VFXExpressionValues values, VisualEffect effect)
            => Record("OnPlay", state, values, effect);

        public override void OnUpdate(VFXSpawnerState state, VFXExpressionValues values, VisualEffect effect)
            => Record("OnUpdate", state, values, effect);

        public override void OnStop(VFXSpawnerState state, VFXExpressionValues values, VisualEffect effect)
            => Record("OnStop", state, values, effect);

        private static void Record(
            string method,
            VFXSpawnerState state,
            VFXExpressionValues values,
            VisualEffect effect)
        {
            VFXBuiltInProbeLog.Add(new ProbeBuiltInRecord
            {
                effectName = effect.gameObject.name,
                method = method,
                stateDeltaTime = state.deltaTime,
                stateTotalTime = state.totalTime,
                statePlaying = state.playing,
                timeDeltaTime = Time.deltaTime,
                timeUnscaledDeltaTime = Time.unscaledDeltaTime,
                timeSmoothDeltaTime = Time.smoothDeltaTime,
                timeTotalTime = Time.time,
                timeUnscaledTotalTime = Time.unscaledTime,
                timeSinceSceneLoad = Time.timeSinceLevelLoad,
                timeScale = Time.timeScale,
                timeFrameCount = Time.frameCount,
                effectPlayRate = effect.playRate,
                effectStartSeed = effect.startSeed,
                effectResetSeedOnPlay = effect.resetSeedOnPlay,
                effectPause = effect.pause,
                effectCulled = effect.culled,
                cameraCount = Camera.allCamerasCount,
                managerFixedTimeStep = VFXManager.fixedTimeStep,
                managerMaxDeltaTime = VFXManager.maxDeltaTime,
                vfxDeltaTime = values.GetFloat(VfxDeltaTimeId),
                vfxUnscaledDeltaTime = values.GetFloat(VfxUnscaledDeltaTimeId),
                vfxTotalTime = values.GetFloat(VfxTotalTimeId),
                vfxFrameIndex = values.GetUInt(VfxFrameIndexId),
                vfxPlayRate = values.GetFloat(VfxPlayRateId),
                vfxManagerFixedTimeStep = values.GetFloat(VfxManagerFixedTimeStepId),
                vfxManagerMaxDeltaTime = values.GetFloat(VfxManagerMaxDeltaTimeId),
                gameDeltaTime = values.GetFloat(GameDeltaTimeId),
                gameUnscaledDeltaTime = values.GetFloat(GameUnscaledDeltaTimeId),
                gameSmoothDeltaTime = values.GetFloat(GameSmoothDeltaTimeId),
                gameTotalTime = values.GetFloat(GameTotalTimeId),
                gameUnscaledTotalTime = values.GetFloat(GameUnscaledTotalTimeId),
                gameTotalTimeSinceSceneLoad = values.GetFloat(GameTotalTimeSinceSceneLoadId),
                gameTimeScale = values.GetFloat(GameTimeScaleId),
                localToWorld = ProbeMatrix.From(values.GetMatrix4x4(LocalToWorldId)),
                worldToLocal = ProbeMatrix.From(values.GetMatrix4x4(WorldToLocalId)),
                componentLocalToWorld = ProbeMatrix.From(effect.transform.localToWorldMatrix),
                componentWorldToLocal = ProbeMatrix.From(effect.transform.worldToLocalMatrix),
                systemSeed = values.GetUInt(SystemSeedId)
            });
        }
    }

    public static class VFXBuiltInProbeLog
    {
        private static readonly object Sync = new object();
        private static readonly List<ProbeBuiltInRecord> Records = new List<ProbeBuiltInRecord>();
        private static int _sequence;

        public static void Clear()
        {
            lock (Sync)
            {
                Records.Clear();
                _sequence = 0;
            }
        }

        public static void Add(ProbeBuiltInRecord record)
        {
            lock (Sync)
            {
                record.sequence = _sequence++;
                Records.Add(record);
            }
        }

        public static ProbeBuiltInRecord[] Snapshot()
        {
            lock (Sync)
                return Records.ToArray();
        }
    }

    [Serializable]
    public sealed class ProbeBuiltInRecord
    {
        public int sequence;
        public string effectName;
        public string method;
        public float stateDeltaTime;
        public float stateTotalTime;
        public bool statePlaying;
        public float timeDeltaTime;
        public float timeUnscaledDeltaTime;
        public float timeSmoothDeltaTime;
        public float timeTotalTime;
        public float timeUnscaledTotalTime;
        public float timeSinceSceneLoad;
        public float timeScale;
        public int timeFrameCount;
        public float effectPlayRate;
        public uint effectStartSeed;
        public bool effectResetSeedOnPlay;
        public bool effectPause;
        public bool effectCulled;
        public int cameraCount;
        public float managerFixedTimeStep;
        public float managerMaxDeltaTime;
        public float vfxDeltaTime;
        public float vfxUnscaledDeltaTime;
        public float vfxTotalTime;
        public uint vfxFrameIndex;
        public float vfxPlayRate;
        public float vfxManagerFixedTimeStep;
        public float vfxManagerMaxDeltaTime;
        public float gameDeltaTime;
        public float gameUnscaledDeltaTime;
        public float gameSmoothDeltaTime;
        public float gameTotalTime;
        public float gameUnscaledTotalTime;
        public float gameTotalTimeSinceSceneLoad;
        public float gameTimeScale;
        public ProbeMatrix localToWorld;
        public ProbeMatrix worldToLocal;
        public ProbeMatrix componentLocalToWorld;
        public ProbeMatrix componentWorldToLocal;
        public uint systemSeed;
    }

    [Serializable]
    public sealed class ProbeMatrix
    {
        public float m00; public float m01; public float m02; public float m03;
        public float m10; public float m11; public float m12; public float m13;
        public float m20; public float m21; public float m22; public float m23;
        public float m30; public float m31; public float m32; public float m33;

        public static ProbeMatrix From(Matrix4x4 value) => new ProbeMatrix
        {
            m00 = value.m00, m01 = value.m01, m02 = value.m02, m03 = value.m03,
            m10 = value.m10, m11 = value.m11, m12 = value.m12, m13 = value.m13,
            m20 = value.m20, m21 = value.m21, m22 = value.m22, m23 = value.m23,
            m30 = value.m30, m31 = value.m31, m32 = value.m32, m33 = value.m33
        };
    }
}
