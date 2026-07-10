using System.Collections.Generic;
using System.Text;

namespace UnityEngine;

/// <summary>
/// UnityWebRequest for HTTP/HTTPS communication.
/// </summary>
public class UnityWebRequest : IDisposable
{
    public string url { get; set; } = string.Empty;
    public string method { get; set; } = "GET";
    public string error { get; protected set; } = string.Empty;
    public long responseCode { get; protected set; }
    public bool isDone { get; protected set; }
    public float downloadProgress { get; protected set; }
    public float uploadProgress { get; protected set; }
    public ulong downloadedBytes { get; protected set; }
    public ulong uploadedBytes { get; protected set; }
    public int timeout { get; set; } = 60;
    public bool disposeCertificateHandlerOnDispose { get; set; }
    public bool disposeDownloadHandlerOnDispose { get; set; } = true;
    public bool disposeUploadHandlerOnDispose { get; set; } = true;

    public DownloadHandler? downloadHandler { get; set; }
    public UploadHandler? uploadHandler { get; set; }

    public UnityWebRequest() { }

    public UnityWebRequest(string url)
    {
        this.url = url;
    }

    public UnityWebRequest(string url, string method)
    {
        this.url = url;
        this.method = method;
    }

    public UnityWebRequest(string url, string method, DownloadHandler? downloadHandler, UploadHandler? uploadHandler)
    {
        this.url = url;
        this.method = method;
        this.downloadHandler = downloadHandler;
        this.uploadHandler = uploadHandler;
    }

    public static UnityWebRequest Get(string url) => new(url, "GET", new DownloadHandlerBuffer(), null);
    public static UnityWebRequest Post(string url, string postData) => new(url, "POST", new DownloadHandlerBuffer(), new UploadHandlerRaw(Encoding.UTF8.GetBytes(postData)));
    public static UnityWebRequest Post(string url, WWWForm formData) => new(url, "POST", new DownloadHandlerBuffer(), formData?.data != null ? new UploadHandlerRaw(formData.data) : null);
    public static UnityWebRequest Put(string url, byte[] bodyData) => new(url, "PUT", new DownloadHandlerBuffer(), bodyData != null ? new UploadHandlerRaw(bodyData) : null);
    public static UnityWebRequest Put(string url, string bodyData) => new(url, "PUT", new DownloadHandlerBuffer(), new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyData)));
    public static UnityWebRequest Delete(string url) => new(url, "DELETE", new DownloadHandlerBuffer(), null);
    public static UnityWebRequest Head(string url) => new(url, "HEAD", new DownloadHandlerBuffer(), null);
    public static UnityWebRequest GetTexture(string url) => new(url, "GET", new DownloadHandlerTexture(), null);
    public static UnityWebRequest GetAudioClip(string url, AudioType audioType) => new(url, "GET", new DownloadHandlerAudioClip(), null);
    public static UnityWebRequest GetAssetBundle(string url) => new(url, "GET", new DownloadHandlerAssetBundle(), null);

    public UnityWebRequest SendWebRequest()
    {
        isDone = true;
        return this;
    }

    public void Dispose()
    {
        if (disposeDownloadHandlerOnDispose) downloadHandler?.Dispose();
        if (disposeUploadHandlerOnDispose) uploadHandler?.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Abort() { }
    public string GetRequestHeader(string name) => string.Empty;
    public void SetRequestHeader(string name, string value) { }
    public void ClearCookieCache() { }
    public void ClearCookieCache(Uri uri) { }

    public static string EscapeURL(string s) => Uri.EscapeDataString(s);
    public static string UnEscapeURL(string s) => Uri.UnescapeDataString(s);
    public static string SerializeFormSections(List<IMultipartFormSection> sections, byte[] boundary) => string.Empty;
    public static byte[] GenerateBoundary() => Guid.NewGuid().ToByteArray();
    public static string SerializeSimpleForm(Dictionary<string, string> formFields) => string.Empty;
}

public abstract class DownloadHandler : IDisposable
{
    public bool isDone { get; protected set; }
    public byte[] data => GetData();
    public string text => GetText();
    public float progress { get; protected set; }

    protected abstract byte[] GetData();
    protected abstract string GetText();
    public virtual void Dispose() { }
}

public class DownloadHandlerBuffer : DownloadHandler
{
    private byte[] _data = Array.Empty<byte>();
    protected override byte[] GetData() => _data;
    protected override string GetText() => Encoding.UTF8.GetString(_data);
}

public class DownloadHandlerTexture : DownloadHandler
{
    public Texture2D? texture { get; protected set; }
    protected override byte[] GetData() => Array.Empty<byte>();
    protected override string GetText() => string.Empty;
}

public class DownloadHandlerAudioClip : DownloadHandler
{
    public AudioClip? audioClip { get; protected set; }
    protected override byte[] GetData() => Array.Empty<byte>();
    protected override string GetText() => string.Empty;
}

public class DownloadHandlerAssetBundle : DownloadHandler
{
    public AssetBundle? assetBundle { get; protected set; }
    protected override byte[] GetData() => Array.Empty<byte>();
    protected override string GetText() => string.Empty;
}

public class DownloadHandlerFile : DownloadHandler
{
    protected override byte[] GetData() => Array.Empty<byte>();
    protected override string GetText() => string.Empty;
}

public class UploadHandler : IDisposable
{
    public string contentType { get; set; } = "application/octet-stream";
    public byte[] data { get; protected set; } = Array.Empty<byte>();
    public virtual void Dispose() { }
}

public class UploadHandlerRaw : UploadHandler
{
    public UploadHandlerRaw(byte[] data)
    {
        this.data = data ?? Array.Empty<byte>();
    }
}

public class UploadHandlerFile : UploadHandler
{
    public UploadHandlerFile(string filePath)
    {
        _ = filePath;
    }
}

public interface IMultipartFormSection
{
    string sectionName { get; }
    byte[] sectionData { get; }
    string fileName { get; }
    string contentType { get; }
}

public class WWWForm
{
    private readonly List<byte> _data = new();
    public byte[] data => _data.ToArray();
    public Dictionary<string, string> headers => new();

    public void AddField(string fieldName, string value) { }
    public void AddField(string fieldName, string value, Encoding e) { }
    public void AddField(string fieldName, int value) { }
    public void AddBinaryData(string fieldName, byte[] contents) { }
    public void AddBinaryData(string fieldName, byte[] contents, string fileName) { }
    public void AddBinaryData(string fieldName, byte[] contents, string fileName, string mimeType) { }
}

public enum AudioType
{
    UNKNOWN,
    ACC,
    AIFF,
    IT,
    MOD,
    MPEG,
    OGGVORBIS,
    S3M,
    WAV,
    XM,
    XMA,
    VAG,
    AUDIOQUEUE
}
