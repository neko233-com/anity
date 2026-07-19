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

    public static TheoryData<string, string, string> PrePostUnityQuaternionSamples => new()
    {
        { "pre-x", "PreRotation=10,0,0", "tn6yPdd2tT3Ka709gKrIPQBd1T0pyeE90IPsPdCW9D1rl/k9Xar7PVN0+z219vk93ln4PSam9z2Pu/k9msX7PUQs9T12/t09tn6yPZgMaj3N0Lc8RXAwvDYyEb2hFji9AAAAAEspJ7sJRx+8yb6qvJthEL1F4VW9AUORvQhIub199eC98hMDvq9aE75qPiC+krYovr7BK753oyG+PkMGvq7vvL2LCT+9qdGCMtvKLT04Zp09Xr7PPXuU7T0npvc9AAAAAPAxmLv6oI+8s8IXvQQWfL0X+7a9O1TzvV7uF75GyjS+bOJOvtEBZb78E3a+KouAvo2Agr735ne+p0NTvseiGr5ugqS9vHfuMv04qT30OSM+zL9jPjGHhz6ibo8+ngZ/P1D9fj/b2X4/IIl+P271fT95Dn0/xM57P4U+ej8RdHg/SpJ2P5DFdD/OP3M/BzRyP5DRcT9XFHM/9Dp2P8kWej/XYH0/ngZ/PyJ5fj/v5Hs/0zd4P1rodD80hXM/" },
        { "pre-x-negative", "PreRotation=-10,0,0", "tn6yvTSFr73yfae90vqbvYasjr3WL4G9kMVpvVWGVb1Hska99WE9vVHMOL0RgTe9OMY3vaEWOL3TfDe9rTk8vZHxU703XoW9tn6yvYFD7r0HVhi+NUU3vs7bTb5peFa+AAAAAIB6WbtOvU68bdvcvEv2Ob3wFom9+k65vU07670fKg6++wIlvtvhOL5Uiki+8spSvml4Vr4NO0q+mOgovjPF773ARHW97PipMirsZT0sstM9jtcNPsINJD6+wSs+AAAAAIdeh7s8PH+8UqEGvUgvX72JoaG9a2jWvTuJBb79gh6+3/o0vq7wR753g1a+f+Jfvrs2Y75bEVi+gL04vuHhB75vbJG98ZLUMvaPlz2fFBM+hkBOPoJPdj6MgII+ngZ/P/MNfz9AF38/OwZ/P1a7fj/pHH4/rx19P2LAez+xGHo/wkl4P9yCdj+9+nQ/EupzPzSFcz+nznQ/WPR3P3+cez/2WH4/ngZ/P7UlfT+eEHk/5vdzP3Gcbz+i0m0/" },
        { "pre-y", "PreRotation=0,25,0", "AAAAADquGzuf5RA8dc+VPKjQ8Ty9iik9tbBYPSyPgT0KV5M9cDChPYwiqz3HmLE9GCu1PV1Stj28OLI9JUujPYVUgz2XMRo9NQ9xsmCDPL3JMcK99FoOvhDdLr5MKTu+WaJdvmeTYL7Fy2i+IWh1vmLBgr7UFYy+5zKWvrqUoL4ftaq+Zg+0vqkjvL5XeMK+LZnGvnATyL7SJsO+Cqa1vkKFob5CCYm+WaJdvhIfK75Jgf69vO+6vW7ij706pIC9AAAAADOUgrtFxXa8UqACvb6HWb3MXp69Hk3TvXFlBL76GR6+O4g1vtyMSb4pGlm+Cy9jvvfHZr4OxVq+0Xs5vvDLBr5PPo69zi7CMqIEkD1sAAo+FY8/Pr01Yz4XKnA+ie55P8nDeT+1QXk/h1x4P5sFdz+rNHU/G+5yPyhGcD9KYW0/N3JqPzy2Zz+hcGU/o+VjPzxVYz9HMGU/VexpP54EcD9p1HU/ie55P0R7ez8EfXo/ytl3PxQadT9G5HM/" },
        { "pre-z", "PreRotation=0,0,-35", "AAAAAIAZFTvSNws8RqqQPOEQ6zwwJCY96E9WPRhxgT2syJQ9LoSkPXZssD0Vo7g9onO9Pb8Rvz0udrk9XfumPRVWgz2HshY9LrxqskMAMb28jLO9YxcCvteaHr7GTSm+AAAAAJJpG7s6uhS8inmgvHbKCL0gd0y9nS6MvUd9tL01HN295OIBvjYIE77NwiC+xNopvgAjLb4mQSK+dTkFvjk0uL1HGTa9DYZyMlmnHD2DoYk9QpawPdqvxT3RWMw9HPaZPunOlz4u0JE+Qa2IPq85ej4NtF8+qkVDPm9pJj7ejwo+Ji3iPdqGtj1Fi5Q9pPd8PX02bT2R5ZA9kpHZPdLQIz7VVmg+HPaZPqmdvz5RfOE+SXH8PlQKBz8SNgo/yyZ0P6d8dD/XYHU/IZ52P7H7dz/2RXk/HFV6PwYRez/Ecns/ooN7Py1aez+SFXs/u9d6P0++ej9LDHs/kn97P50dez+m4ng/yyZ0P5XvbD84F2Q/YTlbP4JoVD+8vFE/" },
        { "pre-mixed", "PreRotation=10,20,30", "ADMcPQRnIj0KAjM9RW1KPRPJZD1IdH49nzyKPbBukj0PZ5c9JlOZPSnlmD0yLpc98WWVPZOhlD0w7JY9+WSZPe4Ekz2FonY9/jIcPQNu2Tth9/W8FK2FvfR8u71aOdC969lBvlLnRL4iZ02+FltavkW/ar48iH2+g9KIvngCk77ez5y+v76lvtJdrb5mRrO+0hi3vnh1uL5d6LO+KECnvrvsk77eane+7NlBvoGDDL63Ubm9RrlavZsM77zFx6e8oAp1vospeb75SYK+KOiKvputlb7x2KG+ka6uvgJ+u75apse+vpjSvivY277l9eK+dIrnvjwr6b5OuOO+DW7UvkyivL7z552+ngp1vnMZK77Udsq9G8QrvfvoUbvvCTU8r5hzP3sqcz8W7nE/LfFvP28/bT+t6mk/FBBmP2zaYT/GgV0/JUlZP/h6VT86ZVI/rlVQP2+WTz/RDlI/loxYP7l1YT+KB2s/sJhzP1jweT/2kn0/sNx+P3DQfj+Nmn4/" },
        { "post-y", "PostRotation=0,25,0", "AAAAADquGzug5RA8ds+VPKnQ8Ty+iik9uLBYPSyPgT0JV5M9cDChPYwiqz3HmLE9GSu1PVxStj29OLI9IkujPYNUgz2XMRo9Q798sl6DPL3HMcK99FoOvg/dLr5MKTu+WaJdPpOvWj57YFI+Km9FPhmTND7MlyA+/2kKPuE15j0Dt7c94OOLPbW2Sj1Ccw09oivKPKcdrTwXzwY9Dl2EPVfw4T21LCc+WaJdPl0Ehz6TDpo+EiOnPnGRrj68/7A+AAAAADOUgrtGxXa8UqACvb+HWb3MXp69G03TvXFlBL76GR6+O4g1vtyMSb4qGlm+Cy9jvvfHZr4PxVq+1Hs5vvHLBr5PPo691LXLMqIEkD1rAAo+FY8/Prw1Yz4XKnA+ie55P1sXej/Nf3o/LwV7P/eBez9K1Xs/Z+h7P8ixez8gNns/W4d6PwDCeT9aCXk/sIJ4P75PeD/l83g/8WN6PzWpez8gwXs/ie55P9j/dT/fdnA/IYFqPx+9ZT8z1mM/" },
        { "post-y-negative", "PostRotation=0,-25,0", "AAAAAK3L9TmAQNg69+lKO3AtjTvsX547Fk+OOyt2MDubQUi44zdru1wb9LvpXDS8V6NevKuZbryzGzu8EomMuwflIjsjJJw79topsX98RLx1bO284j5AvRfUeb16QYi9WaJdvmiTYL7Fy2i+Imh1vmLBgr7VFYy+5zKWvrqUoL4htaq+Zg+0vqkjvL5XeMK+LpnGvnATyL7SJsO+DKa1vkOFob5CCYm+WaJdvhIfK75Igf69ve+6vW7ij706pIC9AAAAAM09l7tvcY68PQsWvUE/eL2mW7O95ErtvWteE75DbC6+s5VGvjTUWr4LSmq+US50vlqvd76+7mu+WpxKviTrFb60f6G908nsMvpbqj1mGyY+9cVpPkzpiz5eYZQ+ie55P8nDeT+1QXk/h1x4P5sFdz+sNHU/G+5yPyhGcD9KYW0/N3JqPzy2Zz+gcGU/o+VjPzxVYz9HMGU/VexpP54EcD9p1HU/ie55P0R7ez8EfXo/ytl3PxQadT9G5HM/" },
        { "post-mixed", "PostRotation=-15,25,-35", "jH9pPbufdj0dUI09GvmnPTleyD2Rous9OpQHPs5cGD4VUic+deMzPovEPT5O3UQ+eC5JPo+qSj4emEU+KOc1PiLPGT7uX+A9iH9pPRgcTLtJ7IK9VgzwvaBeHr76tiy+o854Pkghdj5Kim4+cpdiPt7PUj7t0T8+Q2YqPoeHEz5QwPg9I4HMPaoipT0IiYU9ZSNhPZH9UT35F4I9LdnEPZBqET7oG0Y+o854PiuvkT6xMqE+/tKqPsKnrz5yFLE+GTuHvgkQib4QIo6+/saVvhZQn76jD6q+aF61voigwL5TScu+Id3Uvqnw3L5mJeO+/yLnvu+N6L7TzuO+QHfWvhKhwb40laa+GTuHvh90TL7RmA2+6Iyxva2BUr09Hx297n9uPztcbj+V8W0/wjptP8QwbD/n0Go/0CBpP3cwZz9OGmU/2wFjP2ARYT/hdl8/+WBeP4n7XT+OSV8/qaJiP/cAZz/tSGs/7n9uP/gIcD+31G8/iW1uP87QbD+cFWw/" },
        { "pre-post-mixed", "PreRotation=10,20,30;PostRotation=-15,25,-35", "j6pKPgDSTT62bFY+7ghjPpUfcj7YG4E+GAKJPok9kD4LcpY+YG6bPj0onz4bsqE+BCujPtSooz7l8qE+2zScPvDZkD6lHn0+j6pKPi4CDj7nfpw9YselPJlWpLwumBC9kf1RPRakRT0w8iI9AmjaPLLHHjwzHSi8mv8DvdXNYb3BVp+9gdLKvczX8L16dwe+OFkRvizmFL4gGAm+mUPSvTxdar3V73K7kf1RPSIZ0D2HOxI+K6gwPozeQj7VDkk+743ovp/46b6r4+2+m8nzvqch+750sgG/ZQkGv1ZaCr8VcA6/8RsSvyo1Fb9klxe/OSAZv+GrGb+E2Be/OrkSv6i8Cr9lXAC/743ovmfmzr4PM7a+tjGhvlqrkr7sRY2+iftdPwR5XT9NBlw/5LxZPwC1Vj/UC1M/cedOP0F4Sj9x+EU/6qlBP4LTPT8Qvjo/2LE4P2P0Nz8JaDo/+OpAPzoPSj+DQlQ/iftdP13+ZT81n2s/0eBuP1tUcD8otHA/" },
        { "pre-post-fractional", "PreRotation=0.5,-0.75,1.25;PostRotation=-1.5,2.25,-3.75", "HT+KPLIslzydBbo8nt3rPPSgEj3Vjy89wcFJPfwVXz3vcW49OMx3PQcSfD2N5Xw9LTd8PeS7ez3q2Xw9R+t4PeW8YD2nnCY9GT+KPNYWdryhAFC9f7usvRsx3723fPK9+3nZPIFOwTwe2Xo8U6IiO4v/ZrziiAm9rEthvQZcnr1IlMu9zuP1vQFuDb7uDBy+SKYlvgMZKb63oR2+siD9vWaHor3rqt68BnrZPLNrmz2wZO495DwVPitLJz47ci0+S6QxvcF8Q7368nS959ifvQCczr0CwAG+R6sdvqCOOb5dCVS+4eFrvhgDgL5Zwoe+ar6MvuCDjr7xlYi+JOBvviQLPL4XXfK9RKQxvTynDT0Judw9fnUsPk9zVj4U2GU+36F/P+CXfz8Vcn8/nxx/P+GAfj/FjX0/bz18P0qYej+ctXg/87l2PwnUdD+gOHM/yR1yP761cT+5CnM/4F12P3huej9y5H0/36F/P0cUfz9/bHw/VqJ4P/44dT8hy3M/" },
    };

    [Theory]
    [MemberData(nameof(PrePostUnityQuaternionSamples))]
    public void PrePostRotationsMatchUnityQuaternionBitsAcrossEveryResampledFrame(
        string caseName, string propertyOverrides, string expectedSamplesBase64)
    {
        var imported = ReimportTransformStack(caseName, propertyOverrides);
        AssertQuaternionBits(imported.Clip, expectedSamplesBase64);
    }

    [Theory]
    [MemberData(nameof(TransformStackRawUnitySamples))]
    public void TransformStackWithoutResamplingMatchesUnity2022BindingStrategy(
        string caseName, string propertyOverrides,
        string expectedLayout, string expectedSamplesBase64)
    {
        var imported = ReimportTransformStack(caseName, propertyOverrides, false);
        var bindings = imported.Clip.bindings
            .Where(binding => binding.type == typeof(Transform))
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

    public static TheoryData<string, string, string> PivotRawPositionUnitySamples => new()
    {
        { "rotation-positive", "RotationPivot=25,-10,15",
          "AAAAgByFzzqUG8A7DsZEPGacnDxlbtc8RkgGPSm4Gz252Co90qozPcc+Nz3qZDc9WTg2PdiNNT0VRTc9IKs0PVddHT2FPMU8AAAAgBN5AL2doYS9h7TAvbkG6r3FK/m9AAAAAGuWvLoPdra7edpHvJArrbyNbwO9JZI2vUJTbb0IP5K94U6svbckw70iLNW9PffgvcIw5b0yHte9LsewvZ1gcr0wB+m8AAAAAIcnrDx+/QM9cB8PPUtYCD345wI9AAAAAN3mFDuNZg080NeWPFil/TzKvzo9Jjh8Pc0HoD0HeME93bPgPdf8+z39ygg+BeQPPkBxEj4q9gk+OQvmPR0eoz03Hic9AAAAABvvHr2WH5S9YBPJvaz+6r2KD/e9" },
        { "rotation-negative", "RotationPivot=-35,20,-5",
          "AAAAgG5yELu71gS8Fq+GvCyT07zZQQ+9SnkvvRKoR71Awla9R2tdvQylXb1JTFq94XdWvbXIVL1EwVm9d91dvVdoSb2M1gO9AAAAgFSrOj0G0sc9JYwVPotZOT6v10Y+AAAAAEJvVztJoU08LNvcPBT6Oj3fc4o986i7PVNe7j133A8+0nsmPnLPOT6bxEg+hGpSPsrYVT7uXUo+Pk0qPuD08j0+Znc9AAAAAMEWW72hYr+9orXxvQv0BL4eVgi+AAAAAMdTDLvU6AS8bzSNvAsq7Lx4wiy9h6JnvYfUkb2d6K69F5nJve+G4L3zafK9Rwr+vd0YAb7XVfS9hB3OvUWJlL327Rq9AAAAAJkzGD3CiY89j3XEPUqx5j1n6/I9" },
        { "rotation-fractional", "RotationPivot=0.5,-0.75,1.25",
          "AAAAgFlM8reXm+e4qrp6uUlg2LmuqSW6sn5ruvSnnrrudsy6a8X8uj/dFbs0ICq7JUk4uz6LPbsYaSy7N80Cu2ylorodOhK6AAAAgKmKBDoO8oo6ATLbOvp7EDt7NB87AAAAAAQ7rDn9laE650EpO/8Zizsnpsc7GEADPG8xIjwLRT88BU9ZPBhIbzzUHIA8hZWFPKOIhzy/BIE85aldPCDvJDzNJbQ7AAAAAFi2x7skZ0i8q6CQvBY0sLyiGry8AAAAAML/ETqpSgo7PfuSOysS9jsAVjQ8c3dyPNM3mTwQmLg8M87VPCZH7zy5tgE90VUIPUy3Cj3PzQI9XsvaPNcenDxbnSE8AAAAAPziHrxUH5e8ayPRvA/397wjFAO9" },
        { "scaling-positive", "ScalingPivot=30,20,10",
          "AAAAgHsLrzlGcqE6CYMkO1chgjtyxrE78N7bO8x7/DuAvAg8YmINPJQ3DTwvLgo8HL0GPBw3BTyXsQk8uqoNPL7j/jsdIaM7AAAAgDmG37tJ5m+8r6S1vLX647wY7/W8AAAAAHgjsrnGmay6rqM9u549pbvDBP27fNYxvE+jarxoEJO8xl+wvKYmy7yTGeG89+DvvD5C9byrg+O87oe1vG/8b7xKmt+7AAAAAORMozvdJgA8LL0PPD1QDTzZjAk8AAAAAFmWhjXlwXc3n8yROGnDVjlTyPM5pT5pOo/wxDqIjRY7KYdTOxayiTu0u6Y7eLq7OwSpwzt8Fao7yjlfO8Z6zTqcWsA5AAAAAExWgjmA7UA6wsiYOjfKuTrAF8Q6" },
        { "scaling-negative", "ScalingPivot=-12,-18,-6",
          "AAAAgCZkwLnkHLG6SRg0u49QjrviwsK7AGryu7/6DLwO5Ru8a+8lvFS9K7x0aS68O0AvvBtgL7ybmS68ZkknvOaKDrxpi7K7AAAAgNyq+jtckok80kTUPCfRBj0JDBI9AAAAAKt0dzpVqmo7Psf5O/NiUTw2oZk8ArjOPPitAj1XdB09c1o2PWkKTD0LLl09xm9oPYN7bD2eCF89T5o6PVQoBT3Nook8AAAAAK8ChbwpK/i8Cp0nvaV3Qr1Ktku9AAAAAHwQVDpn/Ug7IMTVOwsGMzx5MYM8/kCwPKBj3jzaogU9Sk8aPckfLD1NDTo9Lh5DPQFcRj2wjDs9zdAdPfKO4jzjKms8AAAAAMY8ZbwBDNi87RoUvaVfLr3c2ze9" },
        { "scaling-fractional", "ScalingPivot=-0.5,0.75,-1.25",
          "AAAAgLwLOrk8ozG6NRq/uvNqIruGLHK7dL+lu+Ex1bu0iQK80V0ZvNKmLbxA6z28h7RIvK6ZTLydsD+8J1AdvPuR2buG1Ve7AAAAgHQOQDvtIK87OWXrO3BhCTzGjRA8AAAAAMRKozkXV5o61pMjO0SMiDsnuMc7ZyEGPHmSKTwJnkw8OZdtPBhXhTw3+ZA8vK+YPC55mzxXPZI8+UJzPPbMLDweFrM7AAAAACBVtbsg5DC8XRh7vAe6l7wBmKG8AAAAAIL3FDoh0ww7CDiVOzTX+Ds0iDU8AdpyPFmpmDyw+bY8NOjSPB4J6zwA+f08cSwFPTJkBz3HAQA9RKbXPLJ6mzxp7iI8AAAAAMUtJLwUwJ28JADcvO8OA70jzAq9" },
        { "scaling-offset-positive", "ScalingPivot=-12,18,6;ScalingOffset=4,-8,10",
          "CtcjvUKqKr1Nwj29AhBbvXcvgL3QnJW9eHmsvZRnw73YHdm963/svVWq/L3UdgS+oFwIvqy8Cb78HAW+1bjvvbdyxb2uo469CtcjvQtgQ7wXLiw802LPPEc5Bz1JqRA9CtejvY4/pL0bJKW9W9ClvRl8pb1kgaO9EoWfvWyNmb1QBJK996aJvWlpgb2lrHS9oORqvQ9KZ73UFnO9shqIvTjsmL0HVqS9CtejvQsplb3hmne94kg/vYc5E70NFAK9zMzMPYE9zD2Ykso9PbrHPeacwz1kNb49a6C3PUAjsD34Kag9dD+gPZoBmT0lFZM9KxuPPV+pjT2lbpI9XNqePc5srz0gDMA9zMzMPQoV0z14jdI9G1TNPQRExz1LhMQ9" },
        { "scaling-offset-negative", "ScalingPivot=12,-18,-6;ScalingOffset=-4,8,-10",
          "CtcjPRNJKT3tiTg9/fxPPVvgbT2QGIg9WFqaPfKQrD1XrL09c8nMPRU+2T0UlOI9F3ToPZWD6j1pj+M9HUjPPc4urj3lg4I9CtcjPSkvkjxTE1863z8bvKWQZ7wQ93q8CtejPb2gpT17XKo9YOOwPa76tz3mib49UsPDPbA6xz1T58g96RPJPelByD14Ccc9pvzFPY2QxT2H4MY9JPzIPQt0xz2Zlbw9CtejPfPSeT07ixo9/hd2PIIW7rrzCwe8zMzMvbkryr0JvsK9tR23vQbfp72gqJW9C0OBvbA+V73nqiu9GDgCvWrzuryyIYC8V10yvLtAFrzJd3O8xyD2vCVCU73GrJu9zMzMvWV0972aLAy+IlMXvtehHb7Jsh++" },
        { "rotation-large", "RotationPivot=1250,-750,500",
          "AAAAgLTawT1nhrM+vQ04P8LAkj8Ngco/XK/9PxcgFECwCCRA63suQNw8NEC8oDZAGS83QNIwN0AzxjZA1tovQILNFUDTL7k/AAAAgONN8L/uV3nAgUu2wA+O3sBBdu3AAAAAAIEjxL2glLy+/LRMv0mFr7+EwgPAORA1wBQJacCAWI7AB3mmwOVru8A32cvAfIvWwB5e2sCbnc3AM5aqwDDLbcCFauq/AAAAAA2FvD82YhpARbY1QKv4O0CVtDtAAAAAAHTlxD1l0Lo+5gJHP9cBpz+jWvU/Xj4lQFIUUUBdAHxAjOiRQAgvo0BFx7BAday5QBfdvEDHPrJAgEyVQPANVUBKu9s/AAAAAOI00795b0XAEzaGwKvtnMA3/6TA" },
        { "scaling-large", "ScalingPivot=-900,1100,-1300",
          "AAAAgHVkg7yKrIu9pwItvg6iqr5CcBK/pRNjv3JQor/Prdi/6zsIwL5YIsB0CDjAJrlGwBsVTMDXbTrAhTkNwC4Vp7+XEPq+AAAAgPi7Vz105IG+3Wo2v47IjL9yY6C/AAAAABmZFr002A6+BIeXvs9U+76igDS/tLFqv+Rrjb90ZaC/nEOtv+1JtL/r3ra//BG3vxXYtr+S/La/gPWuv9Bhj7+3BCO/AAAAAAuIGT/h+no/aTWLPxEmgz83dXg/AAAAALUjhz1Fdn0+DY4EP04sWT9m9Jo/4PDJP8GL9j+JNA9A4/YfQFIfLUCdjDZAL0Q8QHg6PkBJhDdAQKYiQM9l+j8JHow/AAAAAMu2m7+gmxnABB5ZwP/6gcAtyonA" },
    };

    [Theory]
    [MemberData(nameof(PivotRawPositionUnitySamples))]
    public void PivotRawPositionUsesUnity2022MatrixConverterBits(
        string caseName, string propertyOverrides, string expectedBase64)
    {
        var imported = ReimportTransformStack(caseName, propertyOverrides, false);
        var curves = new[]
        {
            Curve(imported.Clip, "m_LocalPosition.x"),
            Curve(imported.Clip, "m_LocalPosition.y"),
            Curve(imported.Clip, "m_LocalPosition.z"),
        };
        foreach (var curve in curves)
            Assert.Equal(Enumerable.Range(0, 24), Frames(curve, imported.Clip.frameRate));
        AssertUnityBits(expectedBase64,
            curves.SelectMany(curve => curve.keys.Select(key => key.value)).ToArray(),
            caseName + ":position");
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
        var properties = clip.bindings
            .Where(binding => binding.type == typeof(Transform))
            .Select(binding => binding.propertyName)
            .ToArray();
        Assert.Equal(9, properties.Length);
        Assert.Contains("localEulerAnglesRaw.x", properties);
        Assert.Contains("localEulerAnglesRaw.y", properties);
        Assert.Contains("localEulerAnglesRaw.z", properties);
        Assert.DoesNotContain(properties, property => property.StartsWith("m_LocalRotation.", StringComparison.Ordinal));
    }

    public static TheoryData<string, float, float, float, float, bool, bool> VisibilityUnitySamples => new()
    {
        { "visible", 1f, 1f, 1f, 1f, false, true },
        { "hidden", 0f, 0f, 0f, 0f, false, false },
        { "visible-hidden-visible", 1f, 1f, 0f, 1f, false, true },
        { "hidden-visible-hidden", 0f, 0f, 1f, 0f, false, false },
        { "negative-zero-positive", -1f, -1f, 0f, 1f, false, true },
        { "positive-fractions", 0.01f, 0.01f, 0.5f, 2f, false, true },
        { "mixed-signs", -0.25f, -0.25f, 0.25f, -2f, false, true },
        { "smooth-source-is-stepped", 1f, 1f, 0f, 1f, true, true },
        { "default-hidden-animated-visible", 0f, 1f, 1f, 1f, false, false },
        { "import-visibility-off", 0f, 0f, 1f, 0f, false, false },
    };

    [Theory]
    [MemberData(nameof(VisibilityUnitySamples))]
    public void VisibilityImportMatchesUnity2022BindingValuesAndRuntime(
        string caseName, float defaultValue, float first, float middle, float last,
        bool smoothSource, bool expectedInitialEnabled)
    {
        var importVisibility = caseName != "import-visibility-off";
        var imported = ReimportVisibility(defaultValue, first, middle, last, importer =>
        {
            importer.importVisibility = importVisibility;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        }, smoothSource);
        var renderer = Assert.IsAssignableFrom<Renderer>(imported.Root.GetComponent<Renderer>());
        Assert.Equal(importVisibility ? expectedInitialEnabled : true, renderer.enabled);

        var bindings = AnimationUtility.GetCurveBindings(imported.Clip)
            .Where(binding => binding.type == typeof(Renderer) &&
                binding.propertyName == "m_Enabled")
            .ToArray();
        if (!importVisibility)
        {
            Assert.Empty(bindings);
            imported.Clip.SampleAnimation(imported.Root, 13f / 24f);
            Assert.True(renderer.enabled);
            return;
        }

        var binding = Assert.Single(bindings);
        Assert.Equal(string.Empty, binding.path);
        Assert.False(binding.isDiscreteCurve);
        Assert.Equal(typeof(bool), AnimationUtility.GetEditorCurveValueType(imported.Root, binding));
        Assert.True(AnimationUtility.GetFloatValue(imported.Root, binding, out var currentValue));
        Assert.Equal(renderer.enabled ? 1f : 0f, currentValue);
        var curve = Assert.IsType<AnimationCurve>(AnimationUtility.GetEditorCurve(imported.Clip, binding));
        Assert.Equal(24, curve.length);
        Assert.Equal(Enumerable.Range(0, 24), Frames(curve, imported.Clip.frameRate));
        Assert.Equal(Enumerable.Range(0, 24).Select(frame =>
            frame <= 12 ? first : frame <= 22 ? middle : last),
            curve.keys.Select(key => key.value));
        var keys = curve.keys;
        for (var index = 0; index < keys.Length; ++index)
        {
            static float Slope(Keyframe left, Keyframe right) =>
                (right.value - left.value) / (right.time - left.time);
            var expectedIn = index > 0 ? Slope(keys[index - 1], keys[index])
                : Slope(keys[0], keys[1]);
            var expectedOut = index + 1 < keys.Length ? Slope(keys[index], keys[index + 1])
                : expectedIn;
            Assert.InRange(MathF.Abs(keys[index].inTangent - expectedIn), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(keys[index].outTangent - expectedOut), 0f, 0.0001f);
        }

        imported.Clip.SampleAnimation(imported.Root, 0f);
        Assert.Equal(first != 0f, renderer.enabled);
        imported.Clip.SampleAnimation(imported.Root, 13f / 24f);
        Assert.Equal(middle != 0f, renderer.enabled);
        imported.Clip.SampleAnimation(imported.Root, 23f / 24f);
        Assert.Equal(last != 0f, renderer.enabled);
    }

    [Fact]
    public void NonResampledVisibilityMatchesUnity2022SteppedFiveKeyLayout()
    {
        var imported = ReimportVisibility(1f, 1f, 0f, 1f, importer =>
        {
            importer.resampleCurves = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        });
        var curve = Curve(imported.Clip, "m_Enabled");
        var keys = curve.keys;
        Assert.Equal(5, keys.Length);
        var expectedTimes = new[]
        {
            0f, 13f / 24f - 0.00001f, 13f / 24f,
            23f / 24f - 0.00001f, 23f / 24f,
        };
        for (var index = 0; index < keys.Length; ++index)
            Assert.InRange(MathF.Abs(keys[index].time - expectedTimes[index]), 0f, 0.000002f);
        Assert.Equal(new[] { 1f, 1f, 0f, 0f, 1f }, keys.Select(key => key.value));
        Assert.True(float.IsNegativeInfinity(keys[1].outTangent));
        Assert.True(float.IsNegativeInfinity(keys[2].inTangent));
        Assert.True(float.IsNegativeInfinity(keys[3].outTangent));
        Assert.True(float.IsNegativeInfinity(keys[4].inTangent));
        Assert.True(float.IsNegativeInfinity(keys[4].outTangent));

        var renderer = Assert.IsAssignableFrom<Renderer>(imported.Root.GetComponent<Renderer>());
        imported.Clip.SampleAnimation(imported.Root, 13f / 24f - 0.000005f);
        Assert.True(renderer.enabled);
        imported.Clip.SampleAnimation(imported.Root, 13f / 24f);
        Assert.False(renderer.enabled);
    }

    [Theory]
    [InlineData(ModelImporterAnimationCompression.KeyframeReduction)]
    [InlineData(ModelImporterAnimationCompression.KeyframeReductionAndCompression)]
    [InlineData(ModelImporterAnimationCompression.Optimal)]
    public void ResampledVisibilityCompressionMatchesUnity2022FiveRetainedFrames(
        ModelImporterAnimationCompression compression)
    {
        var imported = ReimportVisibility(1f, 1f, 0f, 1f, importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = compression;
        });
        var curve = Curve(imported.Clip, "m_Enabled");
        Assert.Equal(new[] { 0, 12, 13, 22, 23 }, Frames(curve, imported.Clip.frameRate));
        Assert.Equal(new[] { 1f, 1f, 0f, 0f, 1f }, curve.keys.Select(key => key.value));
    }

    [Fact]
    public void AnimatorAppliesImportedRendererVisibilityCurve()
    {
        var imported = ReimportVisibility(1f, 1f, 0f, 1f, importer =>
        {
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        });
        var controller = new AnimatorController();
        var state = controller.layers[0].stateMachine.AddState("Visibility");
        state.motion = imported.Clip;
        controller.layers[0].stateMachine.defaultState = state;
        var renderer = Assert.IsAssignableFrom<Renderer>(imported.Root.GetComponent<Renderer>());
        var animator = imported.Root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.Play("Visibility");
        animator.Update(13f / 24f);
        Assert.False(renderer.enabled);
        animator.Update(10f / 24f);
        Assert.True(renderer.enabled);
    }

    [Fact]
    public void ParentVisibilityPropagatesToNestedMeshRenderer()
    {
        var imported = ReimportVisibilityTopology(false, new[] { 1f, 0f, 1f }, null, true);
        var rootCurve = VisibilityCurve(imported.Clip, string.Empty);
        var childCurve = VisibilityCurve(imported.Clip, "Child");
        Assert.Equal(Enumerable.Range(0, 24), Frames(rootCurve, imported.Clip.frameRate));
        Assert.Equal(rootCurve.keys.Select(key => key.value), childCurve.keys.Select(key => key.value));
    }

    [Fact]
    public void NullParentVisibilityPropagatesToDescendantRenderer()
    {
        var imported = ReimportVisibilityTopology(true, new[] { 1f, 0f, 1f }, null, true);
        var binding = Assert.Single(imported.Clip.bindings.Where(candidate =>
            candidate.propertyName == "m_Enabled"));
        Assert.Equal("Child", binding.path);
        Assert.Equal(Enumerable.Range(0, 24).Select(frame => frame <= 12 ? 1f : frame <= 22 ? 0f : 1f),
            binding.curve.keys.Select(key => key.value));
    }

    [Fact]
    public void ChildVisibilityDoesNotPropagateToParentRenderer()
    {
        var imported = ReimportVisibilityTopology(false, null, new[] { 1f, 0f, 1f }, true);
        var binding = Assert.Single(imported.Clip.bindings.Where(candidate =>
            candidate.propertyName == "m_Enabled"));
        Assert.Equal("Child", binding.path);
    }

    [Fact]
    public void AncestorAndChildVisibilityMultiplySourceNumbers()
    {
        var imported = ReimportVisibilityTopology(false,
            new[] { -1f, 0f, 2f }, new[] { 0.25f, -2f, 0f }, true);
        var parent = VisibilityCurve(imported.Clip, string.Empty);
        var child = VisibilityCurve(imported.Clip, "Child");
        Assert.Equal(Enumerable.Range(0, 24).Select(frame => frame <= 12 ? -1f : frame <= 22 ? 0f : 2f),
            parent.keys.Select(key => key.value));
        Assert.Equal(Enumerable.Range(0, 24).Select(frame => frame <= 12 ? -0.25f : 0f),
            child.keys.Select(key => key.value));
    }

    [Fact]
    public void StaggeredVisibilityCurvesComposeAtEveryResampledFrame()
    {
        var imported = ReimportVisibilityTopology(false,
            new[] { 1f, 0f, 1f }, new[] { 1f, 0f, 1f }, true,
            parentFrames: new[] { 0, 13, 23 }, childFrames: new[] { 0, 7, 19 });
        var child = VisibilityCurve(imported.Clip, "Child");
        Assert.Equal(Enumerable.Range(0, 24).Select(frame => frame <= 6 || frame == 23 ? 1f : 0f),
            child.keys.Select(key => key.value));
    }

    [Fact]
    public void NonResampledParentVisibilityPropagatesUnityFiveKeyStepLayout()
    {
        var imported = ReimportVisibilityTopology(false, new[] { 1f, 0f, 1f }, null, false);
        foreach (var path in new[] { string.Empty, "Child" })
        {
            var keys = VisibilityCurve(imported.Clip, path).keys;
            Assert.Equal(5, keys.Length);
            Assert.Equal(new[] { 1f, 1f, 0f, 0f, 1f }, keys.Select(key => key.value));
            Assert.True(float.IsNegativeInfinity(keys[1].outTangent));
            Assert.True(float.IsNegativeInfinity(keys[2].inTangent));
            Assert.True(float.IsNegativeInfinity(keys[3].outTangent));
            Assert.True(float.IsNegativeInfinity(keys[4].inTangent));
        }
    }

    [Fact]
    public void NonResampledStaggeredVisibilityKeepsSourceTimeUnion()
    {
        var imported = ReimportVisibilityTopology(false,
            new[] { 1f, 0f, 1f }, new[] { 1f, 0f, 1f }, false,
            parentFrames: new[] { 0, 13, 23 }, childFrames: new[] { 0, 7, 19 });
        var keys = VisibilityCurve(imported.Clip, "Child").keys;
        Assert.Equal(7, keys.Length);
        var expectedTimes = new[]
        {
            0f, 7f / 24f - 0.00001f, 7f / 24f,
            13f / 24f, 19f / 24f, 23f / 24f - 0.00001f, 23f / 24f,
        };
        for (var index = 0; index < keys.Length; ++index)
            Assert.InRange(MathF.Abs(keys[index].time - expectedTimes[index]), 0f, 0.000002f);
        Assert.Equal(new[] { 1f, 1f, 0f, 0f, 0f, 0f, 1f },
            keys.Select(key => key.value));
    }

    [Fact]
    public void StaticHiddenParentDisablesEveryDescendantRenderer()
    {
        var imported = ReimportVisibilityTopology(false, null, null, true, parentDefault: 0f);
        Assert.All(imported.Root.GetComponentsInChildren<Renderer>(true), renderer =>
            Assert.False(renderer.enabled));
    }

    [Fact]
    public void StaticHiddenChildDoesNotDisableParentRenderer()
    {
        var imported = ReimportVisibilityTopology(false, null, null, true, childDefault: 0f);
        Assert.True(imported.Root.GetComponent<Renderer>()!.enabled);
        Assert.False(imported.Root.GetComponentsInChildren<Renderer>(true)
            .Single(renderer => renderer.gameObject.name == "Child").enabled);
    }

    [Fact]
    public void AnimatedParentVisibilityOverridesStaticHiddenChildWhenSampled()
    {
        var imported = ReimportVisibilityTopology(false, new[] { 1f, 0f, 1f }, null, true,
            childDefault: 0f);
        var child = imported.Root.GetComponentsInChildren<Renderer>(true)
            .Single(renderer => renderer.gameObject.name == "Child");
        Assert.False(child.enabled);
        imported.Clip.SampleAnimation(imported.Root, 0f);
        Assert.True(child.enabled);
        imported.Clip.SampleAnimation(imported.Root, 13f / 24f);
        Assert.False(child.enabled);
    }

    [Fact]
    public void StaticHiddenParentDoesNotMultiplyChildAnimationCurve()
    {
        var imported = ReimportVisibilityTopology(false, null, new[] { 1f, 0f, 1f }, true,
            parentDefault: 0f);
        var child = VisibilityCurve(imported.Clip, "Child");
        Assert.Equal(1f, child.keys[0].value);
        Assert.Equal(0f, child.keys[13].value);
        Assert.Equal(1f, child.keys[23].value);
    }

    [Fact]
    public void NestedImportVisibilityOffRestoresRenderersAndDropsBindings()
    {
        var imported = ReimportVisibilityTopology(false, new[] { 0f, 1f, 0f }, null, true,
            parentDefault: 0f, importVisibility: false);
        Assert.All(imported.Root.GetComponentsInChildren<Renderer>(true), renderer =>
            Assert.True(renderer.enabled));
        Assert.DoesNotContain(imported.Clip.bindings, binding => binding.propertyName == "m_Enabled");
    }

    [Theory]
    [InlineData(0, 2f)]
    [InlineData(1, 2.00372767f)]
    [InlineData(2, 2.0151732f)]
    [InlineData(3, 2.037238f)]
    [InlineData(4, 2.07476544f)]
    [InlineData(5, 2.13643384f)]
    [InlineData(6, 2.23788333f)]
    [InlineData(7, 2.39445376f)]
    [InlineData(8, 2.56706977f)]
    [InlineData(9, 2.69721365f)]
    [InlineData(10, 2.78680372f)]
    [InlineData(11, 2.85005951f)]
    [InlineData(12, 2.89599752f)]
    [InlineData(13, 2.92984843f)]
    [InlineData(14, 2.95480156f)]
    [InlineData(15, 2.97292328f)]
    [InlineData(16, 2.98562264f)]
    [InlineData(17, 2.99390435f)]
    [InlineData(18, 2.998509f)]
    [InlineData(19, 3f)]
    public void LayeredAdditiveVisibilityMatchesUnity2022WeightedFrameBits(
        int frame, float expected)
    {
        var clip = ReimportLayeredVisibility("LayeredVisibilityCube.fbx", true).Clip;
        var keys = VisibilityCurve(clip, string.Empty).keys;
        Assert.Equal(120, keys.Length);
        Assert.Equal(
            BitConverter.SingleToInt32Bits(expected),
            BitConverter.SingleToInt32Bits(keys[frame].value));
    }

    [Theory]
    [InlineData("RightOnly", 1073756635, 1075065845, 1077899885)]
    [InlineData("LeftOnly", 1073783623, 1076993201, 1077929935)]
    [InlineData("RightPacked1", 1074062351, 1076318104, 1077905117)]
    [InlineData("LeftPacked1", 1073774494, 1075123595, 1077616476)]
    [InlineData("BothPacked1", 1073962470, 1075728590, 1077715484)]
    [InlineData("RightPacked3333", 1073777501, 1075678462, 1077902276)]
    [InlineData("BothPacked9999", 1073748232, 1074729715, 1077931683)]
    [InlineData("RightPacked10000", 1073748107, 1074240033, 1077893785)]
    [InlineData("LeftPacked10000", 1073786450, 1077296687, 1077931761)]
    [InlineData("BothPacked65535", 1073962684, 1075728609, 1077715266)]
    [InlineData("BrokenBoth", 1073757459, 1076666150, 1077929874)]
    [InlineData("AutoProgressive", 1073775457, 1075673564, 1077902495)]
    [InlineData("AutoClamp", 1073962577, 1075728600, 1077715375)]
    [InlineData("TcbAsymmetric", 1073893186, 1075751370, 1077803118)]
    public void WeightedLayerCurveModesMatchUnity2022FrameBits(
        string variant, int expected1, int expected9, int expected18)
    {
        var clip = ReimportLayeredVisibility("LayeredVisibilityCube.fbx", true,
            source => RewriteFbxLayerWeightCurve(source, variant)).Clip;
        var keys = VisibilityCurve(clip, string.Empty).keys;
        Assert.Equal(120, keys.Length);
        foreach (var (frame, expected) in new[]
        {
            (1, expected1), (9, expected9), (18, expected18),
        })
            Assert.Equal(expected, BitConverter.SingleToInt32Bits(keys[frame].value));
    }

    [Theory]
    [InlineData("PostSlope", 20, 1077936500, 29, 1077939843, 119, 1077973273)]
    [InlineData("PostRepeat", 20, 1073757459, 29, 1077041918, 119, 1074314069)]
    [InlineData("PostMirror", 20, 1077929874, 29, 1076666150, 119, 1074314069)]
    [InlineData("PostRepeatRelative", 20, 1077951763, 38, 1082130432, 119, 1090662101)]
    [InlineData("PreSlopeDelayed", 0, 1073700445, 5, 1073723433, 9, 1073741824)]
    [InlineData("PreRepeatDelayed", 0, 1073796923, 5, 1077391280, 9, 1073741824)]
    [InlineData("PreMirrorDelayed", 0, 1077913407, 5, 1075841433, 9, 1073741824)]
    [InlineData("PreRepeatRelativeDelayed", 0, 1065463414, 5, 1072652129, 9, 1073741824)]
    public void LayerWeightExtrapolationMatchesUnity2022FrameBits(
        string variant, int frameA, int expectedA, int frameB, int expectedB,
        int frameC, int expectedC)
    {
        var clip = ReimportLayeredVisibility("LayeredVisibilityCube.fbx", true,
            source => RewriteFbxLayerWeightCurve(source, variant)).Clip;
        var keys = VisibilityCurve(clip, string.Empty).keys;
        Assert.Equal(120, keys.Length);
        foreach (var (frame, expected) in new[]
        {
            (frameA, expectedA), (frameB, expectedB), (frameC, expectedC),
        })
            Assert.Equal(expected, BitConverter.SingleToInt32Bits(keys[frame].value));
    }

    [Theory]
    [InlineData("Baseline", 0, 0, 0, 0, 0, 0, 2f, 2.92984843f, 3f)]
    [InlineData("MuteBase", 1, 0, 0, 0, 0, 0, 1f, 1.92984831f, 2f)]
    [InlineData("MuteX", 0, 0, 1, 0, 0, 0, 1f, 1.92984831f, 2f)]
    [InlineData("MuteY", 0, 0, 0, 0, 1, 0, 2f, 2f, 2f)]
    [InlineData("MuteAll", 1, 0, 1, 0, 1, 0, 1f, 1f, 1f)]
    [InlineData("SoloBase", 0, 1, 0, 0, 0, 0, 2f, 2.92984843f, 3f)]
    [InlineData("SoloX", 0, 0, 0, 1, 0, 0, 2f, 2.92984843f, 3f)]
    [InlineData("SoloY", 0, 0, 0, 0, 0, 1, 2f, 2.92984843f, 3f)]
    [InlineData("SoloXY", 0, 0, 0, 1, 0, 1, 2f, 2.92984843f, 3f)]
    [InlineData("MuteSoloX", 0, 0, 1, 1, 0, 0, 1f, 1.92984831f, 2f)]
    [InlineData("SoloXMutedY", 0, 0, 0, 1, 1, 0, 2f, 2f, 2f)]
    [InlineData("SoloBaseY", 0, 1, 0, 0, 0, 1, 2f, 2.92984843f, 3f)]
    public void LayerMuteAndSoloMatchUnity2022ImportedVisibilityBits(
        string _, int baseMute, int baseSolo, int xMute, int xSolo,
        int yMute, int ySolo, float expected0, float expected13, float expected19)
    {
        var clip = ReimportLayeredVisibility("LayeredVisibilityCube.fbx", true, source =>
        {
            source = SetFbxLayerMuteSolo(source, "BaseLayer", baseMute, baseSolo);
            source = SetFbxLayerMuteSolo(source, "X", xMute, xSolo);
            return SetFbxLayerMuteSolo(source, "Y", yMute, ySolo);
        }).Clip;
        var keys = VisibilityCurve(clip, string.Empty).keys;
        Assert.Equal(120, keys.Length);
        foreach (var (frame, expected) in new[]
        {
            (0, expected0), (13, expected13), (19, expected19),
        })
            Assert.Equal(BitConverter.SingleToInt32Bits(expected),
                BitConverter.SingleToInt32Bits(keys[frame].value));
    }

    [Fact]
    public void LayeredVisibilityWeightUsesSmoothFbxWeightCurve()
    {
        var clip = ReimportLayeredVisibility("LayeredVisibilityCube.fbx", true).Clip;
        var keys = VisibilityCurve(clip, string.Empty).keys;
        Assert.True(keys[1].value > keys[0].value);
        Assert.True(keys[8].value > keys[7].value);
        Assert.InRange(MathF.Abs(keys[8].inTangent - 4.142785f), 0f, 0.001f);
        Assert.InRange(MathF.Abs(keys[19].outTangent), 0f, 0.0001f);
    }

    [Fact]
    public void NonResampledLayeredVisibilityStillBakesUnityFrameGridAsSteps()
    {
        var clip = ReimportLayeredVisibility("LayeredVisibilityCube.fbx", false).Clip;
        var keys = VisibilityCurve(clip, string.Empty).keys;
        Assert.Equal(139, keys.Length);
        Assert.Equal(0f, keys[0].time);
        Assert.Equal(2f, keys[0].value);
        Assert.InRange(MathF.Abs(keys[1].time - (1f / 24f - 0.00001f)), 0f, 0.000002f);
        Assert.Equal(2f, keys[1].value);
        Assert.InRange(MathF.Abs(keys[2].time - 1f / 24f), 0f, 0.000002f);
        Assert.InRange(MathF.Abs(keys[2].value - 2.00372767f), 0f, 0.000001f);
        Assert.True(float.IsNegativeInfinity(keys[1].outTangent));
        Assert.True(float.IsNegativeInfinity(keys[2].inTangent));
        Assert.InRange(MathF.Abs(keys[^1].time - 119f / 24f), 0f, 0.000002f);
        Assert.Equal(3f, keys[^1].value);
    }

    [Fact]
    public void LayeredOverridePassthroughVisibilityMatchesUnity2022Blend()
    {
        var imported = ReimportLayeredVisibility("LayeredVisibilityCube.fbx", true, source =>
            source.Replace(
                "P: \"Weight\", \"Number\", \"\", \"A+\",0\n\t\t\tP: \"Color\"",
                "P: \"Weight\", \"Number\", \"\", \"A+\",0\n" +
                "\t\t\tP: \"BlendMode\", \"enum\", \"\", \"\",2\n\t\t\tP: \"Color\"",
                StringComparison.Ordinal));
        var keys = VisibilityCurve(imported.Clip, string.Empty).keys;
        Assert.Equal(120, keys.Length);
        foreach (var (frame, expected) in new[]
        {
            (0, 2f), (1, 1.99627233f), (7, 1.60554624f),
            (8, 1.43293023f), (13, 1.07015169f), (19, 1f), (119, 1f),
        })
            Assert.InRange(MathF.Abs(keys[frame].value - expected), 0f, 0.00005f);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConstantMultiLayerVisibilityKeepsUnityFrameGrid(bool resampleCurves)
    {
        var clip = ReimportLayeredVisibility(
            "LayeredConstantVisibilityCube.fbx", resampleCurves).Clip;
        var keys = VisibilityCurve(clip, string.Empty).keys;
        Assert.Equal(120, keys.Length);
        Assert.All(keys, key =>
            Assert.InRange(MathF.Abs(key.value - 1.31851852f), 0f, 0.000001f));
        Assert.Equal(Enumerable.Range(0, 120), Frames(new AnimationCurve(keys), clip.frameRate));
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

    private (GameObject Root, AnimationClip Clip) ReimportVisibility(
        float defaultValue, float first, float middle, float last,
        Action<ModelImporter> configure, bool smoothSource = false)
    {
        var path = CopyAnimatedFixture();
        var fullPath = FullPath(path);
        var fixture = File.ReadAllText(fullPath);
        static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        fixture = fixture.Replace(
            "Property: \"Visibility\", \"Visibility\", \"A+\",1",
            "Property: \"Visibility\", \"Visibility\", \"A+\"," + Number(defaultValue),
            StringComparison.Ordinal);
        const string originalChannel =
            "\t\t\tChannel: \"Visibility\" {\n" +
            "\t\t\t\tDefault: 1\n" +
            "\t\t\t\tKeyVer: 4005\n" +
            "\t\t\t\tKeyCount: 3\n" +
            "\t\t\t\tKey: 1924423250,1,C,s,26941925500,1,C,s,46186158000,1,C,s\n" +
            "\t\t\t\tColor: 1,1,1\n" +
            "\t\t\t}";
        Assert.True(fixture.Contains(originalChannel, StringComparison.Ordinal));
        var keyData = smoothSource
            ? $"1924423250,{Number(first)},U,s,0,0,n,26941925500,{Number(middle)},U,s,0,0,n,46186158000,{Number(last)},U,s,0,0,n"
            : $"1924423250,{Number(first)},C,s,26941925500,{Number(middle)},C,s,46186158000,{Number(last)},C,s";
        var replacementChannel =
            "\t\t\tChannel: \"Visibility\" {\n" +
            $"\t\t\t\tDefault: {Number(defaultValue)}\n" +
            "\t\t\t\tKeyVer: 4005\n" +
            "\t\t\t\tKeyCount: 3\n" +
            $"\t\t\t\tKey: {keyData}\n" +
            "\t\t\t\tColor: 1,1,1\n" +
            "\t\t\t}";
        var rewritten = fixture.Replace(originalChannel, replacementChannel, StringComparison.Ordinal);
        File.WriteAllText(fullPath, rewritten);
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        configure(importer);
        importer.SaveAndReimport();
        return (
            AssetDatabase.LoadAssetAtPath<GameObject>(path)!,
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single());
    }

    private (GameObject Root, AnimationClip Clip) ReimportVisibilityTopology(
        bool nullParent, float[]? parentValues, float[]? childValues, bool resampleCurves,
        float parentDefault = 1f, float childDefault = 1f,
        int[]? parentFrames = null, int[]? childFrames = null,
        bool importVisibility = true)
    {
        var path = CopyAnimatedFixture();
        var fullPath = FullPath(path);
        var fixture = File.ReadAllText(fullPath);
        var model = FbxBraceBlock(fixture, "\tModel: \"Model::pCube1\", \"Mesh\" {");
        var takes = fixture.IndexOf("Takes:", StringComparison.Ordinal);
        var take = FbxBraceBlock(fixture, "\t\tModel: \"Model::pCube1\" {", takes);
        var parent = RenameFbxModel(model, "Parent", nullParent ? "Null" : "Mesh");
        if (nullParent)
        {
            var properties = FbxBraceBlock(parent, "\t\tProperties60:  {");
            parent = "\tModel: \"Model::Parent\", \"Null\" {\n\t\tVersion: 232\n" +
                properties + "\n\t\tMultiLayer: 0\n\t\tMultiTake: 0\n\t\tShading: T\n" +
                "\t\tCulling: \"CullingOff\"\n\t}";
        }
        var child = RenameFbxModel(model, "Child", "Mesh");
        parent = SetFbxDefaultVisibility(parent, parentDefault);
        child = SetFbxDefaultVisibility(child, childDefault);
        fixture = fixture.Replace(model, parent + "\n" + child, StringComparison.Ordinal)
            .Replace("\tCount: 4", "\tCount: 5", StringComparison.Ordinal)
            .Replace("\t\tCount: 1\n\t}\n\tObjectType: \"Material\"",
                "\t\tCount: 2\n\t}\n\tObjectType: \"Material\"", StringComparison.Ordinal)
            .Replace("\tConnect: \"OO\", \"Model::pCube1\", \"Model::Scene\"\n" +
                "\tConnect: \"OO\", \"Material::lambert1\", \"Model::pCube1\"",
                "\tConnect: \"OO\", \"Model::Parent\", \"Model::Scene\"\n" +
                "\tConnect: \"OO\", \"Model::Child\", \"Model::Parent\"\n" +
                "\tConnect: \"OO\", \"Material::lambert1\", \"Model::Child\"" +
                (nullParent ? string.Empty :
                    "\n\tConnect: \"OO\", \"Material::lambert1\", \"Model::Parent\""),
                StringComparison.Ordinal);
        var parentTake = RewriteFbxVisibilityTake(take, "Parent", parentValues, parentFrames);
        var childTake = RewriteFbxVisibilityTake(take, "Child", childValues, childFrames);
        fixture = fixture.Replace(take, parentTake + "\n" + childTake, StringComparison.Ordinal);
        File.WriteAllText(fullPath, fixture);
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        importer.importVisibility = importVisibility;
        importer.resampleCurves = resampleCurves;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        return (
            AssetDatabase.LoadAssetAtPath<GameObject>(path)!,
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single());
    }

    private (GameObject Root, AnimationClip Clip) ReimportLayeredVisibility(
        string fixtureName, bool resampleCurves, Func<string, string>? rewrite = null)
    {
        var path = "Assets/Models/Layered-" + Guid.NewGuid().ToString("N") + ".fbx";
        var fullPath = FullPath(path);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Models", fixtureName), fullPath);
        if (rewrite is not null)
        {
            var source = File.ReadAllText(fullPath);
            var rewritten = rewrite(source);
            Assert.NotEqual(source, rewritten);
            File.WriteAllText(fullPath, rewritten);
        }
        AssetDatabase.ImportAsset(path);
        var importer = ModelImporter.GetAtPath(path);
        importer.importVisibility = true;
        importer.resampleCurves = resampleCurves;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        return (
            AssetDatabase.LoadAssetAtPath<GameObject>(path)!,
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single());
    }

    private static string RenameFbxModel(string block, string name, string type) =>
        block.Replace("Model::pCube1\", \"Mesh", "Model::" + name + "\", \"" + type,
            StringComparison.Ordinal);

    private static string SetFbxDefaultVisibility(string block, float value) =>
        block.Replace("Property: \"Visibility\", \"Visibility\", \"A+\",1",
            "Property: \"Visibility\", \"Visibility\", \"A+\"," +
            value.ToString("R", CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static string RewriteFbxVisibilityTake(
        string block, string name, float[]? values, int[]? frames)
    {
        block = block.Replace("Model::pCube1", "Model::" + name, StringComparison.Ordinal);
        const string marker = "\t\t\tChannel: \"Visibility\" {";
        var channel = FbxBraceBlock(block, marker);
        if (values is null) return block.Replace(channel, string.Empty, StringComparison.Ordinal);
        frames ??= new[] { 0, 13, 23 };
        Assert.Equal(values.Length, frames.Length);
        const long frameTicks = 1_924_423_250L;
        var keys = string.Join(",", values.Select((value, index) =>
            ((frames[index] + 1L) * frameTicks).ToString(CultureInfo.InvariantCulture) + "," +
            value.ToString("R", CultureInfo.InvariantCulture) + ",C,s"));
        var replacement = marker + "\n\t\t\t\tDefault: " +
            values[0].ToString("R", CultureInfo.InvariantCulture) +
            "\n\t\t\t\tKeyVer: 4005\n\t\t\t\tKeyCount: " + values.Length +
            "\n\t\t\t\tKey: " + keys + "\n\t\t\t\tColor: 1,1,1\n\t\t\t}";
        return block.Replace(channel, replacement, StringComparison.Ordinal);
    }

    private static string FbxBraceBlock(string source, string marker, int searchStart = 0)
    {
        var start = source.IndexOf(marker, searchStart, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing FBX marker: " + marker);
        var brace = source.IndexOf('{', start);
        var depth = 0;
        for (var index = brace; index < source.Length; ++index)
        {
            if (source[index] == '{') ++depth;
            else if (source[index] == '}' && --depth == 0)
                return source.Substring(start, index - start + 1);
        }
        throw new InvalidDataException("Unterminated FBX block: " + marker);
    }

    private static string SetFbxLayerMuteSolo(
        string source, string layerName, int mute, int solo)
    {
        const string layerMarker = "\tAnimationLayer:";
        var nameIndex = source.IndexOf(
            "\"AnimLayer::" + layerName + "\"", StringComparison.Ordinal);
        Assert.True(nameIndex >= 0, "Missing FBX animation layer: " + layerName);
        var start = source.LastIndexOf(
            layerMarker, nameIndex, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing FBX animation layer block: " + layerName);
        var block = FbxBraceBlock(source, layerMarker, start);
        const string properties = "\t\tProperties70:  {";
        string rewritten;
        if (block.Contains(properties, StringComparison.Ordinal))
        {
            rewritten = block.Replace(properties, properties +
                "\n\t\t\tP: \"Mute\", \"bool\", \"\", \"\"," + mute +
                "\n\t\t\tP: \"Solo\", \"bool\", \"\", \"\"," + solo,
                StringComparison.Ordinal);
        }
        else
        {
            rewritten = block.Replace(" {\n\t}", " {\n" + properties +
                "\n\t\t\tP: \"Mute\", \"bool\", \"\", \"\"," + mute +
                "\n\t\t\tP: \"Solo\", \"bool\", \"\", \"\"," + solo +
                "\n\t\t}\n\t}", StringComparison.Ordinal);
        }
        Assert.NotEqual(block, rewritten);
        return source.Substring(0, start) + rewritten +
            source.Substring(start + block.Length);
    }

    private static string RewriteFbxLayerWeightCurve(string source, string variant)
    {
        const string marker = "\tAnimationCurve: 2425217120608";
        var block = FbxBraceBlock(source, marker);
        const int user = 0x408;
        const int broken = 0xc08;
        const int weightedRight = 0x1000000;
        const int weightedNextLeft = 0x2000000;
        var defaultPacked = unchecked((int)0x0d050d05u);
        var slopeOut = BitConverter.SingleToInt32Bits(1.3154056072235107f);
        var slopeIn = BitConverter.SingleToInt32Bits(0.21254299581050873f);
        var slopeEnd = BitConverter.SingleToInt32Bits(0.21251147985458374f);

        int[] Weighted(uint packed) => new[]
        {
            slopeOut, slopeIn, unchecked((int)packed), 0,
            slopeEnd, 0, defaultPacked, 0,
        };

        var config = variant switch
        {
            "RightOnly" => (user | weightedRight, user, Weighted(0x0d0515fbu), (char?)null, (char?)null, false),
            "LeftOnly" => (user | weightedNextLeft, user, Weighted(0x20630d05u), (char?)null, (char?)null, false),
            "RightPacked1" => (user | weightedRight, user, Weighted(0x0d050001u), (char?)null, (char?)null, false),
            "LeftPacked1" => (user | weightedNextLeft, user, Weighted(0x00010d05u), (char?)null, (char?)null, false),
            "BothPacked1" => (user | weightedRight | weightedNextLeft, user, Weighted(0x00010001u), (char?)null, (char?)null, false),
            "RightPacked3333" => (user | weightedRight, user, Weighted(0x0d050d05u), (char?)null, (char?)null, false),
            "BothPacked9999" => (user | weightedRight | weightedNextLeft, user, Weighted(0x270f270fu), (char?)null, (char?)null, false),
            "RightPacked10000" => (user | weightedRight, user, Weighted(0x0d052710u), (char?)null, (char?)null, false),
            "LeftPacked10000" => (user | weightedNextLeft, user, Weighted(0x27100d05u), (char?)null, (char?)null, false),
            "BothPacked65535" => (user | weightedRight | weightedNextLeft, user, Weighted(0xffffffffu), (char?)null, (char?)null, false),
            "BrokenBoth" => (broken | weightedRight | weightedNextLeft, broken, Weighted(0x206315fbu), (char?)null, (char?)null, false),
            "AutoProgressive" => (0x6108, 0x6108, new[] { 0, 0, defaultPacked, 0, 0, 0, defaultPacked, 0 }, (char?)null, (char?)null, false),
            "AutoClamp" => (0x3108, 0x3108, new[] { 0, 0, defaultPacked, 0, 0, 0, defaultPacked, 0 }, (char?)null, (char?)null, false),
            "TcbAsymmetric" => (0x208, 0x208, new[]
            {
                BitConverter.SingleToInt32Bits(0.2f),
                BitConverter.SingleToInt32Bits(-0.3f),
                BitConverter.SingleToInt32Bits(0.4f), 0,
                BitConverter.SingleToInt32Bits(-0.1f),
                BitConverter.SingleToInt32Bits(0.25f),
                BitConverter.SingleToInt32Bits(-0.35f), 0,
            }, (char?)null, (char?)null, false),
            "PostSlope" => (user | weightedRight | weightedNextLeft, user, Weighted(0x206315fbu), (char?)null, (char?)'K', false),
            "PostRepeat" => (user | weightedRight | weightedNextLeft, user, Weighted(0x206315fbu), (char?)null, (char?)'R', false),
            "PostMirror" => (user | weightedRight | weightedNextLeft, user, Weighted(0x206315fbu), (char?)null, (char?)'M', false),
            "PostRepeatRelative" => (user | weightedRight | weightedNextLeft, user, Weighted(0x206315fbu), (char?)null, (char?)'A', false),
            "PreSlopeDelayed" => (user | weightedRight | weightedNextLeft, user, Weighted(0x206315fbu), (char?)'K', (char?)null, true),
            "PreRepeatDelayed" => (user | weightedRight | weightedNextLeft, user, Weighted(0x206315fbu), (char?)'R', (char?)null, true),
            "PreMirrorDelayed" => (user | weightedRight | weightedNextLeft, user, Weighted(0x206315fbu), (char?)'M', (char?)null, true),
            "PreRepeatRelativeDelayed" => (user | weightedRight | weightedNextLeft, user, Weighted(0x206315fbu), (char?)'A', (char?)null, true),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
        };

        var rewritten = block.Replace(
            "\t\t\ta: 50332680,1032",
            "\t\t\ta: " + config.Item1.ToString(CultureInfo.InvariantCulture) + "," +
            config.Item2.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        rewritten = rewritten.Replace(
            "\t\t\ta: 1067999030,1046062303,543364603,0,1046060188,0,218434821,0",
            "\t\t\ta: " + string.Join(",", config.Item3.Select(value =>
                value.ToString(CultureInfo.InvariantCulture))), StringComparison.Ordinal);
        if (config.Item6)
        {
            rewritten = rewritten.Replace(
                "\t\t\ta: 1924423250,38488465000",
                "\t\t\ta: 19244232500,38488465000", StringComparison.Ordinal);
        }
        if (config.Item4.HasValue)
            rewritten = AddFbxCurveExtrapolation(rewritten, "Pre-Extrapolation", config.Item4.Value);
        if (config.Item5.HasValue)
            rewritten = AddFbxCurveExtrapolation(rewritten, "Post-Extrapolation", config.Item5.Value);
        Assert.NotEqual(block, rewritten);
        return source.Replace(block, rewritten, StringComparison.Ordinal);
    }

    private static string AddFbxCurveExtrapolation(string block, string name, char type)
    {
        var close = block.LastIndexOf('}');
        Assert.True(close >= 0);
        return block.Substring(0, close) + "\t\t" + name + ":  {\n" +
            "\t\t\tType: " + type + "\n\t\t\tRepetition: -1\n\t\t}\n\t}";
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

    private static AnimationCurve VisibilityCurve(AnimationClip clip, string path) =>
        clip.bindings.Single(binding => binding.type == typeof(Renderer) &&
            string.Equals(binding.path, path, StringComparison.Ordinal) &&
            string.Equals(binding.propertyName, "m_Enabled", StringComparison.Ordinal)).curve;

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
