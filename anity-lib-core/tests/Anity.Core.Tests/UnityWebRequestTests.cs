using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>UnityWebRequest — ≥12 boundary cases (file://, headers, handlers, abort).</summary>
[Collection(ComponentAttributeBehaviorCollection.Name)]
public class UnityWebRequestTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public UnityWebRequestTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "uwr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "payload.txt");
        File.WriteAllText(_file, "hello-uwr", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    private static string FileUrl(string path)
    {
        var full = Path.GetFullPath(path).Replace('\\', '/');
        if (!full.StartsWith("/")) full = "/" + full;
        return "file://" + full;
    }

    [Fact]
    public void Get_FileUrl_Succeeds()
    {
        using var req = UnityWebRequest.Get(FileUrl(_file));
        req.timeout = 10;
        req.WaitForCompletion();
        Assert.True(req.isDone);
        Assert.False(req.isNetworkError);
        Assert.Equal(200, req.responseCode);
        Assert.Contains("hello-uwr", req.downloadHandler.text);
    }

    [Fact]
    public void Get_LocalPath_Succeeds()
    {
        using var req = UnityWebRequest.Get(_file);
        req.WaitForCompletion();
        Assert.True(req.isDone);
        Assert.Equal("hello-uwr", req.downloadHandler!.text);
    }

    [Fact]
    public void Get_MissingFile_NetworkError()
    {
        using var req = UnityWebRequest.Get(Path.Combine(_dir, "nope.txt"));
        req.WaitForCompletion();
        Assert.True(req.isDone);
        Assert.True(req.isNetworkError || req.result == UnityWebRequest.Result.ConnectionError);
    }

    [Fact]
    public void EscapeUnescape_RoundTrip()
    {
        string s = "a b+c";
        var e = UnityWebRequest.EscapeURL(s);
        Assert.Equal(s, UnityWebRequest.UnEscapeURL(e));
    }

    [Fact]
    public void SetRequestHeader_RoundTrip()
    {
        using var req = UnityWebRequest.Get(_file);
        req.SetRequestHeader("X-Test", "1");
        Assert.Equal("1", req.GetRequestHeader("X-Test"));
    }

    [Fact]
    public void DownloadHandlerBuffer_HoldsData()
    {
        using var req = UnityWebRequest.Get(_file);
        req.WaitForCompletion();
        Assert.True(req.downloadHandler!.data.Length > 0);
        Assert.Equal(req.downloadedBytes, (ulong)req.downloadHandler.data.Length);
    }

    [Fact]
    public void DownloadHandlerFile_WritesPath()
    {
        string outPath = Path.Combine(_dir, "out.bin");
        using var req = new UnityWebRequest(_file, "GET", new DownloadHandlerFile(outPath), null);
        req.WaitForCompletion();
        Assert.True(File.Exists(outPath));
        Assert.Equal("hello-uwr", File.ReadAllText(outPath));
    }

    [Fact]
    public void Post_FormSerialized()
    {
        var form = new System.Collections.Generic.Dictionary<string, string> { ["a"] = "1", ["b"] = "x y" };
        using var req = UnityWebRequest.Post("http://127.0.0.1:9/nope", form); // port 9 discard
        Assert.Equal("POST", req.method);
        Assert.NotNull(req.uploadHandler);
    }

    [Fact]
    public void Put_Delete_Head_Factories()
    {
        Assert.Equal("PUT", UnityWebRequest.Put(_file, "body").method);
        Assert.Equal("DELETE", UnityWebRequest.Delete(_file).method);
        Assert.Equal("HEAD", UnityWebRequest.Head(_file).method);
    }

    [Fact]
    public void Result_Enum_Success()
    {
        using var req = UnityWebRequest.Get(_file);
        req.WaitForCompletion();
        Assert.Equal(UnityWebRequest.Result.Success, req.result);
    }

    [Fact]
    public void Progress_AfterDone_IsOne()
    {
        using var req = UnityWebRequest.Get(_file);
        req.WaitForCompletion();
        Assert.Equal(1f, req.progress);
        Assert.Equal(1f, req.downloadProgress);
    }

    [Fact]
    public void Abort_BeforeDone_SetsError()
    {
        using var req = UnityWebRequest.Get("http://example.com");
        // start but abort immediately
        req.SendWebRequest();
        req.Abort();
        Assert.True(req.isDone);
        Assert.True(req.isNetworkError);
        Assert.Contains("abort", req.error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAssetBundle_FromLocalBundleFile()
    {
        // build a tiny AB then download via file path handler
        string abDir = Path.Combine(_dir, "ab");
        Directory.CreateDirectory(abDir);
        AssetDatabase.CreateAsset(new TextAsset("bundle-payload"), "Assets/uwr_ab.txt");
        BuildPipeline.BuildAssetBundles(abDir, new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = "uwrb",
                assetNames = new[] { "Assets/uwr_ab.txt" }
            }
        }, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);

        string abPath = Path.Combine(abDir, "uwrb");
        using var req = UnityWebRequest.GetAssetBundle(abPath);
        req.WaitForCompletion();
        Assert.True(req.isDone);
        Assert.False(req.isNetworkError);
        var dh = req.downloadHandler as DownloadHandlerAssetBundle;
        Assert.NotNull(dh);
        Assert.NotNull(dh!.assetBundle);
        Assert.NotEmpty(dh.assetBundle!.GetAllAssetNames());
        dh.assetBundle.Unload(true);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var req = UnityWebRequest.Get(_file);
        req.WaitForCompletion();
        req.Dispose();
    }

    [Fact]
    public void ResponseHeaders_AfterLocalGet()
    {
        using var req = UnityWebRequest.Get(_file);
        req.WaitForCompletion();
        Assert.NotNull(req.GetResponseHeader("Content-Type"));
        Assert.True(req.GetResponseHeaders().Count > 0);
    }

    [Fact]
    public void Head_LocalFile_NoBody()
    {
        using var req = UnityWebRequest.Head(_file);
        req.WaitForCompletion();
        Assert.True(req.isDone);
        Assert.Equal(200, req.responseCode);
    }

    [Fact]
    public void SendWebRequest_ThenWait_Succeeds()
    {
        using var req = UnityWebRequest.Get(_file);
        var op = req.SendWebRequest();
        req.WaitForCompletion();
        Assert.True(op.isDone);
        Assert.Equal(UnityWebRequest.Result.Success, req.result);
    }

    [Fact]
    public void Cookie_SetGetClear()
    {
        string url = "http://example.local/";
        using var clear = UnityWebRequest.Get(url);
        clear.ClearCookieCache();
        UnityWebRequest.SetCookie(url, "sid=abc123");
        Assert.True(UnityWebRequest.GetCookieCount(url) >= 1);
        Assert.Contains("sid=abc123", UnityWebRequest.GetCookieHeader(url));
        clear.ClearCookieCache();
        Assert.Equal(0, UnityWebRequest.GetCookieCount(url));
    }

    [Fact]
    public void CertificateHandler_AcceptAll_Validates()
    {
        var h = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();
        Assert.True(h.ValidateCertificateInternal(new byte[] { 1, 2, 3 }));
        h.Dispose();
    }

    [Fact]
    public void CertificateHandler_RejectAll()
    {
        var h = new RejectAllCertificatesHandler();
        Assert.False(h.ValidateCertificateInternal(new byte[] { 9 }));
    }

    [Fact]
    public void CertificateHandler_Callback()
    {
        var h = new CallbackCertificateHandler(data => data != null && data.Length == 2);
        Assert.True(h.ValidateCertificateInternal(new byte[] { 0, 1 }));
        Assert.False(h.ValidateCertificateInternal(new byte[] { 0 }));
    }

    [Fact]
    public void CertificateHandler_AttachedToRequest()
    {
        using var req = UnityWebRequest.Get(_file);
        req.certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();
        req.WaitForCompletion();
        Assert.Equal(UnityWebRequest.Result.Success, req.result);
    }

    [Fact]
    public void DangerAcceptAllCertificates_Flag()
    {
        bool prev = UnityWebRequest.dangerAcceptAllCertificates;
        try
        {
            UnityWebRequest.dangerAcceptAllCertificates = true;
            Assert.True(UnityWebRequest.dangerAcceptAllCertificates);
        }
        finally
        {
            UnityWebRequest.dangerAcceptAllCertificates = prev;
        }
    }
}
