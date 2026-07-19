using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(AssetPipelineStateCollection.Name)]
public sealed class NativeModelImportTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "anity-native-model-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public NativeModelImportTests()
    {
        Directory.CreateDirectory(Path.Combine(_project, "Assets", "Models"));
        EditorApplication.OpenProject(_project);
        NativeModelPostprocessorProbe.Reset();
    }

    public void Dispose()
    {
        NativeModelPostprocessorProbe.Reset();
        EditorApplication.OpenProject(_originalDirectory);
        try { Directory.Delete(_project, true); } catch { }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void NonXyzRotationOrdersResampleTwentyFourSynchronizedQuaternionFrames(int rotationOrder)
    {
        var imported = ReimportOrderedAnimation(rotationOrder, importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        });
        var expectedFrames = Enumerable.Range(0, 24).ToArray();
        foreach (var property in QuaternionProperties)
            Assert.Equal(expectedFrames, Frames(Curve(imported.Clip, property), imported.Clip.frameRate));
    }

    [Theory]
    [InlineData(1, 0.127679437f, -0.189307854f, -0.239298344f, 0.9437144f)]
    [InlineData(2, 0.03813458f, -0.144878134f, -0.268535852f, 0.9515486f)]
    [InlineData(3, 0.0381345823f, -0.189307854f, -0.2685358f, 0.9437144f)]
    [InlineData(4, 0.127679437f, -0.144878119f, -0.239298344f, 0.9515485f)]
    [InlineData(5, 0.127679437f, -0.144878134f, -0.268535823f, 0.9437144f)]
    public void NonXyzRotationOrdersMatchUnity2022AtMiddleSourceKey(
        int rotationOrder, float x, float y, float z, float w)
    {
        var imported = ReimportOrderedAnimation(rotationOrder, importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        });
        Assert.Equal(x, Curve(imported.Clip, "m_LocalRotation.x").keys[13].value);
        Assert.Equal(y, Curve(imported.Clip, "m_LocalRotation.y").keys[13].value);
        Assert.Equal(z, Curve(imported.Clip, "m_LocalRotation.z").keys[13].value);
        Assert.Equal(w, Curve(imported.Clip, "m_LocalRotation.w").keys[13].value);
    }

    [Theory]
    [InlineData(1, -6.06403638E-09f, 1.73606658E-08f, 2.64970872E-08f)]
    [InlineData(2, -8.734566E-09f, 1.89047054E-08f, 2.54229118E-08f)]
    [InlineData(3, -8.833036E-09f, 1.7548496E-08f, 2.5447406E-08f)]
    [InlineData(4, -5.9601466E-09f, 1.87167117E-08f, 2.64841038E-08f)]
    [InlineData(5, -6.01030559E-09f, 1.8700721E-08f, 2.55728114E-08f)]
    public void NonXyzRotationOrdersMatchUnity2022AtMatrixConverterZeroCrossing(
        int rotationOrder, float x, float y, float z)
    {
        var imported = ReimportOrderedAnimation(rotationOrder, importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        });
        Assert.Equal(x, Curve(imported.Clip, "m_LocalRotation.x").keys[18].value);
        Assert.Equal(y, Curve(imported.Clip, "m_LocalRotation.y").keys[18].value);
        Assert.Equal(z, Curve(imported.Clip, "m_LocalRotation.z").keys[18].value);
        Assert.Equal(1f, Curve(imported.Clip, "m_LocalRotation.w").keys[18].value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void NonXyzRotationOrdersCanonicalizeUnityIdentityQuaternionToPositiveZero(
        int rotationOrder)
    {
        var imported = ReimportOrderedAnimation(rotationOrder, importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        });
        Assert.Equal(0, BitConverter.SingleToInt32Bits(
            Curve(imported.Clip, "m_LocalRotation.x").keys[0].value));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(
            Curve(imported.Clip, "m_LocalRotation.y").keys[0].value));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(
            Curve(imported.Clip, "m_LocalRotation.z").keys[0].value));
        Assert.Equal(1f, Curve(imported.Clip, "m_LocalRotation.w").keys[0].value);
    }

    public static TheoryData<int, float, float, float, string> GimbalUnityQuaternionSamples => new()
    {
        { 1, -15f, 90f, 0f, "AAAAACB2ELv3fQi8cSOQvO5C7rwz+Cq9zehevWZAh70THpu9zw+qvYIAtL2wrLm9bEy8vaoFvb2pd8q9pbrqvdclB750ehK+E10Rvrb9Ab41F8+9jrmSvRZoQ70BMxy9AAAAAFW0WLyK50y9SQDZvUWKNL6TGIO+sA6uvh2K2L4qFgC/YYARv4vJH7/Yciq/1SExv394M78zyC6/hWchv7cXDL88CeC+/Kyevu3CNL5ufVO9tQdRPTlk9T3vWhQ+AAAAAH2Y9Le8x9q5g8j1ugXIqrsKUzW8inahvJVS/bxGMzS9HMBsvXhUkb0C9qe9y2G3vaoFvb1uW6u9xBBxvZmbnLwhFQY9XvW6PazeGD4/cUw++idzPnlthT6HfYk+AACAPxz6fz+qq38/woR+P03gez8TOHc/7ktwP7E5Zz/vg1w/QQhRP5HoRT+vbDw/keE1P394Mz80Fjg/afNDP3GHUz/qGmM/uIdvP3/vdj8HKHk/A6h3P3HwdD+vmHM/" },
        { 1, -20f, -90f, 0f, "AAAAAHidQLve/DW8QS3AvLDSHr1852O9qYqUvfo1tL32nc69b3LivZOa7713Ffe9gob6vS94+71t5/G9tr7Yvdk2tr0NTJG9Q2phvTvPMr2ofBq9qvEUvcUKGb0BMxy9AAAAADq0WDwf5kw9hfnYPZF6ND42AIM+hNStPhwX2D7AZ/8+L+sQP/r8Hj/scik/dPwvP+5EMj+JCC8/mu0lP4y4Fz+FYAU/a6bgPgXptD6L14s+dodTPqJTJT7vWhQ+AAAAAE0QIzgo2hE6TtkjO8Ou4zsWtXE8wjDXPN3DKD0GB3A9mJ+dPYVzwT0Bg9898PvzPS94+z3zVf498goEPmjEDT7kZh0+ObsyPr3wSz4w5WU+DdB8PkF5hj6HfYk+AACAP/z5fz/kqX8/0nx+P2bKez8hCnc/n/tvP+O+Zj+42ls/6jFQPzLrRD/GUTs/U7Q0P+5EMj+UljU/8TU+P57YST8yK1Y/UjhhP8S6aT9eRW8/yTFyP5hXcz+vmHM/" },
        { 2, 0f, 90f, 15f, "AAAAAH2Y9Le8x9q5g8j1ugXIqrsKUzW8inahvJVS/bxGMzS9HMBsvXhUkb0C9qe9y2G3vagFvb2bHKy9GPCCvU2oJ73p2Mi8PhqzvBGzEL2l5Hu96wq9vaPC8L1mvgK+AAAAAFW0WLyK50y9SQDZvUWKNL6TGIO+sA6uvh2K2L4qFgC/YYARv4vJH7/Yciq/1SExv394M7/q4C6/GpMhv8jjC7/5Vd2+pxeYvsj3Hb6zY6W8aaK3PV61Jj7r2UE+AAAAACB2ELv3fQi8cSOQvO5C7rwz+Cq9zehevWZAh70THpu9zw+qvYIAtL2wrLm9bEy8vagFvb0XZq69kdmCvR+w57zX+qI8E26aPbWsBT59BTY+ikpZPigbbj6fCnU+AACAPxz6fz+qq38/woR+P03gez8TOHc/7ktwP7E5Zz/vg1w/QQhRP5HoRT+vbDw/keE1P394Mz/Cbjg/WjdFPyoFVj+ks2Y/1Z1zPxKOej+rYHs/zv13P02ecz9El3E/" },
        { 2, 0f, -90f, -20f, "AAAAAE0QI7go2hG6Ttkju8Ou47sWtXG8wjDXvN3DKL0GB3C9mJ+dvYVzwb0Bg9+98PvzvS54+73dGP699mQCvkWnBr5CjQq+FOwMvrgKDb4l7gq+4HQHvkAoBL5mvgK+AAAAADq0WDwf5kw9hfnYPZF6ND42AIM+hNStPhwX2D7AZ/8+L+sQP/r8Hj/scik/dPwvP+5EMj9jMi8/QKEmPzVwGT8jnAg/FuHqPlYbwz4FnZ0+84d8Pq6tUT7r2UE+AAAAAHidQDve/DU8QS3APLDSHj1852M9qYqUPfo1tD32nc49b3LiPZOa7z13Ffc9gob6PS54+z1Q2v89s0gGPku1ED5V9R4+5EQwPqo2Qz7Fz1U+ZMxlPlHocD6fCnU+AACAP/z5fz/kqX8/0nx+P2bKez8hCnc/n/tvP+O+Zj+42ls/6jFQPzLrRD/GUTs/U7Q0P+5EMj/vIjU/9aA8P6DfRj/H/FE/fl1cP9/lZD8eFGs/XvZuP8v5cD9El3E/" },
        { 3, 0f, 90f, 15f, "AAAAAH2Y9Le8x9q5g8j1ugXIqrsKUzW8inahvJVS/bxGMzS9HMBsvXhUkb0C9qe9y2G3vagFvb2bHKy9GPCCvU6oJ73n2Mi8ORqzvBSzEL2l5Hu96wq9vaLC8L1mvgK+AAAAAFW0WLyK50y9SQDZvUWKNL6TGIO+sA6uvh2K2L4qFgC/YYARv4vJH7/Yciq/1SExv394M78cxS6/SkAhv3uEC78ti92+6uCavnMLLL7nPDW9U2tjPeU6+D3vWhQ+AAAAACB2ELv3fQi8cSOQvO5C7rwz+Cq9zehevWZAh70THpu9zw+qvYIAtL2wrLm9bEy8vagFvb0XZq69kdmCvSCw57zW+qI8Em6aPbWsBT58BTY+ikpZPiYbbj6gCnU+AACAPxz6fz+qq38/woR+P03gez8TOHc/7ktwP7E5Zz/vg1w/QQhRP5HoRT+vbDw/keE1P394Mz8ciTg/FXtFP1dDVj/fpmY/ZC1zP5L5eT/qLHs/RKV4P8EzdT+vmHM/" },
        { 3, 0f, -90f, -20f, "AAAAAE0QI7go2hG6Ttkju8Ou47sWtXG8wjDXvN3DKL0GB3C9mJ+dvYVzwb0Bg9+98PvzvS54+73dGP699mQCvkanBr5DjQq+FOwMvrgKDb4l7gq+4XQHvkAoBL5mvgK+AAAAADq0WDwf5kw9hfnYPZF6ND42AIM+hNStPhwX2D7AZ/8+L+sQP/r8Hj/scik/dPwvP+5EMj/YCS8/O/4lPz73Fz+N6QU/1E3iPkLYtj6yjo0+2ahVPsT+JT7vWhQ+AAAAAHidQDve/DU8QS3APLDSHj1852M9qYqUPfo1tD32nc49b3LiPZOa7z13Ffc9gob6PS54+z1P2v89s0gGPku1ED5W9R4+5EQwPqo2Qz7Ez1U+ZcxlPlLocD6gCnU+AACAP/z5fz/kqX8/0nx+P2bKez8hCnc/n/tvP+O+Zj+42ls/6jFQPzLrRD/GUTs/U7Q0P+5EMj8dSjU/ejA9P0MASD/Kt1M/4ZhePzpqZz96nm0/EVJxPzcYcz+vmHM/" },
        { 4, -15f, 90f, 0f, "AAAAACB2ELv3fQi8cSOQvO5C7rwz+Cq9zehevWZAh70THpu9zw+qvYIAtL2wrLm9bEy8vakFvb2qd8q9o7rqvdYlB75zehK+FV0RvrT9Ab41F8+9j7mSvRZoQ70BMxy9AAAAAFW0WLyK50y9SQDZvUWKNL6TGIO+sA6uvh2K2L4qFgC/YYARv4vJH7/Yciq/1SExv394M78Dbi6/bgsgv+QnCb8OPta+VMyQvnnPEb6gjkm8boy/PZ/CJz7r2UE+AAAAAH2Y9Le8x9q5g8j1ugXIqrsKUzW8inahvJVS/bxGMzS9HMBsvXhUkb0C9qe9y2G3vakFvb1tW6u9wxBxvaebnLwhFQY9YPW6Pa/eGD4/cUw++SdzPnpthT6IfYk+AACAPxz6fz+qq38/woR+P03gez8TOHc/7ktwP7E5Zz/vg1w/QQhRP5HoRT+vbDw/keE1P394Mz+razg/HhBFP+xxVT+cdGU/zLdxPzNgeD+ifHk/lNd2P3dDcz9El3E/" },
        { 4, -20f, -90f, 0f, "AAAAAHidQLve/DW8QS3AvLDSHr1852O9qYqUvfo1tL32nc69b3LivZOa7713Ffe9gob6vSx4+71s5/G9uL7Yvdk2tr0NTJG9Q2phvTnPMr2nfBq9q/EUvcYKGb0BMxy9AAAAADq0WDwf5kw9hfnYPZF6ND42AIM+hNStPhwX2D7AZ/8+L+sQP/r8Hj/scik/dPwvP+5EMj/Yfi8/uKYnP5BIGz+LDws/9h/wPmi8xz7M6qA+bAOAPjSrUj7r2UE+AAAAAE0QIzgo2hE6TtkjO8Ou4zsWtXE8wjDXPN3DKD0GB3A9mJ+dPYVzwT0Bg9898PvzPSx4+z3xVf498goEPmnEDT7kZh0+ObsyPrzwSz4v5WU+DtB8PkF5hj6IfYk+AACAP/z5fz/kqX8/0nx+P2bKez8hCnc/n/tvP+O+Zj+42ls/6jFQPzLrRD/GUTs/U7Q0P+5EMj8+JDU/l7E8P1EeRz/OhVI/MjFdP37dZT+z72s/uH5vP5MkcT9El3E/" },
        { 5, -90f, 75f, 90f, "AAAAANNIVryr8UO9I+bDvdysFb4hakG+Rphcvh4DY74v01W+E8c6vjgJGr4Aeve9rsPMvawFvb1bBOy9WxQzvkWmgL7d1KG+zHiuvqY7n75MfHG+ZOcSvjFRi70BMxy9AAAAAHBsN7ykmDS9I9jJvRqUMb6rXYe+7CO6vgbg6747vwu/Fb8cv+BgKL8AOS+/qIUyv394M78LgjG/F50pv3qTF79xEfK+/u6gvuEJFL7MqII74rThPXQ3Lj7s2UE+AAAAANNIVryr8UO9I+bDvdysFb4hakG+Rphcvh4DY74v01W+E8c6vjgJGr4Aeve9rsPMvawFvb2DC9C9f4H/vaFpGb4OzCG+n/UFvncRfr2oEhI9BycLPj7hVz6fCnU+AACAP6/wfz/5KX8/j2N8P1GBdj9PO20/d15hP0SRVD9Qukg/lVM/P7r7OD+wbzU/auAzP394Mz8IOTQ/NLE3P6s6QD/OpU4/m1dgP9rybz8mnHg/d1x5P5nPdT+vmHM/" },
        { 5, -90f, -70f, -90f, "AAAAAAVyVrwYiUS9rEbFvXWoF76tvkW+DW1kvrxLb77xBWe+lrhQvjv9M75Gnxi+bAQFvi14+72g4QK+e9oPvuBQH76DAyq+Va0oviBOF77Cv++9lTilvSFmTr0BMxy9AAAAAAtjKzwUPyk9f/S9PQ7zJz5buoA+Ow+yPtkP4z5LbAc/HO8YPx1eJT8xFC0/eA4xP+5EMj9luDA/eKArP2jbIT+ctxI/kL39PpAI0T7rk6U+DImBPg4kUz7s2UE+AAAAAAVyVjwYiUQ9rEbFPXWoFz6tvkU+DW1kPrxLbz7xBWc+lrhQPjv9Mz5Gnxg+bAQFPi14+z3a1Qk+TYEpPvnvUz62Ln4+bKSOPkH5lD4V0ZE+coWIPgiUfT6fCnU+AACAPzDxfz/YMH8/4398P3rGdj9qtW0/dQZiPwNIVT//Tkk/opM/Py3FOD8yvjQ//dAyP+5EMj/wBDM/dKI1P03vOj8fXEM/S1ZOPwxAWj87D2U/fC5tPz0Acj+vmHM/" },
    };

    [Theory]
    [MemberData(nameof(GimbalUnityQuaternionSamples))]
    public void NonXyzRotationOrdersStayWithinTwoUlpsAcrossGimbalSingularities(
        int rotationOrder, float middleX, float middleY, float middleZ,
        string expectedSamplesBase64)
    {
        var imported = ReimportOrderedAnimation(
            rotationOrder, middleX, middleY, middleZ, importer =>
            {
                importer.resampleCurves = true;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
            });
        var expectedBytes = Convert.FromBase64String(expectedSamplesBase64);
        Assert.Equal(QuaternionProperties.Length * 24 * sizeof(float), expectedBytes.Length);
        var expectedIndex = 0;
        foreach (var property in QuaternionProperties)
        {
            var keys = Curve(imported.Clip, property).keys;
            Assert.Equal(24, keys.Length);
            foreach (var key in keys)
            {
                var bits = BinaryPrimitives.ReadInt32LittleEndian(
                    expectedBytes.AsSpan(expectedIndex * sizeof(float), sizeof(float)));
                var expected = BitConverter.Int32BitsToSingle(bits);
                Assert.InRange(UlpDistance(expected, key.value), 0, 2);
                expectedIndex++;
            }
        }
    }

    [Theory]
    [InlineData(1, 0.01f, "0,1,3,6,8,12,13,14,15,16,17,18,19,20,21,22,23")]
    [InlineData(2, 0.01f, "0,1,3,5,6,8,12,13,14,16,17,18,19,21,22,23")]
    [InlineData(3, 0.01f, "0,1,3,4,6,7,10,12,13,14,15,16,17,18,19,20,21,22,23")]
    [InlineData(4, 0.01f, "0,1,3,5,6,10,11,12,13,14,16,17,18,19,20,21,22,23")]
    [InlineData(5, 0.01f, "0,1,3,6,7,10,11,12,13,14,15,16,17,18,19,20,22,23")]
    [InlineData(1, 0.1f, "0,1,7,12,14,17,19,22,23")]
    [InlineData(2, 0.1f, "0,1,7,12,14,17,19,22,23")]
    [InlineData(3, 0.1f, "0,1,7,12,13,14,17,19,22,23")]
    [InlineData(4, 0.1f, "0,1,7,12,14,17,19,22,23")]
    [InlineData(5, 0.1f, "0,1,7,12,13,14,17,19,22,23")]
    [InlineData(1, 0.5f, "0,5,11,14,17,19,22,23")]
    [InlineData(2, 0.5f, "0,5,11,14,17,19,22,23")]
    [InlineData(3, 0.5f, "0,5,11,14,17,19,22,23")]
    [InlineData(4, 0.5f, "0,5,11,14,17,19,22,23")]
    [InlineData(5, 0.5f, "0,5,11,14,17,19,22,23")]
    public void NonXyzRotationOrderReductionMatchesUnity2022RetainedFrames(
        int rotationOrder, float rotationError, string expectedFrames)
    {
        var imported = ReimportOrderedAnimation(rotationOrder, importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationRotationError = rotationError;
        });
        var expected = expectedFrames.Split(',').Select(int.Parse).ToArray();
        foreach (var property in QuaternionProperties)
            Assert.Equal(expected, Frames(Curve(imported.Clip, property), imported.Clip.frameRate));
    }

    [Fact]
    public void FbxImportCreatesGameObjectMainAssetInsteadOfTextAsset()
    {
        var path = ImportAnimatedFbx();
        Assert.IsType<GameObject>(AssetDatabase.LoadMainAssetAtPath(path));
        Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
    }

    [Fact]
    public void FbxImportCreatesIndexedMeshSubAsset()
    {
        var mesh = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(ImportAnimatedFbx()).OfType<Mesh>());
        Assert.Equal(24, mesh.vertexCount);
        Assert.Equal(36, mesh.GetTriangles(0).Length);
        Assert.Equal(12, mesh.GetTriangles(0).Length / 3);
    }

    [Fact]
    public void FbxImportConvertsCentimetersToUnityMeters()
    {
        var path = ImportAnimatedFbx();
        var importer = ModelImporter.GetAtPath(path);
        var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
        Assert.Equal(0.01f, importer.fileScale, 5);
        Assert.Equal(0.01f, mesh.bounds.size.x, 4);
        Assert.Equal(0.01f, mesh.bounds.size.y, 4);
        Assert.Equal(0.01f, mesh.bounds.size.z, 4);
    }

    [Fact]
    public void ImportedMeshIsAttachedToRootMeshFilter()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(ImportAnimatedFbx())!;
        var filter = root.GetComponent<MeshFilter>();
        Assert.NotNull(filter);
        Assert.Same(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(root)).OfType<Mesh>().Single(), filter!.sharedMesh);
        Assert.NotNull(root.GetComponent<MeshRenderer>());
    }

    [Fact]
    public void FbxAnimationStackCreatesAnimationClip()
    {
        var path = ImportAnimatedFbx();
        var clip = Assert.Single(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
        Assert.Equal("Take 001", clip.name);
        Assert.Equal(24f, clip.frameRate, 4);
        Assert.True(clip.length > 0f);
    }

    [Fact]
    public void ImportedAnimationContainsPositionRotationAndScaleCurves()
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(ImportAnimatedFbx()).OfType<AnimationClip>().Single();
        var properties = clip.bindings.Select(binding => binding.propertyName).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("m_LocalPosition.x", properties);
        Assert.Contains("m_LocalRotation.w", properties);
        Assert.Contains("m_LocalScale.z", properties);
    }

    [Fact]
    public void ImportedAnimationSamplesDecodedTransformMotion()
    {
        var path = ImportAnimatedFbx();
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path)!;
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single();
        var initial = root.transform.localPosition;
        clip.SampleAnimation(root, clip.length);
        Assert.NotEqual(initial, root.transform.localPosition);
    }

    [Theory]
    [InlineData("m_LocalPosition.x", 0f, -0.01f, 0.01f)]
    [InlineData("m_LocalPosition.y", 0f, 0.02f, -0.02f)]
    [InlineData("m_LocalPosition.z", 0f, 0.03f, -0.03f)]
    [InlineData("localEulerAnglesRaw.x", 0f, 10f, -10f)]
    [InlineData("localEulerAnglesRaw.y", 0f, -20f, 20f)]
    [InlineData("localEulerAnglesRaw.z", 0f, -30f, 30f)]
    [InlineData("m_LocalScale.x", 1f, 1.1f, 0.9f)]
    [InlineData("m_LocalScale.y", 1f, 1.2f, 0.8f)]
    [InlineData("m_LocalScale.z", 1f, 1.3f, 0.7f)]
    public void NonResampledTransformCurveMatchesUnity2022SourceKeys(
        string property, float first, float middle, float last)
    {
        var clip = ImportNonResampledAnimation().Clip;
        var keys = Curve(clip, property).keys;
        Assert.Equal(new[] { 0f, 13f, 23f }, keys.Select(key => key.time * clip.frameRate).ToArray());
        Assert.Equal(first, keys[0].value, 5);
        Assert.Equal(middle, keys[1].value, 5);
        Assert.Equal(last, keys[2].value, 5);
        Assert.All(keys, key =>
        {
            Assert.Equal(0f, key.inTangent, 5);
            Assert.Equal(0f, key.outTangent, 5);
        });
    }

    [Fact]
    public void NonResampledTransformUsesNineRawEulerBindings()
    {
        var clip = ImportNonResampledAnimation().Clip;
        var properties = clip.bindings.Select(binding => binding.propertyName).ToArray();
        Assert.Equal(9, properties.Length);
        Assert.Contains("localEulerAnglesRaw.x", properties);
        Assert.Contains("localEulerAnglesRaw.y", properties);
        Assert.Contains("localEulerAnglesRaw.z", properties);
        Assert.DoesNotContain(properties, property => property.StartsWith("m_LocalRotation.", StringComparison.Ordinal));
    }

    [Fact]
    public void NonResampledTransformSamplesUnityConvertedPose()
    {
        var imported = ImportNonResampledAnimation();
        imported.Clip.SampleAnimation(imported.Root, 13f / 24f);
        Assert.Equal(-0.01f, imported.Root.transform.localPosition.x, 5);
        Assert.Equal(0.02f, imported.Root.transform.localPosition.y, 5);
        Assert.Equal(0.03f, imported.Root.transform.localPosition.z, 5);
        Assert.Equal(1.1f, imported.Root.transform.localScale.x, 5);
        Assert.Equal(1.2f, imported.Root.transform.localScale.y, 5);
        Assert.Equal(1.3f, imported.Root.transform.localScale.z, 5);
        Assert.InRange(Quaternion.Angle(Quaternion.Euler(10f, -20f, -30f), imported.Root.transform.localRotation), 0f, 0.001f);
    }

    [Fact]
    public void ResampledTransformUsesQuaternionBindingsAndTwentyFourFramesWhenUncompressed()
    {
        var imported = ReimportAnimated(importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        });
        var properties = imported.Clip.bindings.Select(binding => binding.propertyName).ToArray();
        Assert.Contains("m_LocalRotation.w", properties);
        Assert.DoesNotContain(properties, property => property.StartsWith("localEulerAnglesRaw.", StringComparison.Ordinal));
        var keys = Curve(imported.Clip, "m_LocalRotation.x").keys;
        Assert.Equal(24, keys.Length);
        Assert.Equal(Enumerable.Range(0, 24), Frames(Curve(imported.Clip,
            "m_LocalRotation.x"), imported.Clip.frameRate));
    }

    [Fact]
    public void ResampledTransformQuaternionMatchesUnity2022AtMiddleSourceKey()
    {
        var imported = ReimportAnimated(importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        });
        Assert.Equal(0.038134575f, Curve(imported.Clip, "m_LocalRotation.x").keys[13].value, 6);
        Assert.Equal(-0.18930785f, Curve(imported.Clip, "m_LocalRotation.y").keys[13].value, 6);
        Assert.Equal(-0.23929834f, Curve(imported.Clip, "m_LocalRotation.z").keys[13].value, 6);
        Assert.Equal(0.9515485f, Curve(imported.Clip, "m_LocalRotation.w").keys[13].value, 6);
    }

    [Theory]
    [InlineData(0.01f, "0,1,3,5,6,8,9,11,12,13,14,15,16,17,18,19,20,22,23")]
    [InlineData(0.1f, "0,1,7,12,14,17,19,22,23")]
    [InlineData(0.5f, "0,5,11,14,17,19,22,23")]
    [InlineData(1f, "0,7,13,18,20,22,23")]
    public void ResampledQuaternionReductionMatchesUnity2022RotationErrorFrames(
        float rotationError, string expectedFrames)
    {
        var imported = ReimportAnimated(importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationRotationError = rotationError;
        });
        var expected = expectedFrames.Split(',').Select(int.Parse).ToArray();
        foreach (var property in QuaternionProperties)
        {
            Assert.Equal(expected, Frames(Curve(imported.Clip, property), imported.Clip.frameRate));
        }
    }

    [Theory]
    [InlineData("m_LocalRotation.x")]
    [InlineData("m_LocalRotation.y")]
    [InlineData("m_LocalRotation.z")]
    [InlineData("m_LocalRotation.w")]
    public void ResampledQuaternionReductionSynchronizesComponentKeyTimes(string property)
    {
        var imported = ReimportAnimated(importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationRotationError = 0.5f;
        });
        Assert.Equal(Frames(Curve(imported.Clip, "m_LocalRotation.x"), imported.Clip.frameRate),
            Frames(Curve(imported.Clip, property), imported.Clip.frameRate));
    }

    [Fact]
    public void ResampledQuaternionReductionPreservesUnity2022RetainedValues()
    {
        var (original, reduced) = ImportOriginalAndReducedQuaternionCurves();
        foreach (var property in QuaternionProperties)
        foreach (var retained in reduced[property])
            Assert.Equal(original[property].Single(key => key.time == retained.time).value, retained.value);
    }

    [Fact]
    public void ResampledQuaternionReductionPreservesUnity2022RetainedTangents()
    {
        var (original, reduced) = ImportOriginalAndReducedQuaternionCurves();
        foreach (var property in QuaternionProperties)
        foreach (var retained in reduced[property])
        {
            var source = original[property].Single(key => key.time == retained.time);
            Assert.Equal(source.inTangent, retained.inTangent);
            Assert.Equal(source.outTangent, retained.outTangent);
        }
    }

    [Fact]
    public void ImportAnimationFalseOmitsAnimationClip()
    {
        var path = CopyAnimatedFixture();
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        importer.importAnimation = false;
        importer.SaveAndReimport();
        Assert.Empty(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
    }

    [Fact]
    public void GlobalScaleScalesDecodedGeometry()
    {
        var path = CopyAnimatedFixture();
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        importer.globalScale = 2f;
        importer.SaveAndReimport();
        var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
        Assert.Equal(0.02f, mesh.bounds.size.x, 4);
    }

    [Fact]
    public void CorruptReimportKeepsLastSuccessfulModelArtifact()
    {
        var path = ImportAnimatedFbx();
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        File.WriteAllText(FullPath(path), "not an fbx");
        AssetDatabase.ImportAsset(path);
        Assert.Same(original, AssetDatabase.LoadAssetAtPath<GameObject>(path));
        Assert.Null(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
    }

    [Fact]
    public void ObjImportCreatesHierarchyAndTriangulatedMesh()
    {
        const string path = "Assets/Models/Quad.obj";
        File.WriteAllText(FullPath(path), "o Quad\nv -1 0 -1\nv 1 0 -1\nv 1 0 1\nv -1 0 1\nvt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\nvn 0 1 0\nf 1/1/1 2/2/1 3/3/1 4/4/1\n");
        AssetDatabase.ImportAsset(path);
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
        Assert.NotNull(root);
        Assert.Equal(4, mesh.vertexCount);
        Assert.Equal(6, mesh.GetTriangles(0).Length);
    }

    [Fact]
    public void SpecializedModelCallbacksFollowUnityImportOrder()
    {
        var path = CopyAnimatedFixture();
        NativeModelPostprocessorProbe.TargetPath = path;
        NativeModelPostprocessorProbe.Enabled = true;
        AssetDatabase.ImportAsset(path);
        Assert.Equal(new[]
        {
            "pre-model", "post-mesh", "pre-animation", "post-animation:Take 001", "post-model"
        }, NativeModelPostprocessorProbe.Calls);
    }

    [Fact]
    public void OnPreprocessAnimationCanDisableClipConstruction()
    {
        var path = CopyAnimatedFixture();
        NativeModelPostprocessorProbe.TargetPath = path;
        NativeModelPostprocessorProbe.DisableAnimation = true;
        NativeModelPostprocessorProbe.Enabled = true;
        AssetDatabase.ImportAsset(path);
        Assert.Empty(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>());
        Assert.DoesNotContain(NativeModelPostprocessorProbe.Calls, call => call.StartsWith("post-animation:", StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultClipAnimationsExposeDecodedTakeMetadata()
    {
        var importer = ModelImporter.GetAtPath(ImportAnimatedFbx());
        var clip = Assert.Single(importer.defaultClipAnimations);
        Assert.Equal("Take 001", clip.name);
        Assert.Equal("Take 001", clip.takeName);
        Assert.True(clip.lastFrame > clip.firstFrame);
    }

    private string ImportAnimatedFbx()
    {
        var path = CopyAnimatedFixture();
        AssetDatabase.ImportAsset(path);
        return path;
    }

    private (GameObject Root, AnimationClip Clip) ImportNonResampledAnimation() =>
        ReimportAnimated(importer => importer.resampleCurves = false);

    private (GameObject Root, AnimationClip Clip) ReimportAnimated(Action<ModelImporter> configure)
    {
        var path = ImportAnimatedFbx();
        var importer = ModelImporter.GetAtPath(path);
        configure(importer);
        importer.SaveAndReimport();
        return (
            AssetDatabase.LoadAssetAtPath<GameObject>(path)!,
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single());
    }

    private (GameObject Root, AnimationClip Clip) ReimportOrderedAnimation(
        int rotationOrder, Action<ModelImporter> configure)
    {
        var path = CopyAnimatedFixture();
        var fullPath = FullPath(path);
        var fixture = File.ReadAllText(fullPath);
        var orderedFixture = fixture.Replace(
            "Property: \"RotationOrder\", \"enum\", \"\",0",
            "Property: \"RotationOrder\", \"enum\", \"\"," + rotationOrder,
            StringComparison.Ordinal);
        Assert.NotEqual(fixture, orderedFixture);
        File.WriteAllText(fullPath, orderedFixture);
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        configure(importer);
        importer.SaveAndReimport();
        return (
            AssetDatabase.LoadAssetAtPath<GameObject>(path)!,
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single());
    }

    private (GameObject Root, AnimationClip Clip) ReimportOrderedAnimation(
        int rotationOrder, float middleX, float middleY, float middleZ,
        Action<ModelImporter> configure)
    {
        var path = CopyAnimatedFixture();
        var fullPath = FullPath(path);
        var fixture = File.ReadAllText(fullPath);
        var orderedFixture = fixture.Replace(
            "Property: \"RotationOrder\", \"enum\", \"\",0",
            "Property: \"RotationOrder\", \"enum\", \"\"," + rotationOrder,
            StringComparison.Ordinal);
        foreach (var (original, replacement) in new[]
        {
            (10f, middleX), (20f, middleY), (30f, middleZ),
        })
        {
            orderedFixture = orderedFixture.Replace(
                $"26941925500,{original.ToString("R", CultureInfo.InvariantCulture)},U,s,0,0,n",
                $"26941925500,{replacement.ToString("R", CultureInfo.InvariantCulture)},U,s,0,0,n",
                StringComparison.Ordinal);
        }
        Assert.NotEqual(fixture, orderedFixture);
        File.WriteAllText(fullPath, orderedFixture);
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        configure(importer);
        importer.SaveAndReimport();
        return (
            AssetDatabase.LoadAssetAtPath<GameObject>(path)!,
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single());
    }

    private static AnimationCurve Curve(AnimationClip clip, string property) =>
        clip.bindings.Single(binding => string.Equals(binding.propertyName, property, StringComparison.Ordinal)).curve;

    private static readonly string[] QuaternionProperties =
    {
        "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
    };

    private static int[] Frames(AnimationCurve curve, float frameRate) =>
        curve.keys.Select(key => (int)MathF.Round(key.time * frameRate)).ToArray();

    private static long UlpDistance(float expected, float actual) => Math.Abs(
        (long)BitConverter.SingleToInt32Bits(expected) - BitConverter.SingleToInt32Bits(actual));

    private (Dictionary<string, Keyframe[]> Original, Dictionary<string, Keyframe[]> Reduced)
        ImportOriginalAndReducedQuaternionCurves()
    {
        var path = ImportAnimatedFbx();
        var importer = ModelImporter.GetAtPath(path);
        importer.resampleCurves = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        var originalClip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single();
        var original = QuaternionProperties.ToDictionary(property => property,
            property => Curve(originalClip, property).keys, StringComparer.Ordinal);

        importer = ModelImporter.GetAtPath(path);
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        importer.animationRotationError = 0.5f;
        importer.SaveAndReimport();
        var reducedClip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single();
        var reduced = QuaternionProperties.ToDictionary(property => property,
            property => Curve(reducedClip, property).keys, StringComparer.Ordinal);
        return (original, reduced);
    }

    private string CopyAnimatedFixture()
    {
        var name = "Animated-" + Guid.NewGuid().ToString("N") + ".fbx";
        var path = "Assets/Models/" + name;
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Models", "AnimatedCube.fbx"), FullPath(path));
        return path;
    }

    private string FullPath(string path) => Path.Combine(_project, path);
}

public sealed class NativeModelPostprocessorProbe : AssetPostprocessor
{
    internal static bool Enabled;
    internal static string TargetPath = string.Empty;
    internal static bool DisableAnimation;
    internal static readonly List<string> Calls = new();

    internal static void Reset()
    {
        Enabled = false;
        TargetPath = string.Empty;
        DisableAnimation = false;
        Calls.Clear();
    }

    private bool Active => Enabled && string.Equals(assetPath, TargetPath, StringComparison.Ordinal);
    private void OnPreprocessModel() { if (Active) Calls.Add("pre-model"); }
    private void OnPostprocessMeshHierarchy(GameObject root) { if (Active) Calls.Add("post-mesh"); }
    private void OnPreprocessAnimation()
    {
        if (!Active) return;
        Calls.Add("pre-animation");
        if (DisableAnimation) ((ModelImporter)assetImporter).importAnimation = false;
    }
    private void OnPostprocessAnimation(GameObject root, AnimationClip clip) { if (Active) Calls.Add("post-animation:" + clip.name); }
    private void OnPostprocessModel(GameObject root) { if (Active) Calls.Add("post-model"); }
}
