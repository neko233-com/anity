using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

namespace Anity
{
    public static class VFXSpawnerProbe
    {
        private const string GeneratedFolder = "Assets/Resources/Generated";
        private const string FloatGuid = "f780aa281814f9842a7c076d436932e7";
        private const string Float2Guid = "1b2b751071c7fc14f9fa503163991826";
        private const string Float3Guid = "ac39bd03fca81b849929b9c966f1836a";
        private const string IntGuid = "4d246e354feb93041a837a9ef59437cb";
        private const string UintGuid = "c52d920e7fff73b498050a6b3c4404ca";
        private const string BoolGuid = "b4c11ff25089a324daf359f4b0629b33";
        private const string Matrix4x4Guid = "30cf2e25945865b43b7bf617cb60e203";
        private const string TransformGuid = "3e3f628d80ffceb489beac74258f9cf7";
        private const string DynamicBuiltInGuid = "a72fbb93ebe17974e90a144ef2ec8ceb";
        private const string SpawnerSetAttributeGuid = "709ca816312218f4ba70763d893c34c9";
        private const string SpawnerCustomWrapperGuid = "4bfc68bea08ee074899e288b438a2e89";
        private const string ProbeCallbackScriptGuid = "b5f0b73fc8cb44eb9b6bc9eadb50ab77";
        private const string BuiltInProbeCallbackScriptGuid = "f4d96e678fbc4a49a624b693a8eeb314";

