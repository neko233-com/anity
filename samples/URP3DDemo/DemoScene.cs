using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anity.Demos.URP3D
{
    public static class DemoScene
    {
        public static Texture2D CheckerTexture = null!;
        public static float TimeScaleValue = 1f;

        private static readonly List<GameObject> _coloredObjects = new();
        private static int _uiRebuildCount;
        private static int _particleCount;

        public static int PhysicsObjectCount { get; private set; }
        public static int Physics2DObjectCount { get; private set; }
        public static int ParticleCount => _particleCount;
        public static int UIRebuildCount => _uiRebuildCount;
        public static int AnimatorCount { get; private set; }
        public static int ParticleSystemCount { get; private set; }
        public static int AudioSourceCount { get; private set; }
        public static int ColliderCount { get; private set; }

        public static Scene Build()
        {
            var scene = SceneManager.CreateScene("URP3DDemo");

            _coloredObjects.Clear();
            PhysicsObjectCount = 0;
            Physics2DObjectCount = 0;
            ParticleSystemCount = 0;
            _particleCount = 0;
            AudioSourceCount = 0;
            ColliderCount = 0;
            AnimatorCount = 0;
            _uiRebuildCount = 0;

            CreateCheckerTexture();

            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0, 5, -10);
            cameraGo.transform.LookAt(Vector3.zero);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            CreateGround();
            CreatePhysicsObjects();
            Create2DPhysicsObjects();
            CreateRotatingObjects();
            CreateParticleSystems();
            CreateAnimationObjects();
            CreateAudioObjects();
            CreateTesters();

            return scene;
        }

        private static void CreateCheckerTexture()
        {
            const int size = 64;
            CheckerTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var black = Color.black;
            var white = Color.white;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBlack = ((x / 8) + (y / 8)) % 2 == 0;
                    CheckerTexture.SetPixel(x, y, isBlack ? black : white);
                }
            }
            CheckerTexture.Apply();
        }

        private static void CreateGround()
        {
            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.name = "Ground";
            groundGo.transform.position = new Vector3(0, -0.5f, 0);
            groundGo.transform.localScale = new Vector3(10, 1, 10);
            var groundRenderer = groundGo.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.3f, 0.5f, 0.3f);
                groundRenderer.material = mat;
            }
            ColliderCount++;
        }

        private static void CreatePhysicsObjects()
        {
            PhysicsObjectCount = 0;
            ColliderCount = 0;
            for (int i = 0; i < 10; i++)
            {
                var cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubeGo.name = $"FallingCube_{i}";
                cubeGo.transform.position = new Vector3(UnityEngine.Random.Range(-3f, 3f), 5 + i * 0.5f, UnityEngine.Random.Range(-3f, 3f));
                var rb = cubeGo.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 1f;
                if (i == 0)
                {
                    cubeGo.AddComponent<PhysicsResponder>();
                }
                if (i == 1)
                {
                    var triggerGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    triggerGo.name = "TriggerSphere";
                    triggerGo.transform.position = new Vector3(3, 1, 0);
                    triggerGo.transform.localScale = new Vector3(2, 2, 2);
                    var sc = triggerGo.GetComponent<SphereCollider>();
                    if (sc != null) sc.isTrigger = true;
                    var rbr = triggerGo.GetComponent<Renderer>();
                    if (rbr != null)
                    {
                        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        m.color = new Color(1, 0, 0, 0.3f);
                        rbr.material = m;
                    }
                    ColliderCount++;
                }
                var renderer = cubeGo.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = UnityEngine.Random.ColorHSV();
                    renderer.material = mat;
                    _coloredObjects.Add(cubeGo);
                }
                PhysicsObjectCount++;
                ColliderCount++;
            }
        }

        private static void CreateRotatingObjects()
        {
            var rotatorGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rotatorGo.name = "RotatingCapsule";
            rotatorGo.transform.position = new Vector3(-5, 1, 0);
            var rotator = rotatorGo.AddComponent<Rotator>();
            rotator.rotationSpeed = new Vector3(0, 90, 0);
            var renderer = rotatorGo.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = Color.cyan;
                renderer.material = mat;
                _coloredObjects.Add(rotatorGo);
            }

            var sphereGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereGo.name = "RotatingSphere";
            sphereGo.transform.position = new Vector3(5, 1, 0);
            var sphereRotator = sphereGo.AddComponent<Rotator>();
            sphereRotator.rotationSpeed = new Vector3(0, 180, 45);
            var sphereRenderer = sphereGo.GetComponent<Renderer>();
            if (sphereRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = Color.magenta;
                sphereRenderer.material = mat;
                _coloredObjects.Add(sphereGo);
            }
        }

        private static void CreateParticleSystems()
        {
            var dustGo = new GameObject("DustParticles");
            dustGo.transform.position = new Vector3(0, 0.1f, 0);
            var dust = dustGo.AddComponent<ParticleSystem>();
            var dm = dust.main;
            dm.startLifetime = 1.5f; dm.startSpeed = 1f; dm.startSize = 0.1f;
            dm.startColor = new Color(0.7f, 0.6f, 0.5f, 0.5f); dm.maxParticles = 100;
            var de = dust.emission; de.rateOverTime = 20;
            var ds = dust.shape; ds.shapeType = ParticleSystemShapeType.Box;
            dust.Play();
            ParticleSystemCount++;

            var explosionGo = new GameObject("ExplosionPS");
            explosionGo.transform.position = new Vector3(-4, 2, 0);
            var exp = explosionGo.AddComponent<ParticleSystem>();
            var em = exp.main;
            em.startLifetime = 2f; em.startSpeed = 8f; em.startSize = 0.25f;
            em.startColor = new Color(1f, 0.5f, 0f); em.maxParticles = 300;
            var ee = exp.emission; ee.rateOverTime = 0; ee.SetBursts(new[] { new ParticleSystemBurst(0f, 30) });
            var es = exp.shape; es.shapeType = ParticleSystemShapeType.Sphere; es.radius = 0.5f;
            exp.Play();
            ParticleSystemCount++;

            var fwGo = new GameObject("Fireworks");
            fwGo.transform.position = new Vector3(4, 5, 0);
            var fw = fwGo.AddComponent<ParticleSystem>();
            var fm = fw.main;
            fm.startLifetime = 3f; fm.startSpeed = 3f; fm.startSize = 0.15f;
            fm.startColor = Color.cyan; fm.maxParticles = 200;
            var fe = fw.emission; fe.rateOverTime = 10;
            var fs = fw.shape; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 15f;
            fw.Play();
            ParticleSystemCount++;
        }

        private static void Create2DPhysicsObjects()
        {
            var ground2D = new GameObject("Ground2D");
            ground2D.transform.position = new Vector3(0, -4f, 0);
            ground2D.transform.localScale = new Vector3(8, 0.5f, 1);
            var groundBox = ground2D.AddComponent<BoxCollider2D>();
            groundBox.size = new Vector2(1, 1);
            ColliderCount++;

            for (int i = 0; i < 5; i++)
            {
                var sqGo = new GameObject($"Physics2D_Square_{i}");
                sqGo.transform.position = new Vector3(-3f + i * 1.5f, 5f, 0);
                sqGo.transform.localScale = new Vector3(0.8f, 0.8f, 1);
                var sb = sqGo.AddComponent<BoxCollider2D>();
                sb.size = new Vector2(1, 1);
                var rb2d = sqGo.AddComponent<Rigidbody2D>();
                rb2d.gravityScale = 1f;
                rb2d.mass = 1f;
                Physics2DObjectCount++;
                ColliderCount++;
            }

            for (int i = 0; i < 3; i++)
            {
                var circGo = new GameObject($"Physics2D_Circle_{i}");
                circGo.transform.position = new Vector3(-2f + i * 2f, 7f, 0);
                circGo.transform.localScale = Vector3.one * 0.6f;
                var cc = circGo.AddComponent<CircleCollider2D>();
                cc.radius = 0.5f;
                var rb2d = circGo.AddComponent<Rigidbody2D>();
                rb2d.gravityScale = 1f;
                Physics2DObjectCount++;
                ColliderCount++;
            }

            var trigger2D = new GameObject("Trigger2D");
            trigger2D.transform.position = new Vector3(0, -2f, 0);
            trigger2D.transform.localScale = new Vector3(2f, 1f, 1);
            var tc = trigger2D.AddComponent<BoxCollider2D>();
            tc.isTrigger = true; tc.size = new Vector2(1, 1);
            ColliderCount++;
        }

        private static void CreateAudioObjects()
        {
            var listenerGo = new GameObject("AudioListener");
            listenerGo.AddComponent<AudioListener>();

            var audioGo = new GameObject("BGM_Source");
            audioGo.transform.position = Vector3.zero;
            var src = audioGo.AddComponent<AudioSource>();
            src.loop = true;
            src.volume = 0.5f;
            src.playOnAwake = true;
            AudioSourceCount++;

            var sfxGo = new GameObject("SFX_Source");
            var sfx = sfxGo.AddComponent<AudioSource>();
            sfx.loop = false;
            sfx.volume = 1f;
            sfx.playOnAwake = false;
            AudioSourceCount++;
        }

        private static void CreateAnimationObjects()
        {
            var animGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            animGo.name = "AnimatedCube";
            animGo.transform.position = new Vector3(0, 1, 5);
            animGo.AddComponent<Animator>();
            AnimatorCount++;
        }

        private static void CreateTesters()
        {
            var coroutineGo = new GameObject("CoroutineTester");
            coroutineGo.AddComponent<CoroutineTester>();

            var invokeGo = new GameObject("InvokeTester");
            invokeGo.AddComponent<InvokeTester>();
        }

        public static void SimulateInput()
        {
            _uiRebuildCount++;
            _particleCount = 0;
            var allPs = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
            foreach (var ps in allPs)
            {
                if (ps != null) _particleCount += ps.particleCount;
            }
        }

        public static void SetObjectColor(GameObject go, Color c)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = c;
            }
        }

        public static void SetAllObjectColor(Color c)
        {
            foreach (var go in _coloredObjects)
            {
                if (go != null)
                {
                    SetObjectColor(go, c);
                }
            }
        }
    }
}
