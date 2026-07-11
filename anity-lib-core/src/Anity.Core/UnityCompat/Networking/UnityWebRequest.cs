using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityEngine.Networking;

public class UnityWebRequest : IDisposable
{
    private readonly Dictionary<string, string> _requestHeaders = new();
    private bool _disposed;

    public string url { get; set; } = string.Empty;
    public string method { get; set; } = "GET";
    public DownloadHandler? downloadHandler { get; set; }
    public UploadHandler? uploadHandler { get; set; }
    public int timeout { get; set; } = 60;
    public int redirectLimit { get; set; } = 32;
    public bool useHttpContinue { get; set; } = true;
    public CertificateHandler? certificateHandler { get; set; }
    public bool disposeCertificateHandlerOnDispose { get; set; } = true;
    public bool disposeDownloadHandlerOnDispose { get; set; } = true;
    public bool disposeUploadHandlerOnDispose { get; set; } = true;

    public bool isDone { get; protected set; }
    public bool isNetworkError { get; protected set; }
    public bool isHttpError { get; protected set; }
    public string error { get; protected set; } = string.Empty;
    public long responseCode { get; protected set; }
    public ulong downloadedBytes { get; protected set; }
    public ulong uploadedBytes { get; protected set; }
    public float uploadProgress { get; protected set; }
    public float downloadProgress { get; protected set; }

    public UnityWebRequest()
    {
    }

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

    public UnityWebRequest(string url, string method, DownloadHandler? downloadHandler, UploadHandler? uploadHandler, CertificateHandler? certificateHandler)
        : this(url, method, downloadHandler, uploadHandler)
    {
        this.certificateHandler = certificateHandler;
    }

    public UnityWebRequestAsyncOperation SendWebRequest()
    {
        var operation = new UnityWebRequestAsyncOperation { webRequest = this };
        SimulateRequest(operation);
        return operation;
    }

    private void SimulateRequest(UnityWebRequestAsyncOperation operation)
    {
        try
        {
            if (uploadHandler != null)
            {
                uploadedBytes = (ulong)uploadHandler.data.Length;
                uploadProgress = 1f;
            }

            byte[]? responseData = null;
            if (method != "HEAD" && method != "DELETE")
            {
                responseData = Array.Empty<byte>();
                if (downloadHandler != null)
                {
                    downloadHandler.ReceiveContentLength(responseData.LongLength);
                    if (responseData.Length > 0)
                    {
                        downloadHandler.ReceiveData(responseData, responseData.Length);
                    }
                    downloadHandler.CompleteContent();
                    downloadedBytes = (ulong)responseData.Length;
                    downloadProgress = 1f;
                }
            }

            responseCode = 200;
            isDone = true;
            isNetworkError = false;
            isHttpError = false;
            operation.SetDone();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            isNetworkError = true;
            isDone = true;
            responseCode = 0;
            operation.SetDone();
        }
    }

    public void Abort()
    {
        if (!isDone)
        {
            error = "Request aborted";
            isNetworkError = true;
            isDone = true;
        }
    }

    public string GetRequestHeader(string name)
    {
        return _requestHeaders.TryGetValue(name, out var value) ? value : string.Empty;
    }

    public void SetRequestHeader(string name, string value)
    {
        _requestHeaders[name] = value;
    }

    public Dictionary<string, string> GetRequestHeaders()
    {
        return new Dictionary<string, string>(_requestHeaders);
    }

    public void ClearCookieCache()
    {
    }

    public void ClearCookieCache(Uri uri)
    {
        _ = uri;
    }

