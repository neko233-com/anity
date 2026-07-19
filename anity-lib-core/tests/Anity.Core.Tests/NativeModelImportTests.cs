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
    public void NonXyzRotationOrdersMatchUnityBitsAcrossGimbalSingularities(
        int rotationOrder, float middleX, float middleY, float middleZ,
        string expectedSamplesBase64)
    {
        var imported = ReimportOrderedAnimation(
            rotationOrder, middleX, middleY, middleZ, importer =>
            {
                importer.resampleCurves = true;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
            });
        AssertQuaternionBits(imported.Clip, expectedSamplesBase64);
    }

    public static TheoryData<string, int, double, float, float, float, string>
        WrapAndSubframeUnityQuaternionSamples => new()
    {
        { "o1-wrap720", 1, 14d, 720f, 0f, 0f, "AAAAAJxO2D1TkMc+cjlAP4MJfT+JVmA/Jze1Pic3tb6KVmC/ggl9v3Y5QL9QkMe+0k7YvY0fnKWnpDW+zUgdv1d5er+VE0i/FekBPXJsTj9idHA/+ZUNP5g1BD4BMxy9AAAAgAAAAIAAAACAAAAAAAAAAIAAAAAAAAAAgAAAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAKTEjjFZ8WU7EsEfu/wgRb0mKuG9AS+lvfDioD3rpGw+SAeBPlu9PT7vWhQ+AAAAgAAAAIAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAgAAAAIAAAACAAAAAgHcv1jETZgQ8zp0FPR/+Qz0sQxi8FOIIvsrEPb64mpO9y73hPd7LbT6HfYk+AACAP3CRfj/SwGs/xBIpP5ZXGz4jpfa+021vv9Ntb78fpfa+sVcbPsESKT/TwGs/cJF+PwAAgD8b7ns/gc9JP6LgRz65Lx2//rp8v4FfDr/tEHk+zVJJP2Aycj+vmHM/" },
        { "o2-wrap-mixed", 2, 14d, -720f, 450f, -810f, "AAAAAJhSxr26dXa+0SjOvYRYCD4zZy++PDGTvch+MD91fF4+cVcwv6QiML80K/G+wlHvvgAAAL/d9ei+yh0Vv3+1Q79gjcA+Nrz5Pi5wq70RuFq+sPtZPkRogz5ovgI+AAAAgLgBWL3hhye9SPCrPkKkID+Bbxe+Gk5svzmdLb+OQkK/Zxw5v4U/Jz0oww0/jmkQPwAAAD9ALxU/JZ2lPuteIb+pZSm/TOtUvxW7376zQkG/knfbvnBGKL7r2UG+AAAAgIyH/z2YXvM+mTwvP50QnT3sQC2/mI2xvvamQL6bT9y+Th8mPRXSlj4j25S9Tr/QvgAAAL9+6q++dggcPlsNjj0Yq92+tdjpvVKUPL/Z1JG70UAwvz/F0L6hCnW+AACAPxVtfD+/Ylg/HaAjP49nQz8pVzM/Tq4ZvrtyL77aPeA+2McBvYV1Kb8Jyy6/1IcLvwAAAL/6PRS/md86v8cS7b2lSPc+gx11vgZjAj/euh6/YH8Lv+5YXL9El3G/" },
        { "o3-wrap-triple", 3, 14d, 1080f, -540f, 900f, "AAAAAFCBKj57ORc/SMkGPz0YCb/uygW/eHCAvke59741iEY7POcsPxiqwz4IeRy/v1x5vwAAgL/bwmy/E7wovb0YLT9ZBQa9mxAXv4OqBr/IIKU+uHs6v1q2I75nvgI+AAAAgIy6aD0aCzi99SQWv0gln77oEVo/XAoQP1chHz8Gew89iBLNPgYE9769Wga/2KYRvsrJU6VjC36+GDkevxalxj6QCVG+U+35Pl2ZUT/+qMw+SsEEPZaSmL7zWhS+AAAAgDNt8L3WD22+f3CTPXlu0T1HX5i8sqZJPxMQk77KM1o/0D7+PleKQT+AP/I+WHrJPTIxjaRHMjY+4944PzQIDj/Ow3A/3KEHPxqjXbvJCGS+/AL7vaLGrr2ZCnW+AACAP1g1ej9+ikU/n3ocP/pORz/JbfQ8M0V/O06ECz+jkwU/amK9Pv4hZD4FirY+Q1wVPtPolCqZRGg+f6WdPljNlD6PFoo+jJW6PrCTa75zIFS/MVssv8zsb7+wmHO/" },
        { "o4-wrap-triple", 4, 14d, -1080f, 540f, -900f, "AAAAAE+BKr57ORe/SMkGvz0YCT/uygU/dnCAPkG59z63h0a7O+csvxiqw74IeRw/vlx5PwAAgD/HkW0/R6d9PTu6RL8Pi4i93LrVPihERz41iQy/VlQMP6w7kz4HMxw9AAAAgI26aL0YCzg99SQWP0clnz7pEVq/WgoQv1ohH78Tew+9jhLNvgcE9z68WgY/46YRPjIxjSTaC3I+QQoiP/lXiL7/4EY+YG/6vm4BdL9OFsi+kpkiv7X6P77n2UG+AAAAgDNt8D3XD20+gXCTvYFu0b1QX5g8s6ZJvwYQkz7KM1q/xj7+vlWKQb+BP/K+WnrJvTIxjaQKODu+kJk0v65R8L7Y6WG/Kibjvnnya74jq7g9uFmFvgqC4b6GfYm+AACAP1g1ej9+ikU/n3ocP/pORz+rbfQ8DUV/O1GECz+kkwU/dmK9PhAiZD4AirY+OVwVPjIxjaTs1mM+ciSgPl8NsD7Rqdg+ys0fP1liwLzsuzu/5Z30vuRdVL9El3G/" },
        { "o5-wrap-offset", 5, 14d, 721f, -719f, 1081f, "AAAAAD20sj3lEMA9xzGovpz1Ur7ly9w+oOD0Pesb6L4PCNO+PHZwvYtrIL/r8f2+tBvmvZ42EDyNT1O+VeMpv6qcVj0Ukw0/vzeyPUtqEL/IVZK+kzkLvtkcqT0EMxy9AAAAgP4q9j1jbv8+I6AeP1ElTj04ZNM+VMHnvpKb7z3TK92+0uJQPjlVoT4xnt69EIHIvXu3DbwVyQG+rGccPrGpmr59/MQ+OWfoPGEn0r5jl9s6UWUzP6ICrT7s2UE+AAAAgAPqFL4jb7W+SZGBvjGfCb8INUa/m78vv5XhT78qO0K/VoL7vvbBFT8rdxk/7asiPp42ELw6448+cj47P9z52T73ujg/sf18P8neGj+zmom+i2bUvTC8sjyjCnU+AACAP3doej9NC0k/1JkqPz3sUD9XDle+7msOP7Rjsr4yYYW+00JYv0t90L5PZB4/bdp5P3n4fz9su20/mGcTPXjwWT8HSiM+s8P6vTaDxD6qeWs/4E0xP4/ybz+vmHM/" },
        { "o1-tie180", 1, 14d, 180f, -180f, 180f, "AAAAAArO0jxBQ7Y92sMhPt7pRD7NdSQ+G6qAPSOqgL3OdSS+3OlEvtzDIb4+Q7a9Oc7SvOTlGrNffjW9cw0PvpJmTr6iDxu+6fo3OrKUEz7h5jQ+eJvSPWWmqzsBMxy9AAAAgArO0jxAQ7Y92sMhPt3pRD7MdSQ+G6qAPSKqgL3OdSS+3ulEvtzDIb4/Q7a9Os7SvPQHDrMK+BW9UczcvWSvAr5B3Li8qh88Po9Juj4H0M8+zOKiPgOXSz7vWhQ+AAAAgJBD3rzC9d69ZrB6vmM+1r58SRa/jFYxv4xWMb98SRa/XT7Wvm2wer6+9d69xUPevEj5O7M2Hlm9ig9WvoC84b4FcCa/R/cyv00NCL8rpnG+VWgmPa+UWT6HfYk+AACAP3K8fz80bnw/7IdxP6bXXT9b60Y/iTg3P4k4Nz9c60Y/qNddP+uHcT80bnw/crx/PwAAgD9cN38/Ajx2P5+CXT98hT4/mecwPy1TQD++dV0/DwtxPwDrdD+vmHM/" },
        { "o2-tie180", 2, 14d, -180f, 180f, -180f, "AAAAAArO0rxBQ7a92sMhvt3pRL7MdSS+G6qAvSKqgD3NdSQ+3+lEPtzDIT4+Q7Y9Os7SPPruFzN8cyI9t44APlosPD6FXRI+yfE3OsVtIb6dSHK+Bh5fvq8mIr5mvgK+AAAAgArO0rxBQ7a92sMhvt7pRL7MdSS+G6qAvSOqgD3NdSQ+3OlEPtzDIT4+Q7Y9Oc7SPILEJjN9jkE9w/UfPsrygT6NVYg+ox88PtsrpT0sZTE9GguxPbUFID7r2UE+AAAAgJFD3jzC9d49ZrB6PmQ+1j58SRY/jFYxP4xWMT98SRY/XT7WPmqwej6+9d49wkPePHY1FDNmFh494TUjPiwQtz6IixI/R/cyP/dnLD9xtwg/Zm7BPvQ+jD6fCnU+AACAP3K8fz80bnw/7IdxP6bXXT9b60Y/iTg3P4k4Nz9b60Y/qNddP+uHcT80bnw/crx/PwAAgD8+Un8/FXd3P943YT+7IEM/mOcwPzC7Nz/ifU8/NE5lP+SHbz9El3E/" },
        { "o3-tie-turn", 3, 14d, 540f, 180f, -540f, "AAAAAIwTpj01Z54+hHQRP3SfCz/Dva88OGEavzhhGr/Cva88dJ8LP4R0ET8zZ54+WBOmPbSH9jMTIw4+re77Pr+cHD9q0YS9RmI2v6Eueb6CScM+2aObPoQjf7tmvgK+AAAAgLnao7xBSIe7QLVWPiiJIj/ASF4/OebaPjbm2r7ASF6/KYkiv0S1Vr4YSYc7O9ujPDMxDaWWnQA9dRyMvSuHDL/3yU+/beI3umYCXz9HfjU/umuFPirICD7vWhQ+AAAAgJuDnT1SfYE+E7i7PjBUgD6zUMA7KARgveUDYD2sUMC7NVSAvhO4u75UfYG+YoOdvTExDaUhqO69OtWdvpOoU74mD8s9co55PVXDpT11+PU+DvcaPxkGwj6gCnU+AACAPzVYfj+Tq2o/VMw0P9sM+T4MuP0+CdYrPwrWKz8OuP0+2Az5PlXMND+Tq2o/Nlh+PwAAgD/WoXs/7q9PP5XeBz9vbBI/SfcyP5do1j61LLI+yhwwP2lsaj+vmHM/" },
        { "o4-half-frame", 4, 13.5d, 315f, -225f, 495f, "AAAAALBbQD2Pfg4+AIEZPi4jEL2zk6i+6nHovhj2nL5tDA6+ORBJvt/U177a6h+/nKY4v54EN7+Xcw+/FFiFviP0db3wsVq+OYDVvuDYgL7FATc9zaDiPTzsizwBMxy9AAAAgEKNIT08wjg+eGrbPqOoJz+H4yY/kG/YPtxqhj4907s+DwwTP5ITJj9BWxI/Z0v0Pn4x+D7lwRw/+dIlPy8h2z6HF2g+/m/NPr65KT9Onhg/+pa2PoxhXz7r2UE+AAAAgPj3o70S4J2++HYbv3M8P7+6Au2+xLlDPs7QRD9NaWs/8OA/P6ucDj8vq/I+HmrsPpAY7T5QGv8+D5EpP3HKZj9CEEg/vXBzPd3CIb/qjiW/Tmhhvg69Ez6IfYk+AACAP92xfj/Nb2w/X+gmP3KW3j26lwC/9rxCv6r0/b6xLMK8pDCHPiSKmj4wO3c+uOBJPjRxSz6aTYA+OSWLPlnFsTyJZgq/pDlQv+AmoL6ag/I+mbNmP/cMdz9El3E/" },
        { "o5-half-frame", 5, 14.5d, -405f, 585f, -765f, "AAAAANo+gb0+gpe+QUsiv6WJML+8V9C+8L8Fv//zWL/wVY6+MYAYP742JT8st04+ehHFvaugPL40cDa+gYMtPfm/ID/q04Q+OfpIvwYRnr4M7Om+rdcJv++jK77/Mhy9AAAAgPOFlr1pIkW+anLTvZjyWj4141Y+sF/cvWDPUT5nKzk/wL6KPq16Gr/La3m/KABwvyQ7W78nJl6/Kjl8v5uKFr+8hBU/+g6iPmKUCD0+6/Q+ofobPkCNwz3v2UE+AAAAgD6I3T04xtU+xvU1Pwv13z5R0Nu+3A1Pv2GV9r7l9gW/qao+vzsy1r4WDm49z9FlPtxZTT5JrlU+i2cmPrRD4r6EQz2/T9YHv9oOYr+iEhk+6+tHP9UM3j6jCnU+AACAP2VJfT8tWFY/f9CSPro9Cb+zS0e/A4N9vvGWtb3Pg7W+lXUFvnKmVz71eqY9WNx9vr2g4L5IJtS+7vcGvdYigj7HNFm+0QY0vagptL7zIjy/2xKOPs1SYT+vmHM/" },
    };

    [Theory]
    [MemberData(nameof(WrapAndSubframeUnityQuaternionSamples))]
    public void NonXyzRotationOrdersMatchUnityBitsAcrossWrapTiesAndSubframes(
        string caseName, int rotationOrder, double middleFrame,
        float middleX, float middleY, float middleZ,
        string expectedSamplesBase64)
    {
        Assert.False(string.IsNullOrEmpty(caseName));
        var imported = ReimportOrderedAnimation(
            rotationOrder, middleFrame, middleX, middleY, middleZ, importer =>
            {
                importer.resampleCurves = true;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
            });
        AssertQuaternionBits(imported.Clip, expectedSamplesBase64);
    }

    public static TheoryData<string, string, string, string> TransformStackUnitySamples => new()
    {
        { "pre-x", "PreRotation=10,0,0",
          "AAAAgAAAAAAAAAAAtn6yPQAAAIAAAACAngZ/PwAAgD8AAIA/AACAPwAAAAAAAAAAAAAAAArXIzwK1yM8CtcjPA==", "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8tn6yPSam9z2hFji9AAAAgL7BK74npvc9AAAAgI2Agr6ibo8+ngZ/P5DRcT80hXM/AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "pre-mixed", "PreRotation=10,20,30",
          "AAAAgAAAAAAAAAAAATMcPezZQb6hCnW+sJhzPwAAgD8AAIA/AACAPwAAAAAAAAAAAAAAAArXIzwK1yM8CtcjPA==", "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8ADMcPZOhlD1aOdC969lBvnh1uL7Fx6e8oAp1vjwr6b7vCTU8r5hzP2+WTz+Nmn4/AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "post-y", "PostRotation=0,25,0",
          "AAAAgAAAAAAAAAAAAAAAAFmiXT4AAACAie55PwAAgD8AAIA/AACAPwAAAAAAAAAAAAAAAArXIzwK1yM8CtcjPA==", "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8AAAAAFxStj1MKTu+WaJdPqcdrTy8/7A+AAAAgPfHZr4XKnA+ie55P75PeD8z1mM/AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "post-mixed", "PostRotation=-15,25,-35",
          "AAAAgAAAAAAAAAAAjH9pPaPOeD4ZO4e+7n9uPwAAgD8AAIA/AACAPwAAAAAAAAAAAAAAAArXIzwK1yM8CtcjPA==", "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8jH9pPY+qSj76tiy+o854PpH9UT1yFLE+GTuHvu+N6L49Hx297n9uP4n7XT+cFWw/AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "pre-post", "PreRotation=10,20,30;PostRotation=-15,25,-35",
          "AAAAgAAAAAAAAAAAj6pKPpH9UT3vjei+iftdPwAAgD8AAIA/AACAPwAAAAAAAAAAAAAAAArXIzwK1yM8CtcjPA==", "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8j6pKPtSooz4umBC9kf1RPSzmFL7VDkk+743ovuGrGb/sRY2+iftdP2P0Nz8otHA/AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "rotation-pivot", "RotationPivot=25,-10,15",
          "AACAvszMzL2ZmRk+AAAAAAAAAIAAAACAAACAPwAAgD8AAIA/AACAPwAAgD7MzMw9mpkZvhDXIzwI1yM8ENcjPA==", "AACAvvfFnL5TgXW+zMzMva9Frr3uXdm9mZkZPpndVj5KR4g9AAAAAAAzHD1mvgK+AAAAgOvZQb7uWhQ+AAAAgKAKdb6HfYk+AACAP6+Ycz9El3E/AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "rotation-pivot-offset", "RotationPivot=25,-10,15;RotationOffset=5,7,-9",
          "mZmZvo/C9byPwnU9AAAAAAAAAIAAAACAAACAPwAAgD8AAIA/AACAPwAAgD7MzMw9mpkZvhDXIzwI1yM8ENcjPA==", "mZmZvpBftr5DWpS+j8L1vDRMd7yLAxS9j8J1PUdp9T2FKsC8AAAAAAAzHD1mvgK+AAAAgOvZQb7uWhQ+AAAAgKAKdb6HfYk+AACAP6+Ycz9El3E/AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "scaling-pivot", "ScalingPivot=-12,18,6",
          "AAAAgAAAAAAAAAAAAAAAAAAAAIAAAACAAACAPwAAgD8AAIA/AACAPwAAAAAAAAAAAAAAAArXIzwK1yM8CtcjPA==", "AAAAgMYm7LwC9n07AAAAADyj07ulHqw8AAAAAFV1XDvXC7+8AAAAAAAzHD1mvgK+AAAAgOvZQb7uWhQ+AAAAgKAKdb6HfYk+AACAP6+Ycz9El3E/AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "scaling-pivot-offset", "ScalingPivot=-12,18,6;ScalingOffset=4,-8,10",
          "AAAAgAAAAAAAAAAAAAAAAAAAAIAAAACAAACAPwAAgD8AAIA/AACAPwrXI70K16O9zMzMPQjXIzwQ1yM8CNcjPA==", "AAAAgGKk6ru7a308AAAAACTGqzsqWn48AAAAACZHp7xf9Qk8AAAAAAAzHD1mvgK+AAAAgOvZQb7uWhQ+AAAAgKAKdb6HfYk+AACAP6+Ycz9El3E/AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "geometric-helper", "GeometricTranslation=12,-8,20;GeometricRotation=15,25,-10;GeometricScaling=1.5,0.75,2",
          "AAAAgAAAAAAAAAAAAAAAAAAAAIAAAACAAACAPwAAgD8AAIA/AACAP4/C9b0K16O9zMxMPggauTzA/YE86MXRPA==", "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8AAAAAAAzHD1mvgK+AAAAgOvZQb7uWhQ+AAAAgKAKdb6HfYk+AACAP6+Ycz9El3E/AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
    };

    [Theory]
    [MemberData(nameof(TransformStackUnitySamples))]
    public void PrePostRotationsPivotsAndGeometryMatchUnity2022TransformStack(
        string caseName, string propertyOverrides,
        string expectedStaticBase64, string expectedSamplesBase64)
    {
        var imported = ReimportTransformStack(caseName, propertyOverrides);
        var bounds = imported.Mesh.bounds;
        AssertUnitySamples(expectedStaticBase64, new[]
        {
            imported.Root.transform.localPosition.x,
            imported.Root.transform.localPosition.y,
            imported.Root.transform.localPosition.z,
            imported.Root.transform.localRotation.x,
            imported.Root.transform.localRotation.y,
            imported.Root.transform.localRotation.z,
            imported.Root.transform.localRotation.w,
            imported.Root.transform.localScale.x,
            imported.Root.transform.localScale.y,
            imported.Root.transform.localScale.z,
            bounds.center.x, bounds.center.y, bounds.center.z,
            bounds.size.x, bounds.size.y, bounds.size.z,
        }, caseName + ":static");

        var sampledValues = new List<float>(TransformProperties.Length * 3);
        foreach (var property in TransformProperties)
        {
            var binding = imported.Clip.bindings.Single(candidate =>
                string.Equals(candidate.propertyName, property, StringComparison.Ordinal));
            Assert.Equal(string.Empty, binding.path);
            Assert.Equal(Enumerable.Range(0, 24), Frames(binding.curve, imported.Clip.frameRate));
            sampledValues.Add(binding.curve.keys[0].value);
            sampledValues.Add(binding.curve.keys[13].value);
            sampledValues.Add(binding.curve.keys[23].value);
        }
        AssertUnitySamples(expectedSamplesBase64, sampledValues, caseName + ":curves");
    }

    public static TheoryData<string, string, string, string> TransformStackRawUnitySamples => new()
    {
        { "pre-x", "PreRotation=10,0,0",
          "m_LocalPosition.x:3,m_LocalPosition.y:3,m_LocalPosition.z:3,m_LocalRotation.w:24,m_LocalRotation.x:24,m_LocalRotation.y:24,m_LocalRotation.z:24,m_LocalScale.x:24,m_LocalScale.y:24,m_LocalScale.z:24",
          "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8ngZ/P5HRcT80hXM/tX6yPSam9z2hFji9AAAAAL/BK74qpvc9AAAAAI2Agr6hbo8+AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "pre-mixed", "PreRotation=10,20,30",
          "m_LocalPosition.x:3,m_LocalPosition.y:3,m_LocalPosition.z:3,m_LocalRotation.w:24,m_LocalRotation.x:24,m_LocalRotation.y:24,m_LocalRotation.z:24,m_LocalScale.x:24,m_LocalScale.y:24,m_LocalScale.z:24",
          "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8sJhzP2+WTz+Nmn4//jIcPZOhlD1ZOdC97NlBvnl1uL7Ex6e8oAp1vj4r6b7vCTU8AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "post-y", "PostRotation=0,25,0",
          "m_LocalPosition.x:3,m_LocalPosition.y:3,m_LocalPosition.z:3,m_LocalRotation.w:24,m_LocalRotation.x:24,m_LocalRotation.y:24,m_LocalRotation.z:24,m_LocalScale.x:24,m_LocalScale.y:24,m_LocalScale.z:24",
          "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8ie55P75PeD801mM/AAAAAFtStj1MKTu+WaJdPqMdrTy8/7A+AAAAAPfHZr4YKnA+AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "post-mixed", "PostRotation=-15,25,-35",
          "m_LocalPosition.x:3,m_LocalPosition.y:3,m_LocalPosition.z:3,m_LocalRotation.w:24,m_LocalRotation.x:24,m_LocalRotation.y:24,m_LocalRotation.z:24,m_LocalScale.x:24,m_LocalScale.y:24,m_LocalScale.z:24",
          "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW87n9uP4n7XT+cFWw/jX9pPY+qSj77tiy+o854PpD9UT1yFLE+GTuHvu+N6L4/Hx29AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "pre-post", "PreRotation=10,20,30;PostRotation=-15,25,-35",
          "m_LocalPosition.x:3,m_LocalPosition.y:3,m_LocalPosition.z:3,m_LocalRotation.w:24,m_LocalRotation.x:24,m_LocalRotation.y:24,m_LocalRotation.z:24,m_LocalScale.x:24,m_LocalScale.y:24,m_LocalScale.z:24",
          "AAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8iftdP2P0Nz8otHA/j6pKPtSooz4xmBC9kP1RPS3mFL7VDkk+743ovuKrGb/sRY2+AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "rotation-pivot", "RotationPivot=25,-10,15",
          "localEulerAnglesRaw.x:24,localEulerAnglesRaw.y:24,localEulerAnglesRaw.z:24,m_LocalPosition.x:24,m_LocalPosition.y:24,m_LocalPosition.z:24,m_LocalScale.x:24,m_LocalScale.y:24,m_LocalScale.z:24",
          "AAAAAAAAIEEAACDBAAAAgAAAoMEAAKBBAAAAgAAA8MEAAPBBAAAAgNiNNT3FK/m9AAAAAMIw5b345wI9AAAAAEBxEj6KD/e9AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "rotation-pivot-offset", "RotationPivot=25,-10,15;RotationOffset=5,7,-9",
          "m_LocalPosition.x:24,m_LocalPosition.y:24,m_LocalPosition.z:24,m_LocalRotation.w:24,m_LocalRotation.x:24,m_LocalRotation.y:24,m_LocalRotation.z:24,m_LocalScale.x:24,m_LocalScale.y:24,m_LocalScale.z:24",
          "zMxMvaT3ubsWyS++KVyPPTOpK70l0NA961G4vSkhWT27sFe+AACAP7CYcz9El3E/AAAAAP4yHD1nvgK+AAAAAOzZQb7wWhQ+AAAAAKAKdb6IfYk+AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "scaling-pivot", "ScalingPivot=-12,18,6",
          "localEulerAnglesRaw.x:24,localEulerAnglesRaw.y:24,localEulerAnglesRaw.z:24,m_LocalPosition.x:24,m_LocalPosition.y:24,m_LocalPosition.z:24,m_LocalScale.x:24,m_LocalScale.y:24,m_LocalScale.z:24",
          "AAAAAAAAIEEAACDBAAAAgAAAoMEAAKBBAAAAgAAA8MEAAPBBAAAAgMYm7LwC9n07AAAAADyj07ulHqw8AAAAAFV1XDvXC7+8AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "scaling-pivot-offset", "ScalingPivot=-12,18,6;ScalingOffset=4,-8,10",
          "localEulerAnglesRaw.x:24,localEulerAnglesRaw.y:24,localEulerAnglesRaw.z:24,m_LocalPosition.x:24,m_LocalPosition.y:24,m_LocalPosition.z:24,m_LocalScale.x:24,m_LocalScale.y:24,m_LocalScale.z:24",
          "AAAAAAAAIEEAACDBAAAAgAAAoMEAAKBBAAAAgAAA8MEAAPBBCtcjvay8Cb5JqRA9CtejvQ9KZ70NFAK9zMzMPV+pjT1LhMQ9AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
        { "geometric-helper", "GeometricTranslation=12,-8,20;GeometricRotation=15,25,-10;GeometricScaling=1.5,0.75,2",
          "localEulerAnglesRaw.x:3,localEulerAnglesRaw.y:3,localEulerAnglesRaw.z:3,m_LocalPosition.x:3,m_LocalPosition.y:3,m_LocalPosition.z:3,m_LocalScale.x:3,m_LocalScale.y:3,m_LocalScale.z:3",
          "AAAAAAAAIEEAACDBAAAAgAAAoMEAAKBBAAAAgAAA8MEAAPBBAAAAgArXI7wK1yM8AAAAAArXozwK16O8AAAAAI/C9TyPwvW8AACAP83MjD9mZmY/AACAP5qZmT/NzEw/AACAP2Zmpj8zMzM/" },
    };

    [Theory]
    [MemberData(nameof(TransformStackRawUnitySamples))]
    public void TransformStackWithoutResamplingMatchesUnity2022BindingStrategy(
        string caseName, string propertyOverrides,
        string expectedLayout, string expectedSamplesBase64)
    {
        var imported = ReimportTransformStack(caseName, propertyOverrides, false);
        var bindings = imported.Clip.bindings
            .OrderBy(binding => binding.propertyName, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedLayout, string.Join(",", bindings.Select(binding =>
            binding.propertyName + ":" + binding.curve.length.ToString(CultureInfo.InvariantCulture))));

        var sampledValues = new List<float>();
        foreach (var binding in bindings)
        {
            var keys = binding.curve.keys;
            if (keys.Length <= 3)
                sampledValues.AddRange(keys.Select(key => key.value));
            else
            {
                sampledValues.Add(keys[0].value);
                sampledValues.Add(keys[13].value);
                sampledValues.Add(keys[23].value);
            }
        }
        AssertUnitySamples(expectedSamplesBase64, sampledValues,
            caseName + ":raw-curves");
    }

    public static TheoryData<string, string> PivotRawEulerUnitySamples => new()
    {
        { "rotation-positive", "RotationPivot=25,-10,15" },
        { "rotation-negative", "RotationPivot=-35,20,-5" },
        { "rotation-fractional", "RotationPivot=0.5,-0.75,1.25" },
        { "scaling-positive", "ScalingPivot=30,20,10" },
        { "scaling-negative", "ScalingPivot=-12,-18,-6" },
        { "scaling-fractional", "ScalingPivot=-0.5,0.75,-1.25" },
        { "scaling-offset-positive", "ScalingPivot=-12,18,6;ScalingOffset=4,-8,10" },
        { "scaling-offset-negative", "ScalingPivot=12,-18,-6;ScalingOffset=-4,8,-10" },
        { "rotation-large", "RotationPivot=1250,-750,500" },
        { "scaling-large", "ScalingPivot=-900,1100,-1300" },
    };

    [Theory]
    [MemberData(nameof(PivotRawEulerUnitySamples))]
    public void PivotRawEulerUsesUnity2022KfCurveBits(
        string caseName, string propertyOverrides)
    {
        var imported = ReimportTransformStack(caseName, propertyOverrides, false);
        var x = Curve(imported.Clip, "localEulerAnglesRaw.x");
        var y = Curve(imported.Clip, "localEulerAnglesRaw.y");
        var z = Curve(imported.Clip, "localEulerAnglesRaw.z");
        foreach (var curve in new[] { x, y, z })
            Assert.Equal(Enumerable.Range(0, 24), Frames(curve, imported.Clip.frameRate));
        AssertUnityBits(UnityRawEulerXBase64,
            x.keys.Select(key => key.value).ToArray(),
            caseName + ":euler-x");
        AssertUnityBits(UnityRawEulerYBase64,
            y.keys.Select(key => key.value).ToArray(),
            caseName + ":euler-y");
        AssertUnityBits(UnityRawEulerZBase64,
            z.keys.Select(key => key.value).ToArray(),
            caseName + ":euler-z");
    }

    private static void AssertQuaternionBits(
        AnimationClip clip, string expectedSamplesBase64)
    {
        var expectedBytes = Convert.FromBase64String(expectedSamplesBase64);
        Assert.Equal(QuaternionProperties.Length * 24 * sizeof(float), expectedBytes.Length);
        var expectedIndex = 0;
        var mismatches = new List<string>();
        foreach (var property in QuaternionProperties)
        {
            var keys = Curve(clip, property).keys;
            Assert.True(keys.Length == 24,
                $"{property} has {keys.Length} keys at {string.Join(",", keys.Select(key => key.time.ToString("R", CultureInfo.InvariantCulture)))}");
            for (var frame = 0; frame < keys.Length; frame++)
            {
                var bits = BinaryPrimitives.ReadInt32LittleEndian(
                    expectedBytes.AsSpan(expectedIndex * sizeof(float), sizeof(float)));
                var actualBits = BitConverter.SingleToInt32Bits(keys[frame].value);
                if (bits != actualBits)
                    mismatches.Add($"{property}[{frame}]={bits:X8}/{actualBits:X8}");
                expectedIndex++;
            }
        }
        Assert.True(mismatches.Count == 0, string.Join(", ", mismatches));
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
        Action<ModelImporter> configure) =>
        ReimportOrderedAnimation(rotationOrder, 14d, middleX, middleY, middleZ, configure);

    private (GameObject Root, AnimationClip Clip) ReimportOrderedAnimation(
        int rotationOrder, double middleFrame,
        float middleX, float middleY, float middleZ,
        Action<ModelImporter> configure)
    {
        var path = CopyAnimatedFixture();
        var fullPath = FullPath(path);
        var fixture = File.ReadAllText(fullPath);
        var orderedFixture = fixture.Replace(
            "Property: \"RotationOrder\", \"enum\", \"\",0",
            "Property: \"RotationOrder\", \"enum\", \"\"," + rotationOrder,
            StringComparison.Ordinal);
        const long frameTicks = 1_924_423_250;
        var middleTicks = checked((long)(middleFrame * frameTicks));
        foreach (var (original, replacement) in new[]
        {
            (10f, middleX), (20f, middleY), (30f, middleZ),
        })
        {
            orderedFixture = orderedFixture.Replace(
                $"26941925500,{original.ToString("R", CultureInfo.InvariantCulture)},U,s,0,0,n",
                $"{middleTicks},{replacement.ToString("R", CultureInfo.InvariantCulture)},U,s,0,0,n",
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

    private (GameObject Root, AnimationClip Clip, Mesh Mesh) ReimportTransformStack(
        string caseName, string propertyOverrides, bool resampleCurves = true)
    {
        var path = CopyAnimatedFixture();
        var fullPath = FullPath(path);
        var fixture = File.ReadAllText(fullPath);
        foreach (var propertyOverride in propertyOverrides.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = propertyOverride.IndexOf('=');
            Assert.True(separator > 0, caseName + ": invalid property override");
            var property = propertyOverride[..separator];
            var value = propertyOverride[(separator + 1)..];
            var prefix = $"Property: \"{property}\", \"Vector3D\", \"\",";
            var line = fixture.Split('\n').Single(candidate => candidate.Contains(prefix, StringComparison.Ordinal));
            var replacement = line[..line.IndexOf(prefix, StringComparison.Ordinal)] + prefix + value;
            fixture = fixture.Replace(line, replacement, StringComparison.Ordinal);
        }
        File.WriteAllText(fullPath, fixture);
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        importer.resampleCurves = resampleCurves;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        return (
            AssetDatabase.LoadAssetAtPath<GameObject>(path)!,
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(),
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single());
    }

    private static void AssertUnitySamples(
        string expectedBase64, IReadOnlyList<float> actual, string context,
        float maxAbsoluteError = 1e-6f)
    {
        var expectedBytes = Convert.FromBase64String(expectedBase64);
        Assert.Equal(actual.Count * sizeof(float), expectedBytes.Length);
        var mismatches = new List<string>();
        for (var index = 0; index < actual.Count; ++index)
        {
            var expectedBits = BinaryPrimitives.ReadInt32LittleEndian(
                expectedBytes.AsSpan(index * sizeof(float), sizeof(float)));
            var expected = BitConverter.Int32BitsToSingle(expectedBits);
            var error = MathF.Abs(expected - actual[index]);
            if (!float.IsFinite(actual[index]) || error > maxAbsoluteError)
                mismatches.Add($"{context}[{index}]={expected:R}/{actual[index]:R} ({error:R})");
        }
        Assert.True(mismatches.Count == 0, string.Join(", ", mismatches));
    }

    private static void AssertUnityBits(
        string expectedBase64, IReadOnlyList<float> actual, string context)
    {
        var expectedBytes = Convert.FromBase64String(expectedBase64);
        Assert.Equal(actual.Count * sizeof(float), expectedBytes.Length);
        var mismatches = new List<string>();
        for (var index = 0; index < actual.Count; ++index)
        {
            var expectedBits = BinaryPrimitives.ReadInt32LittleEndian(
                expectedBytes.AsSpan(index * sizeof(float), sizeof(float)));
            var actualBits = BitConverter.SingleToInt32Bits(actual[index]);
            if (expectedBits != actualBits)
                mismatches.Add($"{context}[{index}]={expectedBits:X8}/{actualBits:X8}");
        }
        Assert.True(mismatches.Count == 0, string.Join(", ", mismatches));
    }

    private static AnimationCurve Curve(AnimationClip clip, string property) =>
        clip.bindings.Single(binding => string.Equals(binding.propertyName, property, StringComparison.Ordinal)).curve;

    private static readonly string[] TransformProperties =
    {
        "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
        "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
        "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z",
    };

    private const string UnityRawEulerXBase64 =
        "AAAAAA90LD6tISM/NAmtP+p8EEB0MlNAK5ONQNVsskDGZtZAi8H3QNpeCkHlzRVBME4dQQAAIEE+ChdBpHD9QI/CtUCkcD1AAAAAAKVwPcCPwrXAo3D9wD4KF8EAACDB";
    private const string UnityRawEulerYBase64 =
        "AAAAgA90rL6tIaO/NAktwOp8kMB0MtPAK5MNwdVsMsHGZlbBi8F3wdpeisHlzZXBME6dwQAAoME+CpfBpHB9wY/CNcGkcL3AAAAAgKVwvUCPwjVBo3B9QT4Kl0EAAKBB";
    private const string UnityRawEulerZBase64 =
        "AAAAgAtXAb+DsvS/58aBwF672MDXZR7BwFxUwaDRhcEUzaDBKNG5wUaOz8HXtODBSPXrwQAA8MFcj+LBexS+wexRiMF7FA7BAAAAgHsUDkHsUYhBexS+QVyP4kEAAPBB";

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