        public static void Run()
        {
            try
            {
                string buildPath = CommandLineValue("-anityBuildPath");
                if (string.IsNullOrEmpty(buildPath))
                    throw new InvalidOperationException("-anityBuildPath is required.");

                EnsureGeneratedFolder();
                EnsureUrp();
                ActivateVfxForBatchMode();

                string templatePath = Path.Combine(
                    EditorApplication.applicationContentsPath,
                    "Resources/PackageManager/BuiltInPackages/com.unity.visualeffectgraph/Editor/Templates/SimpleParticleSystem.vfx");
                if (!File.Exists(templatePath))
                    throw new FileNotFoundException("Unity VFX template is unavailable.", templatePath);
                string template = File.ReadAllText(templatePath);

                var scenarios = new[]
                {
                    new Scenario("infinite", 0, null, 0, null, 0, null, 0, null,
                        new uint[] { 1 }, 8),
                    new Scenario("constant_finite", 1, Operand.Float(0.25f),
                        1, Operand.Int(2), 1, Operand.Float(0.125f),
                        1, Operand.Float(0.125f), new uint[] { 1 }, 24),
                    new Scenario("zero_count", 1, Operand.Float(0.25f),
                        1, Operand.Int(0), 0, null, 0, null,
                        new uint[] { 1 }, 4),
                    new Scenario("random_count", 1, Operand.Float(0.125f),
                        2, Operand.Float2(1.25f, 3.75f), 0, null, 0, null,
                        new uint[] { 1, 2, 17, 991, uint.MaxValue }, 16),
                    new Scenario("random_all", 2, Operand.Float2(0.125f, 0.375f),
                        2, Operand.Float2(1.25f, 3.75f),
                        2, Operand.Float2(0.0625f, 0.1875f),
                        2, Operand.Float2(0.0625f, 0.1875f),
                        new uint[] { 1, 2, 17, 991, uint.MaxValue }, 48),
                    new Scenario("set_scalar_order", 0, null, 0, null, 0, null, 0, null,
                        new uint[] { 1 }, 4,
                        new[]
                        {
                            SetAttributeSpec.Off("size", Operand.Float(-4.5f)),
                            SetAttributeSpec.Off("size", Operand.Float(7.25f)),
                            SetAttributeSpec.Off("meshIndex", Operand.Uint(4294967294u)),
                            SetAttributeSpec.Off("alive", Operand.Bool(true)),
                            SetAttributeSpec.Off("spawnTime", Operand.Float(42.5f))
                        }),
                    new Scenario("set_vector_off", 0, null, 0, null, 0, null, 0, null,
                        new uint[] { 1 }, 4,
                        new[]
                        {
                            SetAttributeSpec.Off("position", Operand.Float3(-1f, 2.25f, 3.5f))
                        }),
                    new Scenario("set_random_per_component", 0, null, 0, null, 0, null, 0, null,
                        new uint[] { 1, 2, 17, 991, uint.MaxValue }, 4,
                        new[]
                        {
                            SetAttributeSpec.Random("position", 1,
                                Operand.Float3(-1f, 0.25f, 0.5f),
                                Operand.Float3(2f, 0.75f, 1.5f))
                        }),
                    new Scenario("set_random_uniform", 0, null, 0, null, 0, null, 0, null,
                        new uint[] { 1, 2, 17, 991, uint.MaxValue }, 4,
                        new[]
                        {
                            SetAttributeSpec.Random("position", 2,
                                Operand.Float3(-1f, 0.25f, 0.5f),
                                Operand.Float3(2f, 0.75f, 1.5f))
                        }),
                    new Scenario("set_spawn_count_after_rate", 0, null, 0, null, 0, null, 0, null,
                        new uint[] { 1 }, 8,
                        new[] { SetAttributeSpec.Off("spawnCount", Operand.Float(3f)) }),
                    new Scenario("set_spawn_count_before_rate", 0, null, 0, null, 0, null, 0, null,
                        new uint[] { 1 }, 8,
                        new[] { SetAttributeSpec.Off("spawnCount", Operand.Float(3f)) },
                        setAttributesBeforeExistingBlocks: true),
                    new Scenario("callback_after_rate", 0, null, 0, null, 0, null, 0, null,
                        new uint[] { 1 }, 4,
                        new[] { SetAttributeSpec.Off("size", Operand.Float(-1f)) },
                        setAttributesBeforeExistingBlocks: true,
                        customCallback: new CustomCallbackSpec(2f, 100f)),
                    new Scenario("callback_before_rate", 0, null, 0, null, 0, null, 0, null,
                        new uint[] { 1 }, 4,
                        new[] { SetAttributeSpec.Off("size", Operand.Float(-1f)) },
                        setAttributesBeforeExistingBlocks: true,
                        customCallback: new CustomCallbackSpec(2f, 200f),
                        customCallbackBeforeExistingBlocks: true),
                    new Scenario("callback_builtins", 0, null, 0, null, 0, null, 0, null,
                        new uint[] { 17 }, 4,
                        builtInCallback: true)
                };

                foreach (Scenario scenario in scenarios)
                {
                    string assetPath = $"{GeneratedFolder}/{scenario.Name}.vfx";
                    File.WriteAllText(assetPath, InjectScenario(template, scenario));
                    EnsureVfxMeta(assetPath, scenario.AssetGuid);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (Scenario scenario in scenarios)
                {
                    string assetPath = $"{GeneratedFolder}/{scenario.Name}.vfx";
                    AssetDatabase.ImportAsset(
                        assetPath,
                        ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }
                AssetDatabase.SaveAssets();
                ActivateVfxForBatchMode();

                const string scenePath = GeneratedFolder + "/Probe.unity";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("VFX Spawner Player Probe").AddComponent<VFXSpawnerPlayerProbe>();
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.rotation = Quaternion.identity;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new IOException($"Unity could not save '{scenePath}'.");
                PlayerSettings.productName = "AnityVFXSpawnerProbe";
                PlayerSettings.applicationIdentifier = "com.neko233.anity.vfxspawnerprobe";
                PlayerSettings.runInBackground = true;
                PlayerSettings.SetGraphicsAPIs(
                    BuildTarget.StandaloneOSX,
                    new[] { GraphicsDeviceType.Metal });
                BuildReport build = BuildPipeline.BuildPlayer(
                    new[] { scenePath },
                    Path.GetFullPath(buildPath),
                    BuildTarget.StandaloneOSX,
                    BuildOptions.None);
                if (build.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException(
                        $"Unity Player build failed: {build.summary.result}, errors={build.summary.totalErrors}.");
                Debug.Log($"ANITY_VFX_SPAWNER_BUILD_OK {build.summary.outputPath}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureGeneratedFolder()
        {
            Directory.CreateDirectory(GeneratedFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureVfxMeta(string assetPath, string guid)
        {
            string metaPath = assetPath + ".meta";
            string expected =
                "fileFormatVersion: 2\n" +
                $"guid: {guid}\n" +
                "VisualEffectImporter:\n" +
                "  externalObjects: {}\n" +
                "  userData: \n" +
                "  assetBundleName: \n" +
                "  assetBundleVariant: \n";
            if (!File.Exists(metaPath) || !string.Equals(
                    File.ReadAllText(metaPath), expected, StringComparison.Ordinal))
                File.WriteAllText(metaPath, expected);
        }

        private static void ActivateVfxForBatchMode()
        {
            PropertyInfo activate = typeof(VFXManager).GetProperty(
                "activateVFX",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (activate == null || !activate.CanWrite)
                throw new MissingMemberException(typeof(VFXManager).FullName, "activateVFX");
            activate.SetValue(null, true, null);
            if (!(bool)activate.GetValue(null, null))
                throw new InvalidOperationException("Unity VFX could not be activated in batch mode.");
            Type managerEditor = Type.GetType(
                "VFXManagerEditor, Unity.VisualEffectGraph.Editor",
                throwOnError: true);
            MethodInfo check = managerEditor.GetMethod(
                "CheckVFXManager",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (check == null)
                throw new MissingMethodException(managerEditor.FullName, "CheckVFXManager");
            check.Invoke(null, null);
        }

        private static void EnsureUrp()
        {
            const string pipelinePath = GeneratedFolder + "/ProbeURP.asset";
            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                ScriptableRendererData renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
                if (renderer == null)
                    throw new FileNotFoundException("The URP 14 universal renderer data is unavailable.");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            GraphicsSettings.renderPipelineAsset = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();
            if (GraphicsSettings.currentRenderPipeline == null)
                throw new InvalidOperationException("The URP asset did not become the current render pipeline.");
        }

        private static void CaptureScenario(
            ProbeReport report,
            Scenario scenario,
            VisualEffectAsset asset,
            uint seed)
        {
            ActivateVfxForBatchMode();
            var gameObject = new GameObject($"probe-{scenario.Name}-{seed}");
            try
            {
                VisualEffect effect = gameObject.AddComponent<VisualEffect>();
                effect.enabled = true;
                effect.visualEffectAsset = asset;
                effect.startSeed = seed;
                effect.resetSeedOnPlay = true;
                effect.Reinit();
                effect.Play();

                var spawnNames = new List<string>();
                var particleNames = new List<string>();
                effect.GetSpawnSystemNames(spawnNames);
                effect.GetParticleSystemNames(particleNames);
                if (spawnNames.Count != 1 || particleNames.Count != 1)
                    throw new InvalidDataException(
                        $"Scenario '{scenario.Name}' expected one Spawn and one Particle system; " +
                        $"found {spawnNames.Count} and {particleNames.Count}.");

                Capture(report, scenario.Name, seed, "after_play", 0, effect,
                    spawnNames[0], particleNames[0]);
                for (int frame = 1; frame <= scenario.FrameCount; frame++)
                {
                    effect.Simulate(report.fixedDeltaTime, 1);
                    Capture(report, scenario.Name, seed, "tick", frame, effect,
                        spawnNames[0], particleNames[0]);
                }

                effect.Stop();
                Capture(report, scenario.Name, seed, "after_stop", scenario.FrameCount,
                    effect, spawnNames[0], particleNames[0]);
                effect.Play();
                Capture(report, scenario.Name, seed, "after_replay", scenario.FrameCount,
                    effect, spawnNames[0], particleNames[0]);
                effect.Simulate(report.fixedDeltaTime, 1);
                Capture(report, scenario.Name, seed, "replay_tick", scenario.FrameCount + 1,
                    effect, spawnNames[0], particleNames[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static void Capture(
            ProbeReport report,
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
                report.records.Add(new ProbeRecord
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
                    sleeping = particle.sleeping
                });
            }
        }

        private static string InjectScenario(string source, Scenario scenario)
        {
            const string settings =
                "  loopDuration: 0\n" +
                "  loopCount: 0\n" +
                "  delayBeforeLoop: 0\n" +
                "  delayAfterLoop: 0\n";
            int settingsOffset = source.IndexOf(settings, StringComparison.Ordinal);
            if (settingsOffset < 0)
                throw new InvalidDataException("The official VFX template Spawner settings were not found.");
            int documentStart = source.LastIndexOf("--- !u!114 &", settingsOffset, StringComparison.Ordinal);
            int documentEnd = source.IndexOf("--- !u!", settingsOffset + settings.Length, StringComparison.Ordinal);
            if (documentStart < 0 || documentEnd <= documentStart)
                throw new InvalidDataException("The official VFX template Spawner document is malformed.");
            int idStart = documentStart + "--- !u!114 &".Length;
            int idEnd = source.IndexOf('\n', idStart);
            long ownerId = long.Parse(source.Substring(idStart, idEnd - idStart), CultureInfo.InvariantCulture);
            const string parentPrefix = "  m_Parent: {fileID: ";
            string originalSpawnerDocument = source.Substring(documentStart, documentEnd - documentStart);
            int parentStart = originalSpawnerDocument.IndexOf(parentPrefix, StringComparison.Ordinal);
            int parentEnd = originalSpawnerDocument.IndexOf('}', parentStart + parentPrefix.Length);
            if (parentStart < 0 || parentEnd <= parentStart)
                throw new InvalidDataException("The official VFX template Spawner parent is malformed.");
            long graphId = long.Parse(originalSpawnerDocument.Substring(
                parentStart + parentPrefix.Length,
                parentEnd - parentStart - parentPrefix.Length), CultureInfo.InvariantCulture);

            var slots = new List<Slot>();
            AddSlot(slots, scenario.LoopDuration, "LoopDuration", 920001);
            AddSlot(slots, scenario.LoopCount, "LoopCount", 920002);
            AddSlot(slots, scenario.DelayBefore, "DelayBeforeLoop", 920003);
            AddSlot(slots, scenario.DelayAfter, "DelayAfterLoop", 920004);
            string slotReferences = slots.Count == 0
                ? "  m_InputSlots: []\n"
                : "  m_InputSlots:\n" + string.Concat(slots.Select(slot => $"  - {{fileID: {slot.Id}}}\n"));

            string document = originalSpawnerDocument
                .Replace("  m_InputSlots: []\n", slotReferences)
                .Replace("  loopDuration: 0\n", $"  loopDuration: {scenario.LoopDurationMode}\n")
                .Replace("  loopCount: 0\n", $"  loopCount: {scenario.LoopCountMode}\n")
                .Replace("  delayBeforeLoop: 0\n", $"  delayBeforeLoop: {scenario.DelayBeforeMode}\n")
                .Replace("  delayAfterLoop: 0\n", $"  delayAfterLoop: {scenario.DelayAfterMode}\n");
            var setAttributeBlocks = new List<SetAttributeBlock>();
            for (int index = 0; index < scenario.SetAttributes.Length; index++)
            {
                SetAttributeSpec specification = scenario.SetAttributes[index];
                long blockId = 930000 + index * 100;
                var inputSlots = new List<Slot>
                {
                    new Slot(940000 + index * 100, specification.RandomMode == 0
                        ? specification.Attribute
                        : "Min", specification.Minimum)
                };
                if (specification.RandomMode != 0)
                    inputSlots.Add(new Slot(940001 + index * 100, "Max", specification.Maximum));
                setAttributeBlocks.Add(new SetAttributeBlock(blockId, specification, inputSlots));
            }
            CustomCallbackBlock customCallbackBlock = null;
            DynamicBuiltInNode dynamicBuiltInNode = null;
            if (scenario.CustomCallback != null)
            {
                customCallbackBlock = new CustomCallbackBlock(955000, scenario.CustomCallback,
                    new[]
                    {
                        new Slot(960000, "SpawnDelta", Operand.Float(scenario.CustomCallback.SpawnDelta)),
                        new Slot(960001, "Marker", Operand.Float(scenario.CustomCallback.Marker))
                    });
            }
            else if (scenario.BuiltInCallback)
            {
                string[] names =
                {
                    "VfxDeltaTime", "VfxUnscaledDeltaTime", "VfxTotalTime", "VfxFrameIndex",
                    "VfxPlayRate", "VfxManagerFixedTimeStep", "VfxManagerMaxDeltaTime",
                    "GameDeltaTime", "GameUnscaledDeltaTime", "GameSmoothDeltaTime",
                    "GameTotalTime", "GameUnscaledTotalTime", "GameTotalTimeSinceSceneLoad",
                    "GameTimeScale", "LocalToWorld", "WorldToLocal", "SystemSeed"
                };
                Operand[] operands =
                {
                    Operand.Float(0f), Operand.Float(0f), Operand.Float(0f), Operand.Uint(0),
                    Operand.Float(0f), Operand.Float(0f), Operand.Float(0f),
                    Operand.Float(0f), Operand.Float(0f), Operand.Float(0f), Operand.Float(0f),
                    Operand.Float(0f), Operand.Float(0f), Operand.Float(0f),
                    Operand.MatrixIdentity(), Operand.MatrixIdentity(), Operand.Uint(0)
                };
                Operand[] outputOperands =
                {
                    Operand.Float(0f), Operand.Float(0f), Operand.Float(0f), Operand.Uint(0),
                    Operand.Float(0f), Operand.Float(0f), Operand.Float(0f),
                    Operand.Float(0f), Operand.Float(0f), Operand.Float(0f), Operand.Float(0f),
                    Operand.Float(0f), Operand.Float(0f), Operand.Float(0f),
                    Operand.TransformIdentity(), Operand.TransformIdentity(), Operand.Uint(0)
                };
                var callbackInputs = new List<Slot>(names.Length);
                var builtInOutputs = new List<Slot>(names.Length);
                for (int index = 0; index < names.Length; index++)
                {
                    long inputId = 960000 + index;
                    long outputId = 971000 + index;
                    callbackInputs.Add(new Slot(inputId, names[index], operands[index], 0, outputId));
                    builtInOutputs.Add(new Slot(outputId, names[index], outputOperands[index], 1, inputId));
                }
                customCallbackBlock = new CustomCallbackBlock(955000, null, callbackInputs, true);
                dynamicBuiltInNode = new DynamicBuiltInNode(970000, builtInOutputs);
            }
            string setReferences = string.Concat(setAttributeBlocks.Select(block =>
                $"  - {{fileID: {block.Id}}}\n"));
            string callbackReference = customCallbackBlock == null
                ? string.Empty
                : $"  - {{fileID: {customCallbackBlock.Id}}}\n";
            string beforeReferences =
                (scenario.CustomCallbackBeforeExistingBlocks ? callbackReference : string.Empty) +
                (scenario.SetAttributesBeforeExistingBlocks ? setReferences : string.Empty);
            string afterReferences =
                (!scenario.SetAttributesBeforeExistingBlocks ? setReferences : string.Empty) +
                (!scenario.CustomCallbackBeforeExistingBlocks ? callbackReference : string.Empty);
            if (beforeReferences.Length > 0)
                document = document.Replace(
                    "  m_Children:\n",
                    "  m_Children:\n" + beforeReferences);
            if (afterReferences.Length > 0)
            {
                int childEnd = document.IndexOf("  m_UIPosition:", StringComparison.Ordinal);
                if (childEnd < 0)
                    throw new InvalidDataException("The official VFX template Spawner child list is malformed.");
                document = document.Insert(childEnd, afterReferences);
            }
            if (setAttributeBlocks.Count > 0)
            {
                const string outputFlowMarker = "  m_OutputFlowSlot:\n  - link:\n";
                if (!document.Contains(outputFlowMarker))
                    throw new InvalidDataException("The official VFX template Spawner output flow is malformed.");
                document = document.Replace(
                    outputFlowMarker,
                    outputFlowMarker +
                    "    - context: {fileID: 950000}\n" +
                    "      slotIndex: 0\n");
            }
            var result = new StringBuilder(source.Length +
                (slots.Count + setAttributeBlocks.Sum(block => block.InputSlots.Count)) * 1200);
            string prefix = source.Substring(0, documentStart);
            if (setAttributeBlocks.Count > 0)
                prefix = InjectGraphChild(prefix, graphId, 950000);
            if (dynamicBuiltInNode != null)
                prefix = InjectGraphChild(prefix, graphId, dynamicBuiltInNode.Id);
            result.Append(prefix);
            result.Append(document);
            result.Append(source, documentEnd, source.Length - documentEnd);
            foreach (Slot slot in slots)
                result.Append(SlotDocument(slot, ownerId));
            foreach (SetAttributeBlock block in setAttributeBlocks)
            {
                result.Append(SetAttributeBlockDocument(block, ownerId));
                foreach (Slot slot in block.InputSlots)
                    result.Append(SlotDocument(slot, block.Id));
            }
            if (customCallbackBlock != null)
            {
                result.Append(CustomCallbackBlockDocument(customCallbackBlock, ownerId));
                foreach (Slot slot in customCallbackBlock.InputSlots)
                    result.Append(SlotDocument(slot, customCallbackBlock.Id));
            }
            if (dynamicBuiltInNode != null)
            {
                result.Append(DynamicBuiltInDocument(dynamicBuiltInNode, graphId));
                foreach (Slot slot in dynamicBuiltInNode.OutputSlots)
                    result.Append(SlotDocument(slot, dynamicBuiltInNode.Id));
            }
            if (setAttributeBlocks.Count > 0)
            {
                result.Append(OutputEventContextDocument(950000, 950001, graphId, ownerId));
                result.Append(OutputEventDataDocument(950001, 950000));
            }
            return result.ToString();
        }

        private static string InjectGraphChild(string prefix, long graphId, long childId)
        {
            string marker = $"--- !u!114 &{graphId}\n";
            int start = prefix.IndexOf(marker, StringComparison.Ordinal);
            int end = prefix.IndexOf("--- !u!", start + marker.Length, StringComparison.Ordinal);
            if (start < 0 || end <= start)
                throw new InvalidDataException("The official VFX template Graph document is malformed.");
            string document = prefix.Substring(start, end - start);
            int childEnd = document.IndexOf("  m_UIPosition:", StringComparison.Ordinal);
            if (childEnd < 0)
                throw new InvalidDataException("The official VFX template Graph child list is malformed.");
            document = document.Insert(childEnd, $"  - {{fileID: {childId}}}\n");
            return prefix.Substring(0, start) + document + prefix.Substring(end);
        }

        private static string OutputEventContextDocument(
            long contextId,
            long dataId,
            long graphId,
            long spawnerId)
            => $"--- !u!114 &{contextId}\nMonoBehaviour:\n" +
               "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
               "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
               "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
               "  m_Script: {fileID: 11500000, guid: 4f39de6f4fce95c4d9240e5055b057a6, type: 3}\n" +
               "  m_Name: \n  m_EditorClassIdentifier: \n  m_UIIgnoredErrors: []\n" +
               $"  m_Parent: {{fileID: {graphId}}}\n  m_Children: []\n" +
               "  m_UIPosition: {x: 1500, y: -190}\n  m_UICollapsed: 0\n  m_UISuperCollapsed: 0\n" +
               "  m_InputSlots: []\n  m_OutputSlots: []\n  m_Label: \n" +
               $"  m_Data: {{fileID: {dataId}}}\n  m_InputFlowSlot:\n  - link:\n" +
               $"    - context: {{fileID: {spawnerId}}}\n      slotIndex: 0\n" +
               "  m_OutputFlowSlot: []\n  eventName: Anity SetAttribute Probe\n";

        private static string OutputEventDataDocument(long dataId, long contextId)
            => $"--- !u!114 &{dataId}\nMonoBehaviour:\n" +
               "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
               "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
               "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
               "  m_Script: {fileID: 11500000, guid: c56fa986310a594418ab6f35c4dbc51c, type: 3}\n" +
               "  m_Name: \n  m_EditorClassIdentifier: \n  m_UIIgnoredErrors: []\n" +
               "  m_Parent: {fileID: 0}\n  m_Children: []\n" +
               "  m_UIPosition: {x: 0, y: 0}\n  m_UICollapsed: 1\n  m_UISuperCollapsed: 0\n" +
               "  title: \n  m_Owners:\n" +
               $"  - {{fileID: {contextId}}}\n";

        private static string SetAttributeBlockDocument(SetAttributeBlock block, long ownerId)
        {
            string inputSlots = string.Concat(block.InputSlots.Select(slot =>
                $"  - {{fileID: {slot.Id}}}\n"));
            return $"--- !u!114 &{block.Id}\nMonoBehaviour:\n" +
                   "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                   "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
                   "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                   $"  m_Script: {{fileID: 11500000, guid: {SpawnerSetAttributeGuid}, type: 3}}\n" +
                   $"  m_Name: Set SpawnEvent {block.Specification.Attribute}\n" +
                   "  m_EditorClassIdentifier: \n  m_UIIgnoredErrors: []\n" +
                   $"  m_Parent: {{fileID: {ownerId}}}\n  m_Children: []\n" +
                   "  m_UIPosition: {x: 0, y: 0}\n  m_UICollapsed: 0\n  m_UISuperCollapsed: 0\n" +
                   "  m_InputSlots:\n" + inputSlots +
                   "  m_OutputSlots: []\n  m_Disabled: 0\n" +
                   $"  attribute: {block.Specification.Attribute}\n" +
                   $"  randomMode: {block.Specification.RandomMode}\n";
        }

        private static string CustomCallbackBlockDocument(CustomCallbackBlock block, long ownerId)
        {
            string inputSlots = string.Concat(block.InputSlots.Select(slot =>
                $"  - {{fileID: {slot.Id}}}\n"));
            return $"--- !u!114 &{block.Id}\nMonoBehaviour:\n" +
                   "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                   "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
                   "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                   $"  m_Script: {{fileID: 11500000, guid: {SpawnerCustomWrapperGuid}, type: 3}}\n" +
                   "  m_Name: Probe Spawner Callback\n  m_EditorClassIdentifier: \n  m_UIIgnoredErrors: []\n" +
                   $"  m_Parent: {{fileID: {ownerId}}}\n  m_Children: []\n" +
                   "  m_UIPosition: {x: 0, y: 0}\n  m_UICollapsed: 0\n  m_UISuperCollapsed: 0\n" +
                   "  m_InputSlots:\n" + inputSlots +
                   "  m_OutputSlots: []\n  m_Disabled: 0\n" +
                   "  m_customType:\n" +
                   (block.IsBuiltIn
                       ? "    m_SerializableType: Anity.VFXBuiltInProbeSpawnerCallbacks, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n"
                       : "    m_SerializableType: Anity.VFXProbeSpawnerCallbacks, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n") +
                   $"  m_customScript: {{fileID: 11500000, guid: {(block.IsBuiltIn ? BuiltInProbeCallbackScriptGuid : ProbeCallbackScriptGuid)}, type: 3}}\n";
        }

        private static string DynamicBuiltInDocument(DynamicBuiltInNode node, long graphId)
        {
            string outputs = string.Concat(node.OutputSlots.Select(slot =>
                $"  - {{fileID: {slot.Id}}}\n"));
            return $"--- !u!114 &{node.Id}\nMonoBehaviour:\n" +
                   "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                   "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
                   "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                   $"  m_Script: {{fileID: 11500000, guid: {DynamicBuiltInGuid}, type: 3}}\n" +
                   "  m_Name: \n  m_EditorClassIdentifier: \n  m_UIIgnoredErrors: []\n" +
                   $"  m_Parent: {{fileID: {graphId}}}\n  m_Children: []\n" +
                   "  m_UIPosition: {x: 400, y: -250}\n  m_UICollapsed: 0\n  m_UISuperCollapsed: 0\n" +
                   "  m_InputSlots: []\n  m_OutputSlots:\n" + outputs +
                   "  m_BuiltInParameters: 131071\n";
        }

        private static void AddSlot(List<Slot> slots, Operand operand, string propertyName, long id)
        {
            if (operand != null)
                slots.Add(new Slot(id, propertyName, operand));
        }

        private static string SlotDocument(Slot slot, long ownerId)
        {
            string guid;
            string type;
            string serialized;
            switch (slot.Operand.Kind)
            {
                case OperandKind.Float:
                    guid = FloatGuid;
                    type = "System.Single, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
                    serialized = Format(slot.Operand.Minimum);
                    break;
                case OperandKind.Int:
                    guid = IntGuid;
                    type = "System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
                    serialized = ((int)slot.Operand.Minimum).ToString(CultureInfo.InvariantCulture);
                    break;
                case OperandKind.Float2:
                    guid = Float2Guid;
                    type = "UnityEngine.Vector2, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
                    serialized = $"'{{\"x\":{Format(slot.Operand.Minimum)},\"y\":{Format(slot.Operand.Maximum)}}}'";
                    break;
                case OperandKind.Float3:
                    guid = Float3Guid;
                    type = "UnityEngine.Vector3, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
                    serialized = $"'{{\"x\":{Format(slot.Operand.X)},\"y\":{Format(slot.Operand.Y)},\"z\":{Format(slot.Operand.Z)}}}'";
                    break;
                case OperandKind.Uint:
                    guid = UintGuid;
                    type = "System.UInt32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
                    serialized = slot.Operand.UintValue.ToString(CultureInfo.InvariantCulture);
                    break;
                case OperandKind.Bool:
                    guid = BoolGuid;
                    type = "System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
                    serialized = slot.Operand.BoolValue ? "True" : "False";
                    break;
                case OperandKind.Matrix4x4:
                    guid = Matrix4x4Guid;
                    type = "UnityEngine.Matrix4x4, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
                    serialized = "'{}'";
                    break;
                case OperandKind.Transform:
                    return TransformSlotDocument(slot, ownerId);
                default:
                    throw new ArgumentOutOfRangeException();
            }

            int componentCount = slot.Operand.Kind == OperandKind.Float2 ? 2 :
                slot.Operand.Kind == OperandKind.Float3 ? 3 : 0;
            string[] componentNames = { "x", "y", "z" };
            string children = componentCount > 0
                ? "  m_Children:\n" + string.Concat(Enumerable.Range(0, componentCount).Select(index =>
                    $"  - {{fileID: {slot.Id * 10 + index + 1}}}\n"))
                : "  m_Children: []\n";
            string result = $"--- !u!114 &{slot.Id}\nMonoBehaviour:\n" +
                   "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                   "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
                   "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                   $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n" +
                   "  m_Name: \n  m_EditorClassIdentifier: \n  m_UIIgnoredErrors: []\n" +
                   "  m_Parent: {fileID: 0}\n" + children + "  m_UIPosition: {x: 0, y: 0}\n" +
                   "  m_UICollapsed: 1\n  m_UISuperCollapsed: 0\n" +
                   $"  m_MasterSlot: {{fileID: {slot.Id}}}\n  m_MasterData:\n" +
                   $"    m_Owner: {{fileID: {ownerId}}}\n    m_Value:\n      m_Type:\n" +
                   $"        m_SerializableType: {type}\n      m_SerializableObject: {serialized}\n" +
                   "    m_Space: 2147483647\n  m_Property:\n" +
                   $"    name: {slot.PropertyName}\n    m_serializedType:\n" +
                   $"      m_SerializableType: {type}\n  m_Direction: {slot.Direction}\n" +
                   LinkedSlots(slot.LinkedSlotId);
            for (int index = 0; index < componentCount; index++)
                result += VectorChildDocument(slot.Id * 10 + index + 1, slot.Id, componentNames[index], slot.Direction);
            return result;
        }

        private static string LinkedSlots(long linkedSlotId)
            => linkedSlotId == 0
                ? "  m_LinkedSlots: []\n"
                : $"  m_LinkedSlots:\n  - {{fileID: {linkedSlotId}}}\n";

        private static string TransformSlotDocument(Slot slot, long ownerId)
        {
            string transformType =
                "UnityEditor.VFX.Transform, Unity.VisualEffectGraph.Editor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
            long positionId = slot.Id * 10 + 1;
            long anglesId = slot.Id * 10 + 2;
            long scaleId = slot.Id * 10 + 3;
            string result = $"--- !u!114 &{slot.Id}\nMonoBehaviour:\n" +
                   "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                   "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
                   "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                   $"  m_Script: {{fileID: 11500000, guid: {TransformGuid}, type: 3}}\n" +
                   "  m_Name: \n  m_EditorClassIdentifier: \n  m_UIIgnoredErrors: []\n" +
                   $"  m_Parent: {{fileID: 0}}\n  m_Children:\n  - {{fileID: {positionId}}}\n" +
                   $"  - {{fileID: {anglesId}}}\n  - {{fileID: {scaleId}}}\n" +
                   "  m_UIPosition: {x: 0, y: 0}\n  m_UICollapsed: 1\n  m_UISuperCollapsed: 0\n" +
                   $"  m_MasterSlot: {{fileID: {slot.Id}}}\n  m_MasterData:\n" +
                   $"    m_Owner: {{fileID: {ownerId}}}\n    m_Value:\n      m_Type:\n" +
                   $"        m_SerializableType: {transformType}\n" +
                   "      m_SerializableObject: '{\"position\":{\"x\":0.0,\"y\":0.0,\"z\":0.0},\"angles\":{\"x\":0.0,\"y\":0.0,\"z\":0.0},\"scale\":{\"x\":1.0,\"y\":1.0,\"z\":1.0}}'\n" +
                   $"    m_Space: {(slot.PropertyName == "LocalToWorld" ? 0 : 1)}\n  m_Property:\n" +
                   $"    name: {slot.PropertyName}\n    m_serializedType:\n      m_SerializableType: {transformType}\n" +
                   $"  m_Direction: {slot.Direction}\n" + LinkedSlots(slot.LinkedSlotId);
            result += Float3ChildDocument(positionId, slot.Id, "position", slot.Direction, 0f, 0f, 0f);
            result += Float3ChildDocument(anglesId, slot.Id, "angles", slot.Direction, 0f, 0f, 0f);
            result += Float3ChildDocument(scaleId, slot.Id, "scale", slot.Direction, 1f, 1f, 1f);
            return result;
        }

        private static string Float3ChildDocument(
            long id, long masterId, string propertyName, int direction, float x, float y, float z)
        {
            string vectorType =
                "UnityEngine.Vector3, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
            string result = $"--- !u!114 &{id}\nMonoBehaviour:\n" +
                   "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                   "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
                   "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                   $"  m_Script: {{fileID: 11500000, guid: {Float3Guid}, type: 3}}\n" +
                   "  m_Name: \n  m_EditorClassIdentifier: \n  m_UIIgnoredErrors: []\n" +
                   $"  m_Parent: {{fileID: {masterId}}}\n  m_Children:\n" +
                   $"  - {{fileID: {id * 10 + 1}}}\n  - {{fileID: {id * 10 + 2}}}\n  - {{fileID: {id * 10 + 3}}}\n" +
                   "  m_UIPosition: {x: 0, y: 0}\n  m_UICollapsed: 1\n  m_UISuperCollapsed: 0\n" +
                   $"  m_MasterSlot: {{fileID: {masterId}}}\n  m_MasterData:\n" +
                   "    m_Owner: {fileID: 0}\n    m_Value:\n      m_Type:\n        m_SerializableType: \n" +
                   "      m_SerializableObject: \n    m_Space: 2147483647\n  m_Property:\n" +
                   $"    name: {propertyName}\n    m_serializedType:\n      m_SerializableType: {vectorType}\n" +
                   $"  m_Direction: {direction}\n  m_LinkedSlots: []\n";
            result += VectorChildDocument(id * 10 + 1, masterId, "x", direction);
            result += VectorChildDocument(id * 10 + 2, masterId, "y", direction);
            result += VectorChildDocument(id * 10 + 3, masterId, "z", direction);
            return result;
        }

        private static string VectorChildDocument(long id, long masterId, string propertyName, int direction)
        {
            const string floatType =
                "System.Single, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
            return $"--- !u!114 &{id}\nMonoBehaviour:\n" +
                   "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                   "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
                   "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                   $"  m_Script: {{fileID: 11500000, guid: {FloatGuid}, type: 3}}\n" +
                   "  m_Name: \n  m_EditorClassIdentifier: \n  m_UIIgnoredErrors: []\n" +
                   $"  m_Parent: {{fileID: {masterId}}}\n  m_Children: []\n" +
                   "  m_UIPosition: {x: 0, y: 0}\n  m_UICollapsed: 1\n  m_UISuperCollapsed: 0\n" +
                   $"  m_MasterSlot: {{fileID: {masterId}}}\n  m_MasterData:\n" +
                   "    m_Owner: {fileID: 0}\n    m_Value:\n      m_Type:\n" +
                   "        m_SerializableType: \n      m_SerializableObject: \n" +
                   "    m_Space: 2147483647\n  m_Property:\n" +
                   $"    name: {propertyName}\n    m_serializedType:\n" +
                   $"      m_SerializableType: {floatType}\n  m_Direction: {direction}\n  m_LinkedSlots: []\n";
        }

        private static string Format(float value)
            => value.ToString("R", CultureInfo.InvariantCulture);

        private static string CommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < args.Length; index++)
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                    return args[index + 1];
            return null;
        }

        private sealed class Scenario
        {
            public Scenario(
                string name,
                int loopDurationMode,
                Operand loopDuration,
                int loopCountMode,
                Operand loopCount,
                int delayBeforeMode,
                Operand delayBefore,
                int delayAfterMode,
                Operand delayAfter,
                uint[] seeds,
                int frameCount,
                SetAttributeSpec[] setAttributes = null,
                bool setAttributesBeforeExistingBlocks = false,
                CustomCallbackSpec customCallback = null,
                bool customCallbackBeforeExistingBlocks = false,
                bool builtInCallback = false)
            {
                Name = name;
                AssetGuid = name switch
                {
                    "infinite" => "2efeae0edea9649488ef15feb1889db5",
                    "constant_finite" => "33e9c76d279d44521af663f71d55015b",
                    "zero_count" => "e151dc784310f40f6952e55dcefa4967",
                    "random_count" => "db950925c067a42398c75d24e4abf6c1",
                    "random_all" => "4065ad7c86ead4533a4b3a7dea407e87",
                    "set_scalar_order" => "7c0f163be84740839d51b5cb37f87e5a",
                    "set_vector_off" => "a8a27bf93a834be39e40bd517ea99af1",
                    "set_random_per_component" => "b67ff4fef9314a39971102b6ac8f69b9",
                    "set_random_uniform" => "03ecce67d95444fcb81a0437ab1a99c8",
                    "set_spawn_count_after_rate" => "1f5f1adaebd74b8c8d45b3ff6760c395",
                    "set_spawn_count_before_rate" => "d9bfca2713944e438129e4d674361d36",
                    "callback_after_rate" => "780306b550424cd6978aed790f3ba869",
                    "callback_before_rate" => "ac06923e2360499b94a019b7d3c48b5c",
                    "callback_builtins" => "671e9fbde5e643ca8825420c882899f4",
                    _ => throw new ArgumentOutOfRangeException(nameof(name))
                };
                LoopDurationMode = loopDurationMode;
                LoopDuration = loopDuration;
                LoopCountMode = loopCountMode;
                LoopCount = loopCount;
                DelayBeforeMode = delayBeforeMode;
                DelayBefore = delayBefore;
                DelayAfterMode = delayAfterMode;
                DelayAfter = delayAfter;
                Seeds = seeds;
                FrameCount = frameCount;
                SetAttributes = setAttributes ?? Array.Empty<SetAttributeSpec>();
                SetAttributesBeforeExistingBlocks = setAttributesBeforeExistingBlocks;
                CustomCallback = customCallback;
                CustomCallbackBeforeExistingBlocks = customCallbackBeforeExistingBlocks;
                BuiltInCallback = builtInCallback;
            }

            public string Name { get; }
            public string AssetGuid { get; }
            public int LoopDurationMode { get; }
            public Operand LoopDuration { get; }
            public int LoopCountMode { get; }
            public Operand LoopCount { get; }
            public int DelayBeforeMode { get; }
            public Operand DelayBefore { get; }
            public int DelayAfterMode { get; }
            public Operand DelayAfter { get; }
            public uint[] Seeds { get; }
            public int FrameCount { get; }
            public SetAttributeSpec[] SetAttributes { get; }
            public bool SetAttributesBeforeExistingBlocks { get; }
            public CustomCallbackSpec CustomCallback { get; }
            public bool CustomCallbackBeforeExistingBlocks { get; }
            public bool BuiltInCallback { get; }
        }

        private sealed class CustomCallbackSpec
        {
            public CustomCallbackSpec(float spawnDelta, float marker)
            {
                SpawnDelta = spawnDelta;
                Marker = marker;
            }

            public float SpawnDelta { get; }
            public float Marker { get; }
        }

        private enum OperandKind { Float, Float2, Float3, Int, Uint, Bool, Matrix4x4, Transform }

        private sealed class Operand
        {
            private Operand(
                OperandKind kind,
                float minimum,
                float maximum,
                float x = 0f,
                float y = 0f,
                float z = 0f,
                uint uintValue = 0,
                bool boolValue = false)
            {
                Kind = kind;
                Minimum = minimum;
                Maximum = maximum;
                X = x;
                Y = y;
                Z = z;
                UintValue = uintValue;
                BoolValue = boolValue;
            }

            public OperandKind Kind { get; }
            public float Minimum { get; }
            public float Maximum { get; }
            public float X { get; }
            public float Y { get; }
            public float Z { get; }
            public uint UintValue { get; }
            public bool BoolValue { get; }
            public static Operand Float(float value) => new Operand(OperandKind.Float, value, value);
            public static Operand Float2(float minimum, float maximum) => new Operand(OperandKind.Float2, minimum, maximum);
            public static Operand Float3(float x, float y, float z) =>
                new Operand(OperandKind.Float3, 0f, 0f, x, y, z);
            public static Operand Int(int value) => new Operand(OperandKind.Int, value, value);
            public static Operand Uint(uint value) => new Operand(OperandKind.Uint, 0f, 0f, uintValue: value);
            public static Operand Bool(bool value) => new Operand(OperandKind.Bool, 0f, 0f, boolValue: value);
            public static Operand MatrixIdentity() => new Operand(OperandKind.Matrix4x4, 0f, 0f);
            public static Operand TransformIdentity() => new Operand(OperandKind.Transform, 0f, 0f);
        }

        private sealed class SetAttributeSpec
        {
            private SetAttributeSpec(
                string attribute,
                int randomMode,
                Operand minimum,
                Operand maximum)
            {
                Attribute = attribute;
                RandomMode = randomMode;
                Minimum = minimum;
                Maximum = maximum;
            }

            public string Attribute { get; }
            public int RandomMode { get; }
            public Operand Minimum { get; }
            public Operand Maximum { get; }

            public static SetAttributeSpec Off(string attribute, Operand value)
                => new SetAttributeSpec(attribute, 0, value, value);

            public static SetAttributeSpec Random(
                string attribute,
                int randomMode,
                Operand minimum,
                Operand maximum)
                => new SetAttributeSpec(attribute, randomMode, minimum, maximum);
        }

        private sealed class SetAttributeBlock
        {
            public SetAttributeBlock(
                long id,
                SetAttributeSpec specification,
                IReadOnlyList<Slot> inputSlots)
            {
                Id = id;
                Specification = specification;
                InputSlots = inputSlots;
            }

            public long Id { get; }
            public SetAttributeSpec Specification { get; }
            public IReadOnlyList<Slot> InputSlots { get; }
        }

        private sealed class CustomCallbackBlock
        {
            public CustomCallbackBlock(
                long id,
                CustomCallbackSpec specification,
                IReadOnlyList<Slot> inputSlots,
                bool isBuiltIn = false)
            {
                Id = id;
                Specification = specification;
                InputSlots = inputSlots;
                IsBuiltIn = isBuiltIn;
            }

            public long Id { get; }
            public CustomCallbackSpec Specification { get; }
            public IReadOnlyList<Slot> InputSlots { get; }
            public bool IsBuiltIn { get; }
        }

        private sealed class DynamicBuiltInNode
        {
            public DynamicBuiltInNode(long id, IReadOnlyList<Slot> outputSlots)
            {
                Id = id;
                OutputSlots = outputSlots;
            }

            public long Id { get; }
            public IReadOnlyList<Slot> OutputSlots { get; }
        }

        private sealed class Slot
        {
            public Slot(long id, string propertyName, Operand operand, int direction = 0, long linkedSlotId = 0)
            {
                Id = id;
                PropertyName = propertyName;
                Operand = operand;
                Direction = direction;
                LinkedSlotId = linkedSlotId;
            }

            public long Id { get; }
            public string PropertyName { get; }
            public Operand Operand { get; }
            public int Direction { get; }
            public long LinkedSlotId { get; }
        }

        [Serializable]
        private sealed class ProbeReport
        {
            public string editorVersion;
            public string visualEffectGraphVersion;
            public float fixedDeltaTime;
            public List<ProbeRecord> records;
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
        }
    }
}
