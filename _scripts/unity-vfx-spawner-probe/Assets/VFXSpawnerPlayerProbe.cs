using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.VFX;

namespace Anity
{
    public sealed class VFXSpawnerPlayerProbe : MonoBehaviour
    {
        private const float FixedDeltaTime = 0.0625f;
        private IEnumerator<object> _routine;
        private ProbeReport _report;
        private string _outputPath;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = FixedDeltaTime;
            ActivateVfx();
            _outputPath = CommandLineValue("-anityOutput");
            if (string.IsNullOrEmpty(_outputPath))
            {
                Fail(new InvalidOperationException("-anityOutput is required."));
                return;
            }
            _report = new ProbeReport
            {
                editorVersion = Application.unityVersion,
                visualEffectGraphVersion = "14.0.11",
                fixedDeltaTime = FixedDeltaTime,
                vfxFixedTimeStep = VFXManager.fixedTimeStep,
                vfxMaxDeltaTime = VFXManager.maxDeltaTime,
                resetSeedOnPlay = false,
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                unityRandomSamples = CaptureUnityRandomSamples(),
                records = new List<ProbeRecord>(),
                outputEvents = new List<ProbeOutputEventRecord>(),
                callbackRecords = new List<ProbeCallbackRecord>(),
                builtInRecords = new List<ProbeBuiltInRecord>(),
                manualControlRecords = new List<ProbeManualControlRecord>()
            };
            VFXProbeCallbackLog.Clear();
            VFXBuiltInProbeLog.Clear();
            _routine = CaptureAll().GetEnumerator();
        }