    public static string EscapeURL(string s) => Uri.EscapeDataString(s);
    public static string UnEscapeURL(string s) => Uri.UnescapeDataString(s);
    public static string SerializeFormSections(List<IMultipartFormSection> sections, byte[] boundary) => string.Empty;
    public static byte[] GenerateBoundary() => Guid.NewGuid().ToByteArray();
    public static string SerializeSimpleForm(Dictionary<string, string> formFields)
    {
        if (formFields == null || formFields.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        var first = true;
        foreach (var kvp in formFields)
        {
            if (!first) sb.Append('&');
            sb.Append(Uri.EscapeDataString(kvp.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(kvp.Value));
            first = false;
        }
        return sb.ToString();
    }

    public static UnityWebRequest Get(string url) => new(url, "GET", new DownloadHandlerBuffer(), null);
    public static UnityWebRequest Post(string url, string postData) => new(url, "POST", new DownloadHandlerBuffer(), new UploadHandlerRaw(Encoding.UTF8.GetBytes(postData)));
    public static UnityWebRequest Post(string url, WWWForm formData) => new(url, "POST", new DownloadHandlerBuffer(), formData != null ? new UploadHandlerRaw(formData.data) { contentType = "application/x-www-form-urlencoded" } : null);
    public static UnityWebRequest Put(string url, byte[] bodyData) => new(url, "PUT", new DownloadHandlerBuffer(), bodyData != null ? new UploadHandlerRaw(bodyData) : null);
    public static UnityWebRequest Put(string url, string bodyData) => new(url, "PUT", new DownloadHandlerBuffer(), new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyData)));
    public static UnityWebRequest Delete(string url) => new(url, "DELETE", new DownloadHandlerBuffer(), null);
    public static UnityWebRequest Head(string url) => new(url, "HEAD", null, null);
    public static UnityWebRequest GetTexture(string url) => new(url, "GET", new DownloadHandlerTexture(), null);
    public static UnityWebRequest GetAudioClip(string url, AudioType audioType) => new(url, "GET", new DownloadHandlerAudioClip { audioType = audioType }, null);
    public static UnityWebRequest GetAssetBundle(string url) => new(url, "GET", new DownloadHandlerAssetBundle(), null);
    public static UnityWebRequest GetAssetBundle(string url, uint crc) => new(url, "GET", new DownloadHandlerAssetBundle { crc = crc }, null);
    public static UnityWebRequest GetAssetBundle(string url, uint version, uint crc) => new(url, "GET", new DownloadHandlerAssetBundle { version = version, crc = crc }, null);
    public static UnityWebRequest GetAssetBundle(string url, Hash128 hash, uint crc) => new(url, "GET", new DownloadHandlerAssetBundle { hash = hash, crc = crc }, null);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            if (disposeDownloadHandlerOnDispose) downloadHandler?.Dispose();
            if (disposeUploadHandlerOnDispose) uploadHandler?.Dispose();
            if (disposeCertificateHandlerOnDispose) certificateHandler?.Dispose();
        }
        _disposed = true;
    }
}

public class UnityWebRequestAsyncOperation : AsyncOperation
{
    public UnityWebRequest webRequest { get; internal set; } = null!;

    internal UnityWebRequestAsyncOperation() : base(false)
    {
    }

    internal void SetDone()
    {
        isDone = true;
    }
}

public enum SecureProtocol
{
    Tls12 = 192,
    Tls11 = 768,
    Tls = 192,
    Ssl3 = 48,
}

public abstract class CertificateHandler : IDisposable
{
    protected abstract bool ValidateCertificate(byte[] certificateData);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

public abstract class DownloadHandler : IDisposable
{
    private bool _disposed;

    public bool isDone { get; protected set; }
    public byte[] data => GetData() ?? Array.Empty<byte>();
    public string text
    {
        get
        {
            var d = GetData();
            return d != null ? Encoding.UTF8.GetString(d) : string.Empty;
        }
    }
    public float progress { get; protected set; }

    protected abstract byte[]? GetData();

    internal virtual bool ReceiveData(byte[] data, int dataLength)
    {
        _ = data;
        _ = dataLength;
        return true;
    }

    internal virtual void ReceiveContentLength(long contentLength)
    {
        _ = contentLength;
    }