        private void LateUpdate()
        {
            if (_routine == null) return;
            try
            {
                if (_routine.MoveNext()) return;
                _routine = null;
                if (!_report.records.Any(record => record.phase == "tick" && record.loopState != "Finished"))
                    throw new InvalidDataException("Unity Player produced no active VFX Spawner state.");
                _report.callbackRecords.AddRange(VFXProbeCallbackLog.Snapshot());
                _report.builtInRecords.AddRange(VFXBuiltInProbeLog.Snapshot());
                string fullPath = Path.GetFullPath(_outputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, JsonUtility.ToJson(_report, true) + "\n");
                Debug.Log($"ANITY_VFX_SPAWNER_PLAYER_OK {fullPath} records={_report.records.Count}");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private IEnumerable<object> CaptureAll()
        {
            Dictionary<string, VisualEffectAsset> assets = Resources
                .LoadAll<VisualEffectAsset>("Generated")
                .ToDictionary(asset => asset.name, StringComparer.Ordinal);
            Scenario[] scenarios =
            {
                new Scenario("infinite", new uint[] { 1 }, 8),
                new Scenario("constant_finite", new uint[] { 1 }, 24),
                new Scenario("zero_count", new uint[] { 1 }, 4),
                new Scenario("random_count", new uint[] { 1, 2, 17, 991, uint.MaxValue }, 16),
                new Scenario("random_all", new uint[] { 1, 2, 17, 991, uint.MaxValue }, 48),
                new Scenario("set_scalar_order", new uint[] { 1 }, 4),
                new Scenario("set_vector_off", new uint[] { 1 }, 4),
                new Scenario("set_random_per_component", new uint[] { 1, 2, 17, 991, uint.MaxValue }, 4),
                new Scenario("set_random_uniform", new uint[] { 1, 2, 17, 991, uint.MaxValue }, 4),
                new Scenario("set_spawn_count_after_rate", new uint[] { 1 }, 8),
                new Scenario("set_spawn_count_before_rate", new uint[] { 1 }, 8),
                new Scenario("callback_after_rate", new uint[] { 1 }, 4),
                new Scenario("callback_before_rate", new uint[] { 1 }, 4),
                new Scenario("callback_builtins", new uint[] { 17 }, 4)
            };
            foreach (Scenario scenario in scenarios)
            {
                if (!assets.TryGetValue(scenario.Name, out VisualEffectAsset asset))
                    throw new InvalidDataException($"Player is missing VFX asset '{scenario.Name}'.");
                foreach (uint seed in scenario.Seeds)
                {
                    var probeObject = new GameObject($"probe-{scenario.Name}-{seed}");
                    bool builtInScenario = string.Equals(
                        scenario.Name, "callback_builtins", StringComparison.Ordinal);
                    if (builtInScenario)
                    {
                        Time.timeScale = 0.5f;
                        probeObject.transform.position = new Vector3(3f, -2f, 5f);
                        probeObject.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
                        probeObject.transform.localScale = new Vector3(2f, 3f, 4f);
                    }
                    VisualEffect effect = probeObject.AddComponent<VisualEffect>();
                    effect.visualEffectAsset = asset;
                    effect.startSeed = seed;
                    effect.resetSeedOnPlay = false;
                    if (builtInScenario)
                        effect.playRate = 1.75f;
                    int outputSequence = 0;
                    Action<VFXOutputEventArgs> outputHandler = args =>
                        CaptureOutputEvent(scenario.Name, seed, outputSequence++, args);
                    effect.outputEventReceived += outputHandler;
                    effect.Reinit();

                    var spawnNames = new List<string>();
                    var particleNames = new List<string>();
                    effect.GetSpawnSystemNames(spawnNames);
                    effect.GetParticleSystemNames(particleNames);
                    if (spawnNames.Count != 1 || particleNames.Count != 1)
                        throw new InvalidDataException(
                            $"Scenario '{scenario.Name}' expected one Spawn and one Particle system; " +
                            $"found {spawnNames.Count} and {particleNames.Count}.");

                    Capture(scenario.Name, seed, "after_reinit", 0, effect, spawnNames[0], particleNames[0]);
                    effect.Play();
                    yield return null;
                    Capture(scenario.Name, seed, "after_play", 0, effect, spawnNames[0], particleNames[0]);
                    for (int frame = 1; frame <= scenario.FrameCount; frame++)
                    {
                        yield return null;
                        Capture(scenario.Name, seed, "tick", frame, effect, spawnNames[0], particleNames[0]);
                    }

                    effect.Stop();
                    yield return null;
                    Capture(scenario.Name, seed, "after_stop", scenario.FrameCount,
                        effect, spawnNames[0], particleNames[0]);
                    effect.Play();
                    yield return null;
                    Capture(scenario.Name, seed, "after_replay", scenario.FrameCount,
                        effect, spawnNames[0], particleNames[0]);
                    yield return null;
                    Capture(scenario.Name, seed, "replay_tick", scenario.FrameCount + 1,
                        effect, spawnNames[0], particleNames[0]);
                    effect.outputEventReceived -= outputHandler;
                    Destroy(probeObject);
                    yield return null;
                    if (builtInScenario)
                        Time.timeScale = 1f;
                }
            }
            if (!assets.TryGetValue("callback_builtins", out VisualEffectAsset frameAsset))
                throw new InvalidDataException("Player is missing frame semantics VFX asset.");
            foreach (object step in CapturePlayerLoopFrameSemantics(frameAsset))
                yield return step;
            foreach (object step in CaptureManualControlSemantics(frameAsset))
                yield return step;
        }

        private IEnumerable<object> CapturePlayerLoopFrameSemantics(
            VisualEffectAsset asset)
        {
            var firstCameraObject = new GameObject("frame-semantics-camera-a");
            var secondCameraObject = new GameObject("frame-semantics-camera-b");
            Camera firstCamera = firstCameraObject.AddComponent<Camera>();
            Camera secondCamera = secondCameraObject.AddComponent<Camera>();
            firstCamera.transform.position = new Vector3(-1f, 0f, -10f);
            secondCamera.transform.position = new Vector3(1f, 0f, -10f);
            firstCamera.depth = 100f;
            secondCamera.depth = 101f;

            var firstObject = new GameObject("frame-semantics-effect-a");
            var secondObject = new GameObject("frame-semantics-effect-b");
            VisualEffect first = ConfigureFrameSemanticsEffect(
                firstObject, asset, 101u, 1.25f);
            VisualEffect second = ConfigureFrameSemanticsEffect(
                secondObject, asset, 202u, 2f);
            first.Reinit();
            second.Reinit();
            first.Play();
            second.Play();

            for (int frame = 0; frame < 4; frame++)
                yield return null;
            first.pause = true;
            for (int frame = 0; frame < 3; frame++)
                yield return null;
            first.pause = false;
            for (int frame = 0; frame < 2; frame++)
                yield return null;
            first.transform.position = new Vector3(100000f, 100000f, 100000f);
            for (int frame = 0; frame < 3; frame++)
                yield return null;
            first.transform.position = Vector3.zero;
            for (int frame = 0; frame < 2; frame++)
                yield return null;

            first.Stop();
            second.Stop();
            yield return null;
            Destroy(firstObject);
            Destroy(secondObject);
            Destroy(firstCameraObject);
            Destroy(secondCameraObject);
            yield return null;
        }

        private static VisualEffect ConfigureFrameSemanticsEffect(
            GameObject owner,
            VisualEffectAsset asset,
            uint seed,
            float playRate)
        {
            VisualEffect effect = owner.AddComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            effect.startSeed = seed;
            effect.resetSeedOnPlay = false;
            effect.playRate = playRate;
            return effect;
        }

        private IEnumerable<object> CaptureManualControlSemantics(
            VisualEffectAsset asset)
        {
            var cameraObject = new GameObject("manual-control-camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var owner = new GameObject("manual-control-effect");
            VisualEffect effect = ConfigureFrameSemanticsEffect(
                owner, asset, 303u, 1.5f);
            var spawnNames = new List<string>();
            effect.GetSpawnSystemNames(spawnNames);
            if (spawnNames.Count != 1)
                throw new InvalidDataException("Manual-control VFX expected one Spawn system.");
            string spawnSystem = spawnNames[0];

            effect.pause = true;
            effect.Reinit();
            yield return null;
            CaptureManualControl("baseline", "paused", effect, spawnSystem, 0, null);

            int callbackCount = VFXBuiltInProbeLog.Snapshot().Length;
            string exceptionType = InvokeManualAction(effect.AdvanceOneFrame);
            CaptureManualControl(
                "advance_one_frame_paused", "immediate", effect, spawnSystem,
                callbackCount, exceptionType);
            yield return null;
            CaptureManualControl(
                "advance_one_frame_paused", "next_player_frame", effect, spawnSystem,
                callbackCount, exceptionType);

            effect.pause = false;
            yield return null;
            callbackCount = VFXBuiltInProbeLog.Snapshot().Length;
            exceptionType = InvokeManualAction(effect.AdvanceOneFrame);
            CaptureManualControl(
                "advance_one_frame_unpaused", "immediate", effect, spawnSystem,
                callbackCount, exceptionType);
            yield return null;
            CaptureManualControl(
                "advance_one_frame_unpaused", "next_player_frame", effect, spawnSystem,
                callbackCount, exceptionType);

            effect.pause = true;
            yield return null;
            foreach (object step in CaptureSimulateAction(
                         effect, spawnSystem, "simulate_zero_steps", 0.125f, 0u))
                yield return step;
            foreach (object step in CaptureSimulateAction(
                         effect, spawnSystem, "simulate_one_step", 0.125f, 1u))
                yield return step;
            foreach (object step in CaptureSimulateAction(
                         effect, spawnSystem, "simulate_three_steps", 0.125f, 3u))
                yield return step;
            foreach (object step in CaptureSimulateAction(
                         effect, spawnSystem, "simulate_negative_delta", -0.125f, 1u))
                yield return step;
            foreach (object step in CaptureSimulateAction(
                         effect, spawnSystem, "simulate_nan_delta", float.NaN, 1u))
                yield return step;

            callbackCount = VFXBuiltInProbeLog.Snapshot().Length;
            exceptionType = InvokeManualAction(effect.Reinit);
            CaptureManualControl(
                "reinit", "immediate", effect, spawnSystem,
                callbackCount, exceptionType);
            yield return null;
            CaptureManualControl(
                "reinit", "next_player_frame", effect, spawnSystem,
                callbackCount, exceptionType);

            effect.Stop();
            yield return null;
            Destroy(owner);
            Destroy(cameraObject);
            yield return null;
        }

        private IEnumerable<object> CaptureSimulateAction(
            VisualEffect effect,
            string spawnSystem,
            string action,
            float stepDeltaTime,
            uint stepCount)
        {
            int callbackCount = VFXBuiltInProbeLog.Snapshot().Length;
            string exceptionType = InvokeManualAction(
                () => effect.Simulate(stepDeltaTime, stepCount));
            CaptureManualControl(
                action, "immediate", effect, spawnSystem,
                callbackCount, exceptionType);
            yield return null;
            CaptureManualControl(
                action, "next_player_frame", effect, spawnSystem,
                callbackCount, exceptionType);
        }

        private void CaptureManualControl(
            string action,
            string phase,
            VisualEffect effect,
            string spawnSystem,
            int callbackCountBefore,
            string exceptionType)
        {
            ProbeBuiltInRecord[] callbackRecords = VFXBuiltInProbeLog.Snapshot();
            using (VFXSpawnerState state = effect.GetSpawnSystemInfo(spawnSystem))
            {
                _report.manualControlRecords.Add(new ProbeManualControlRecord
                {
                    action = action,
                    phase = phase,
                    callbackCount = callbackRecords.Length,
                    callbackDelta = callbackRecords.Length - callbackCountBefore,
                    exceptionType = exceptionType,
                    pause = effect.pause,
                    playing = state.playing,
                    loopState = state.loopState.ToString(),
                    spawnCount = state.spawnCount,
                    deltaTime = state.deltaTime,
                    totalTime = state.totalTime,
                    timeFrameCount = Time.frameCount
                });
            }
        }

        private static string InvokeManualAction(Action action)
        {
            try
            {
                action();
                return string.Empty;
            }
            catch (Exception exception)
            {
                return exception.GetType().FullName;
            }
        }

        private void CaptureOutputEvent(
            string scenario,
            uint seed,
            int sequence,
            VFXOutputEventArgs args)
        {
            VFXEventAttribute attributes = args.eventAttribute;
            bool hasSize = attributes.HasFloat("size");
            bool hasPosition = attributes.HasVector3("position");
            bool hasMeshIndex = attributes.HasUint("meshIndex");
            bool hasAlive = attributes.HasBool("alive");
            bool hasSpawnTime = attributes.HasFloat("spawnTime");
            bool hasSpawnCount = attributes.HasFloat("spawnCount");
            Vector3 position = hasPosition ? attributes.GetVector3("position") : Vector3.zero;
            _report.outputEvents.Add(new ProbeOutputEventRecord
            {
                scenario = scenario,
                seed = seed.ToString(CultureInfo.InvariantCulture),
                sequence = sequence,
                eventNameId = args.nameId,
                hasSize = hasSize,
                size = hasSize ? attributes.GetFloat("size") : 0f,
                hasPosition = hasPosition,
                positionX = position.x,
                positionY = position.y,
                positionZ = position.z,
                hasMeshIndex = hasMeshIndex,
                meshIndex = hasMeshIndex ? attributes.GetUint("meshIndex") : 0u,
                hasAlive = hasAlive,
                alive = hasAlive && attributes.GetBool("alive"),
                hasSpawnTime = hasSpawnTime,
                spawnTime = hasSpawnTime ? attributes.GetFloat("spawnTime") : 0f,
                hasSpawnCount = hasSpawnCount,
                spawnCount = hasSpawnCount ? attributes.GetFloat("spawnCount") : 0f
            });
        }

        private void Capture(
            string scenario,
            uint seed,
            string phase,
            int frame,
            VisualEffect effect,
            string spawnSystem,
            string particleSystem)
        {
            using (VFXSpawnerState state = effect.GetSpawnSystemInfo(spawnSystem))
            {
                VFXParticleSystemInfo particle = effect.GetParticleSystemInfo(particleSystem);
                VFXEventAttribute attributes = state.vfxEventAttribute;
                bool hasSize = attributes.HasFloat("size");
                bool hasPosition = attributes.HasVector3("position");
                bool hasMeshIndex = attributes.HasUint("meshIndex");
                bool hasAlive = attributes.HasBool("alive");
                bool hasSpawnTime = attributes.HasFloat("spawnTime");
                bool hasEventSpawnCount = attributes.HasFloat("spawnCount");
                Vector3 position = hasPosition ? attributes.GetVector3("position") : Vector3.zero;
                _report.records.Add(new ProbeRecord
                {
                    scenario = scenario,
                    seed = seed.ToString(CultureInfo.InvariantCulture),
                    phase = phase,
                    frame = frame,
                    spawnSystem = spawnSystem,
                    particleSystem = particleSystem,
                    playing = state.playing,
                    newLoop = state.newLoop,
                    loopState = state.loopState.ToString(),
                    spawnCount = state.spawnCount,
                    deltaTime = state.deltaTime,
                    totalTime = state.totalTime,
                    delayBeforeLoop = state.delayBeforeLoop,
                    loopDuration = state.loopDuration,
                    delayAfterLoop = state.delayAfterLoop,
                    loopIndex = state.loopIndex,
                    loopCount = state.loopCount,
                    aliveCount = particle.aliveCount,
                    capacity = particle.capacity,
                    sleeping = particle.sleeping,
                    hasSize = hasSize,
                    eventSize = hasSize ? attributes.GetFloat("size") : 0f,
                    hasPosition = hasPosition,
                    eventPositionX = position.x,
                    eventPositionY = position.y,
                    eventPositionZ = position.z,
                    hasMeshIndex = hasMeshIndex,
                    eventMeshIndex = hasMeshIndex ? attributes.GetUint("meshIndex") : 0u,
                    hasAlive = hasAlive,
                    eventAlive = hasAlive && attributes.GetBool("alive"),
                    hasSpawnTime = hasSpawnTime,
                    eventSpawnTime = hasSpawnTime ? attributes.GetFloat("spawnTime") : 0f,
                    hasEventSpawnCount = hasEventSpawnCount,
                    eventSpawnCount = hasEventSpawnCount ? attributes.GetFloat("spawnCount") : 0f
                });
            }
        }

        private static void ActivateVfx()
        {
            PropertyInfo property = typeof(VFXManager).GetProperty(
                "activateVFX", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
                property.SetValue(null, true, null);
        }

        private static List<UnityRandomSample> CaptureUnityRandomSamples()
        {
            uint[] seeds = { 1, 2, 17, 991, uint.MaxValue };
            UnityEngine.Random.State saved = UnityEngine.Random.state;
            var samples = new List<UnityRandomSample>(seeds.Length);
            try
            {
                foreach (uint seed in seeds)
                {
                    UnityEngine.Random.InitState(unchecked((int)seed));
                    UnityEngine.Random.State initialized = UnityEngine.Random.state;
                    uint[] stateWords = typeof(UnityEngine.Random.State)
                        .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .OrderBy(field => field.MetadataToken)
                        .Select(field => unchecked((uint)(int)field.GetValue(initialized)))
                        .ToArray();
                    if (stateWords.Length != 4)
                        throw new InvalidDataException(
                            $"Unity Random.State expected four words; found {stateWords.Length}.");
                    var values = new float[12];
                    var statesAfterValue = new uint[values.Length * 4];
                    for (int index = 0; index < values.Length; index++)
                    {
                        values[index] = UnityEngine.Random.value;
                        uint[] state = typeof(UnityEngine.Random.State)
                            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .OrderBy(field => field.MetadataToken)
                            .Select(field => unchecked((uint)(int)field.GetValue(UnityEngine.Random.state)))
                            .ToArray();
                        Array.Copy(state, 0, statesAfterValue, index * 4, 4);
                    }
                    samples.Add(new UnityRandomSample
                    {
                        seed = seed.ToString(CultureInfo.InvariantCulture),
                        initialState = stateWords,
                        statesAfterValue = statesAfterValue,
                        values = values
                    });
                }
            }
            finally
            {
                UnityEngine.Random.state = saved;
            }
            return samples;
        }

        private static string CommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < args.Length; index++)
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                    return args[index + 1];
            return null;
        }

        private static void Fail(Exception exception)
        {
            Debug.LogException(exception);
            Application.Quit(1);
        }

        private sealed class Scenario
        {
            public Scenario(string name, uint[] seeds, int frameCount)
            {
                Name = name;
                Seeds = seeds;
                FrameCount = frameCount;
            }

            public string Name { get; }
            public uint[] Seeds { get; }
            public int FrameCount { get; }
        }

        [Serializable]
        private sealed class ProbeReport
        {
            public string editorVersion;
            public string visualEffectGraphVersion;
            public float fixedDeltaTime;
            public float vfxFixedTimeStep;
            public float vfxMaxDeltaTime;
            public bool resetSeedOnPlay;
            public string graphicsDevice;
            public List<UnityRandomSample> unityRandomSamples;
            public List<ProbeRecord> records;
            public List<ProbeOutputEventRecord> outputEvents;
            public List<ProbeCallbackRecord> callbackRecords;
            public List<ProbeBuiltInRecord> builtInRecords;
            public List<ProbeManualControlRecord> manualControlRecords;
        }

        [Serializable]
        private sealed class ProbeManualControlRecord
        {
            public string action;
            public string phase;
            public int callbackCount;
            public int callbackDelta;
            public string exceptionType;
            public bool pause;
            public bool playing;
            public string loopState;
            public float spawnCount;
            public float deltaTime;
            public float totalTime;
            public int timeFrameCount;
        }

        [Serializable]
        private sealed class UnityRandomSample
        {
            public string seed;
            public uint[] initialState;
            public uint[] statesAfterValue;
            public float[] values;
        }

        [Serializable]
        private sealed class ProbeRecord
        {
            public string scenario;
            public string seed;
            public string phase;
            public int frame;
            public string spawnSystem;
            public string particleSystem;
            public bool playing;
            public bool newLoop;
            public string loopState;
            public float spawnCount;
            public float deltaTime;
            public float totalTime;
            public float delayBeforeLoop;
            public float loopDuration;
            public float delayAfterLoop;
            public int loopIndex;
            public int loopCount;
            public uint aliveCount;
            public uint capacity;
            public bool sleeping;
            public bool hasSize;
            public float eventSize;
            public bool hasPosition;
            public float eventPositionX;
            public float eventPositionY;
            public float eventPositionZ;
            public bool hasMeshIndex;
            public uint eventMeshIndex;
            public bool hasAlive;
            public bool eventAlive;
            public bool hasSpawnTime;
            public float eventSpawnTime;
            public bool hasEventSpawnCount;
            public float eventSpawnCount;
        }

        [Serializable]
        private sealed class ProbeOutputEventRecord
        {
            public string scenario;
            public string seed;
            public int sequence;
            public int eventNameId;
            public bool hasSize;
            public float size;
            public bool hasPosition;
            public float positionX;
            public float positionY;
            public float positionZ;
            public bool hasMeshIndex;
            public uint meshIndex;
            public bool hasAlive;
            public bool alive;
            public bool hasSpawnTime;
            public float spawnTime;
            public bool hasSpawnCount;
            public float spawnCount;
        }
    }
}