    internal virtual void CompleteContent()
    {
        isDone = true;
        progress = 1f;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        _disposed = true;
    }
}

public class DownloadHandlerBuffer : DownloadHandler
{
    private readonly MemoryStream _stream = new();

    protected override byte[] GetData()
    {
        return _stream.ToArray();
    }

    internal override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0) return true;
        _stream.Write(data, 0, dataLength);
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class DownloadHandlerFile : DownloadHandler
{
    private readonly string _path;
    private FileStream? _fileStream;
    private bool _removeFileOnAbort;

    public DownloadHandlerFile(string path)
    {
        _path = path;
    }

    public DownloadHandlerFile(string path, bool append)
    {
        _path = path;
        _ = append;
    }

    public bool removeFileOnAbort
    {
        get => _removeFileOnAbort;
        set => _removeFileOnAbort = value;
    }

    protected override byte[]? GetData()
    {
        return null;
    }

    internal override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0) return true;
        EnsureStream();
        _fileStream?.Write(data, 0, dataLength);
        return true;
    }

    internal override void ReceiveContentLength(long contentLength)
    {
        _ = contentLength;
        EnsureStream();
    }

    internal override void CompleteContent()
    {
        _fileStream?.Flush();
        _fileStream?.Dispose();
        _fileStream = null;
        base.CompleteContent();
    }

    private void EnsureStream()
    {
        if (_fileStream == null && !string.IsNullOrEmpty(_path))
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _fileStream = new FileStream(_path, FileMode.Create, FileAccess.Write);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fileStream?.Dispose();
            _fileStream = null;
        }
        base.Dispose(disposing);
    }
}

public class DownloadHandlerTexture : DownloadHandler
{
    private readonly MemoryStream _stream = new();
    public Texture2D? texture { get; protected set; }
    public bool readable { get; set; }
    public bool markNonReadable { get; set; }

    public DownloadHandlerTexture()
    {
        readable = true;
    }

    public DownloadHandlerTexture(bool readable)
    {
        this.readable = readable;
    }

    protected override byte[] GetData()
    {
        return _stream.ToArray();
    }

    internal override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0) return true;
        _stream.Write(data, 0, dataLength);
        return true;
    }

    internal override void CompleteContent()
    {
        texture = new Texture2D(2, 2);
        if (_stream.Length > 0)
        {
            texture.LoadImage(_stream.ToArray());
        }
        _stream.Dispose();
        base.CompleteContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class DownloadHandlerAssetBundle : DownloadHandler
{
    private readonly MemoryStream _stream = new();
    public AssetBundle? assetBundle { get; protected set; }
    public uint crc { get; set; }
    public uint version { get; set; }
    public Hash128 hash { get; set; }
    public bool autoLoadAssetBundle { get; set; } = true;

    protected override byte[] GetData()
    {
        return _stream.ToArray();
    }

    internal override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0) return true;
        _stream.Write(data, 0, dataLength);
        return true;
    }

    internal override void CompleteContent()
    {
        if (autoLoadAssetBundle && _stream.Length > 0)
        {
            assetBundle = AssetBundle.LoadFromMemory(_stream.ToArray(), crc);
        }
        _stream.Dispose();
        base.CompleteContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class DownloadHandlerAudioClip : DownloadHandler
{
    private readonly MemoryStream _stream = new();
    public AudioClip? audioClip { get; protected set; }
    public AudioType audioType { get; set; }
    public bool compressed { get; set; }
    public bool streamAudio { get; set; }

    protected override byte[] GetData()
    {
        return _stream.ToArray();
    }

    internal override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0) return true;
        _stream.Write(data, 0, dataLength);
        return true;
    }

    internal override void CompleteContent()
    {
        audioClip = new AudioClip();
        if (_stream.Length > 0)
        {
            audioClip.name = "DownloadedAudio";
        }
        _stream.Dispose();
        base.CompleteContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }
        base.Dispose(disposing);
    }
}

public abstract class UploadHandler : IDisposable
{
    private bool _disposed;

    public string contentType { get; set; } = "application/octet-stream";
    public virtual byte[] data { get; protected set; } = Array.Empty<byte>();
    public virtual float progress => data != null && data.Length > 0 ? 1f : 0f;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        _disposed = true;
    }
}

public class UploadHandlerRaw : UploadHandler
{
    public UploadHandlerRaw(byte[] data)
    {
        this.data = data ?? Array.Empty<byte>();
    }

    public UploadHandlerRaw(byte[] data, int offset, int count)
    {
        if (data == null)
        {
            this.data = Array.Empty<byte>();
            return;
        }
        var result = new byte[count];
        Array.Copy(data, offset, result, 0, count);
        this.data = result;
    }
}

public class UploadHandlerFile : UploadHandler
{
    public UploadHandlerFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            data = File.ReadAllBytes(filePath);
            contentType = "application/octet-stream";
        }
        else
        {
            data = Array.Empty<byte>();
        }
    }
}

public interface IMultipartFormSection
{
    string sectionName { get; }
    byte[] sectionData { get; }
    string fileName { get; }
    string contentType { get; }
}

public class MultipartFormFileSection : IMultipartFormSection
{
    public string sectionName { get; }
    public byte[] sectionData { get; }
    public string fileName { get; }
    public string contentType { get; }

    public MultipartFormFileSection(string name, byte[] data, string fileName, string contentType)
    {
        sectionName = name;
        sectionData = data ?? Array.Empty<byte>();
        this.fileName = fileName ?? string.Empty;
        this.contentType = contentType ?? "application/octet-stream";
    }

    public MultipartFormFileSection(string name, byte[] data) : this(name, data, name, "application/octet-stream")
    {
    }
}

public class MultipartFormDataSection : IMultipartFormSection
{
    public string sectionName { get; }
    public byte[] sectionData { get; }
    public string fileName => string.Empty;
    public string contentType { get; }

    public MultipartFormDataSection(string name, byte[] data, string contentType)
    {
        sectionName = name;
        sectionData = data ?? Array.Empty<byte>();
        this.contentType = contentType ?? "text/plain";
    }

    public MultipartFormDataSection(string name, string data) : this(name, Encoding.UTF8.GetBytes(data ?? string.Empty), "text/plain")
    {
    }

    public MultipartFormDataSection(string name, byte[] data) : this(name, data, "text/plain")
    {
    }
}

public class WWWForm
{
    private readonly List<byte> _data = new();
    private readonly Dictionary<string, string> _headers = new();

    public byte[] data => _data.ToArray();
    public Dictionary<string, string> headers => new(_headers);

    public WWWForm()
    {
        _headers["Content-Type"] = "application/x-www-form-urlencoded";
    }

    public void AddField(string fieldName, string value)
    {
        AddField(fieldName, value, Encoding.UTF8);
    }

    public void AddField(string fieldName, string value, Encoding e)
    {
        if (_data.Count > 0) _data.Add((byte)'&');
        var bytes = e.GetBytes($"{Uri.EscapeDataString(fieldName)}={Uri.EscapeDataString(value ?? string.Empty)}");
        _data.AddRange(bytes);
    }

    public void AddField(string fieldName, int value)
    {
        AddField(fieldName, value.ToString());
    }

    public void AddBinaryData(string fieldName, byte[] contents)
    {
        AddBinaryData(fieldName, contents, fieldName, "application/octet-stream");
    }

    public void AddBinaryData(string fieldName, byte[] contents, string fileName)
    {
        AddBinaryData(fieldName, contents, fileName, "application/octet-stream");
    }

    public void AddBinaryData(string fieldName, byte[] contents, string fileName, string mimeType)
    {
        _ = fieldName;
        _ = fileName;
        _ = mimeType;
        if (contents != null)
        {
            _data.AddRange(contents);
        }
    }
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
