using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnityEngine.Networking;

public class UnityWebRequest : IDisposable
{
    public enum Result
    {
        InProgress,
        Success,
        ConnectionError,
        ProtocolError,
        DataProcessingError
    }

    private readonly Dictionary<string, string> _requestHeaders = new();
    private readonly Dictionary<string, string> _responseHeaders = new();
    private bool _disposed;

    public string url { get; set; } = string.Empty;
    public string method { get; set; } = "GET";
    public DownloadHandler? downloadHandler { get; set; }
    public UploadHandler? uploadHandler { get; set; }
    public int timeout { get; set; } = 60;
    public int redirectLimit { get; set; } = 32;
    public bool useHttpContinue { get; set; } = true;
    public bool chunkedTransfer { get; set; }
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
    public float progress => isDone ? 1f : (downloadProgress + uploadProgress) / 2f;

    public Result result
    {
        get
        {
            if (!isDone) return Result.InProgress;
            if (isNetworkError) return Result.ConnectionError;
            if (isHttpError) return Result.ProtocolError;
            return Result.Success;
        }
    }

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

    private CancellationTokenSource? _cts;
    private UnityWebRequestAsyncOperation? _pendingOp;

    /// <summary>Shared cookie jar (Unity WebRequest cookie cache). Replaced on ClearCookieCache.</summary>
    private static CookieContainer s_CookieJar = new CookieContainer();
    private static readonly object s_CookieLock = new object();

    /// <summary>When true, ServerCertificateCustomValidationCallback accepts all (dev only).</summary>
    public static bool dangerAcceptAllCertificates { get; set; }

    private HttpClientHandler CreateHandler()
    {
        int redirects = redirectLimit < 0 ? 0 : redirectLimit;
        CookieContainer jar;
        lock (s_CookieLock) jar = s_CookieJar;
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = redirects > 0,
            MaxAutomaticRedirections = redirects > 0 ? Math.Min(redirects, 50) : 1,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = true,
            CookieContainer = jar
        };

        // Certificate validation — wire CertificateHandler when provided
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            if (dangerAcceptAllCertificates) return true;
            if (certificateHandler == null)
                return errors == System.Net.Security.SslPolicyErrors.None;
            byte[] raw = cert?.GetRawCertData() ?? Array.Empty<byte>();
            return certificateHandler.ValidateCertificateInternal(raw);
        };
        return handler;
    }

    private HttpClient CreateClientForRequest()
    {
        var c = new HttpClient(CreateHandler());
        c.Timeout = Timeout.InfiniteTimeSpan;
        return c;
    }

    /// <summary>Send request (async completion). Use WaitForCompletion() for blocking.</summary>
    public UnityWebRequestAsyncOperation SendWebRequest()
    {
        if (_pendingOp != null) return _pendingOp;
        var operation = new UnityWebRequestAsyncOperation { webRequest = this };
        _pendingOp = operation;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        Task.Run(async () =>
        {
            try
            {
                await ExecuteRequestAsync(token).ConfigureAwait(false);
            }
            finally
            {
                operation.SetDone();
            }
        }, token);
        return operation;
    }

    /// <summary>Unity 2021+ blocking wait (also used by tests).</summary>
    public void WaitForCompletion()
    {
        if (isDone) return;
        if (_pendingOp == null)
            SendWebRequest();
        int ms = timeout > 0 ? timeout * 1000 + 5000 : 120_000;
        int waited = 0;
        while (!isDone && waited < ms)
        {
            Thread.Sleep(10);
            waited += 10;
        }
        if (!isDone)
        {
            Abort();
            error = "Request timed out (WaitForCompletion)";
            isNetworkError = true;
            isDone = true;
            _pendingOp?.SetDone();
        }
    }

    private async Task ExecuteRequestAsync(CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Fail("Invalid URL", connection: true);
                return;
            }

            // file:// or local absolute path
            if (TryReadLocal(url, out var localBytes, out var localError))
            {
                if (localError != null)
                {
                    Fail(localError, connection: true);
                    return;
                }
                await DeliverBytes(localBytes ?? Array.Empty<byte>(), 200, "application/octet-stream", ct).ConfigureAwait(false);
                return;
            }

            using var client = CreateClientForRequest();
            using var req = new HttpRequestMessage(new HttpMethod(method ?? "GET"), url);
            foreach (var h in _requestHeaders)
            {
                if (string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    continue;
                try { req.Headers.TryAddWithoutValidation(h.Key, h.Value); } catch { }
            }

            if (uploadHandler != null && uploadHandler.data != null && uploadHandler.data.Length > 0
                && !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                var content = new ByteArrayContent(uploadHandler.data);
                if (!string.IsNullOrEmpty(uploadHandler.contentType))
                    content.Headers.TryAddWithoutValidation("Content-Type", uploadHandler.contentType);
                req.Content = content;
                uploadedBytes = (ulong)uploadHandler.data.Length;
                uploadProgress = 1f;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (timeout > 0)
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeout));

            HttpResponseMessage resp;
            try
            {
                resp = await client.SendAsync(req, HttpCompletionOption.ResponseContentRead, timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Fail("Request timeout", connection: true);
                return;
            }
            catch (HttpRequestException ex)
            {
                Fail(ex.Message, connection: true);
                return;
            }

            using (resp)
            {
                responseCode = (long)resp.StatusCode;
                isHttpError = (int)resp.StatusCode >= 400;
                _responseHeaders.Clear();
                foreach (var h in resp.Headers)
                    _responseHeaders[h.Key] = string.Join(",", h.Value);
                if (resp.Content?.Headers != null)
                {
                    foreach (var h in resp.Content.Headers)
                        _responseHeaders[h.Key] = string.Join(",", h.Value);
                }

                byte[] body = Array.Empty<byte>();
                if (!string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) && resp.Content != null)
                    body = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                string ctype = "application/octet-stream";
                if (_responseHeaders.TryGetValue("Content-Type", out var ctHeader))
                    ctype = ctHeader;

                await DeliverBytes(body, responseCode, ctype, ct).ConfigureAwait(false);
                if (isHttpError)
                    error = $"HTTP/{responseCode}";
            }
        }
        catch (Exception ex)
        {
            Fail(ex.Message, connection: true);
        }
    }

    private async Task DeliverBytes(byte[] body, long code, string contentType, CancellationToken ct)
    {
        await Task.Yield();
        if (ct.IsCancellationRequested)
        {
            Fail("Request aborted", connection: true);
            return;
        }

        responseCode = code;
        isHttpError = code >= 400;
        _responseHeaders["Content-Type"] = contentType;
        _responseHeaders["Content-Length"] = body.Length.ToString();

        if (downloadHandler != null && !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            downloadHandler.ReceiveContentLength(body.LongLength);
            if (body.Length > 0)
            {
                const int chunk = 64 * 1024;
                int offset = 0;
                while (offset < body.Length)
                {
                    int n = Math.Min(chunk, body.Length - offset);
                    var slice = new byte[n];
                    Buffer.BlockCopy(body, offset, slice, 0, n);
                    downloadHandler.ReceiveData(slice, n);
                    offset += n;
                    downloadedBytes = (ulong)offset;
                    downloadProgress = body.Length > 0 ? (float)offset / body.Length : 1f;
                }
            }
            downloadHandler.CompleteContent();
            downloadedBytes = (ulong)body.Length;
            downloadProgress = 1f;
        }

        isDone = true;
        isNetworkError = false;
        if (!isHttpError)
            error = string.Empty;
    }

    private static bool TryReadLocal(string url, out byte[]? data, out string? error)
    {
        data = null;
        error = null;
        try
        {
            string path = url;
            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(url);
                path = uri.LocalPath;
                if (Path.DirectorySeparatorChar == '\\' && path.StartsWith("/"))
                    path = path.TrimStart('/');
            }
            else if (!Path.IsPathRooted(url) && !url.Contains("://"))
            {
                // relative local path
                path = Path.GetFullPath(url);
            }
            else if (url.Contains("://"))
            {
                return false; // remote
            }

            if (File.Exists(path))
            {
                data = File.ReadAllBytes(path);
                return true;
            }
            // not a local existing file → treat as remote if scheme present
            if (url.Contains("://")) return false;
            error = "Local file not found: " + path;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return true;
        }
    }

    private void Fail(string message, bool connection)
    {
        error = message ?? string.Empty;
        isNetworkError = connection;
        isHttpError = !connection && responseCode >= 400;
        isDone = true;
        downloadProgress = isDone ? downloadProgress : 0f;
    }

    public void Abort()
    {
        if (isDone) return;
        try { _cts?.Cancel(); } catch { }
        error = "Request aborted";
        isNetworkError = true;
        isDone = true;
        _pendingOp?.SetDone();
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

    public string GetResponseHeader(string name)
    {
        return _responseHeaders.TryGetValue(name, out var value) ? value : null;
    }

    public Dictionary<string, string> GetResponseHeaders()
    {
        return new Dictionary<string, string>(_responseHeaders);
    }

    public void ClearCookieCache()
    {
        _requestHeaders.Remove("Cookie");
        lock (s_CookieLock)
            s_CookieJar = new CookieContainer();
    }

    public void ClearCookieCache(Uri uri)
    {
        if (uri == null) { ClearCookieCache(); return; }
        lock (s_CookieLock)
        {
            try
            {
                var cookies = s_CookieJar.GetCookies(uri);
                foreach (Cookie c in cookies)
                    c.Expired = true;
            }
            catch { }
        }
    }

    /// <summary>Set a cookie into the shared jar (tests / session bootstrap).</summary>
    public static void SetCookie(string url, string cookieHeader)
    {
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(cookieHeader)) return;
        lock (s_CookieLock)
        {
            try { s_CookieJar.SetCookies(new Uri(url), cookieHeader); } catch { }
        }
    }

    public static string GetCookieHeader(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        lock (s_CookieLock)
        {
            try
            {
                var cookies = s_CookieJar.GetCookies(new Uri(url));
                if (cookies.Count == 0) return string.Empty;
                var sb = new StringBuilder();
                foreach (Cookie c in cookies)
                {
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append(c.Name).Append('=').Append(c.Value);
                }
                return sb.ToString();
            }
            catch { return string.Empty; }
        }
    }

    public static int GetCookieCount(string url)
    {
        if (string.IsNullOrEmpty(url)) return 0;
        lock (s_CookieLock)
        {
            try { return s_CookieJar.GetCookies(new Uri(url)).Count; }
            catch { return 0; }
        }
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
    public static UnityWebRequest Post(string url, WWWForm formData) => new(url, "POST", new DownloadHandlerBuffer(), formData != null ? new UploadHandlerRaw(formData.data) { contentType = formData.headers.TryGetValue("Content-Type", out var ct) ? ct : "application/x-www-form-urlencoded" } : null);
    public static UnityWebRequest Post(string url, Dictionary<string, string> formFields)
    {
        var serialized = SerializeSimpleForm(formFields);
        return new UnityWebRequest(url, "POST", new DownloadHandlerBuffer(), new UploadHandlerRaw(Encoding.UTF8.GetBytes(serialized)) { contentType = "application/x-www-form-urlencoded" });
    }
    public static UnityWebRequest Put(string url, byte[] bodyData) => new(url, "PUT", new DownloadHandlerBuffer(), bodyData != null ? new UploadHandlerRaw(bodyData) : null);
    public static UnityWebRequest Put(string url, string bodyData) => new(url, "PUT", new DownloadHandlerBuffer(), new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyData)));
    public static UnityWebRequest Delete(string url) => new(url, "DELETE", new DownloadHandlerBuffer(), null);
    public static UnityWebRequest Head(string url) => new(url, "HEAD", null, null);
    public static UnityWebRequest GetTexture(string url) => new(url, "GET", new DownloadHandlerTexture(), null);
    public static UnityWebRequest GetAudioClip(string url, AudioType audioType) => new(url, "GET", new DownloadHandlerAudioClip(audioType), null);
    public static UnityWebRequest GetAssetBundle(string url) => new(url, "GET", DownloadHandlerAssetBundle.Create(url), null);
    public static UnityWebRequest GetAssetBundle(string url, uint crc) => new(url, "GET", DownloadHandlerAssetBundle.Create(url, crc), null);
    public static UnityWebRequest GetAssetBundle(string url, uint version, uint crc) => new(url, "GET", DownloadHandlerAssetBundle.Create(url, crc), null);
    public static UnityWebRequest GetAssetBundle(string url, Hash128 hash, uint crc) => new(url, "GET", DownloadHandlerAssetBundle.Create(url, hash, crc), null);

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

    public UnityWebRequestAsyncOperation GetAwaiter()
    {
        return this;
    }

    public bool IsCompleted => isDone;

    public void GetResult()
    {
    }
}

public enum SecureProtocol
{
    Tls12 = 192,
    Tls11 = 768,
    Tls = 192,
    Ssl3 = 48,
}

public delegate bool CertificateHandlerCallback(byte[] certificateData);

public abstract class CertificateHandler : IDisposable
{
    private bool _disposed;
    private CertificateHandlerCallback? _callback;

    public void AcceptCertificateCallback(CertificateHandlerCallback callback)
    {
        _callback = callback;
    }

    protected abstract bool ValidateCertificate(byte[] certificateData);

    /// <summary>Public entry for policy evaluation (also used by HttpClient TLS callback).</summary>
    public bool ValidateCertificateInternal(byte[] certificateData)
    {
        if (_callback != null)
            return _callback(certificateData);
        return ValidateCertificate(certificateData ?? Array.Empty<byte>());
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

/// <summary>Accept any server certificate (dev / self-signed).</summary>
public sealed class AcceptAllCertificatesSignedWithASpecificKeyPublicKey : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData) => true;
}

/// <summary>Always reject — useful for negative tests.</summary>
public sealed class RejectAllCertificatesHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData) => false;
}

/// <summary>Callback-driven certificate policy.</summary>
public sealed class CallbackCertificateHandler : CertificateHandler
{
    private readonly Func<byte[], bool> _validate;
    public CallbackCertificateHandler(Func<byte[], bool> validate) =>
        _validate = validate ?? (_ => false);
    protected override bool ValidateCertificate(byte[] certificateData) => _validate(certificateData ?? Array.Empty<byte>());
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
            if (d == null || d.Length == 0) return string.Empty;
            // strip UTF-8 BOM if present (common on Windows text files)
            int offset = 0;
            if (d.Length >= 3 && d[0] == 0xEF && d[1] == 0xBB && d[2] == 0xBF)
                offset = 3;
            return Encoding.UTF8.GetString(d, offset, d.Length - offset);
        }
    }
    public float progress { get; protected set; }

    protected abstract byte[]? GetData();

    protected virtual byte[] GetDataProtected()
    {
        return GetData() ?? Array.Empty<byte>();
    }

    protected virtual string GetTextProtected()
    {
        return text;
    }

    protected virtual float GetProgressProtected()
    {
        return progress;
    }

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

    public float GetProgress()
    {
        return progress;
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

    public DownloadHandlerBuffer()
    {
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
    private readonly bool _append;
    private FileStream? _fileStream;
    private bool _removeFileOnAbort;

    public DownloadHandlerFile(string path) : this(path, false)
    {
    }

    public DownloadHandlerFile(string path, bool append)
    {
        _path = path;
        _append = append;
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
            _fileStream = new FileStream(_path, _append ? FileMode.Append : FileMode.Create, FileAccess.Write);
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
            texture.LoadImage(_stream.ToArray(), markNonReadable);
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
    private byte[]? _cached;
    private readonly string? _url;
    private AssetBundle? _assetBundle;
    public uint crc { get; set; }
    public uint version { get; set; }
    public Hash128 hash { get; set; }
    public bool autoLoadAssetBundle { get; set; } = true;

    public AssetBundle? assetBundle => _assetBundle;

    private DownloadHandlerAssetBundle()
    {
    }

    public DownloadHandlerAssetBundle(string url, uint crc)
    {
        _url = url;
        this.crc = crc;
    }

    public DownloadHandlerAssetBundle(string url, uint version, uint crc)
    {
        _url = url;
        this.version = version;
        this.crc = crc;
    }

    public DownloadHandlerAssetBundle(string url, Hash128 hash, uint crc)
    {
        _url = url;
        this.hash = hash;
        this.crc = crc;
    }

    public DownloadHandlerAssetBundle(AssetBundle bundle)
    {
        _assetBundle = bundle;
        autoLoadAssetBundle = false;
    }

    public static DownloadHandlerAssetBundle Create(string url)
    {
        return new DownloadHandlerAssetBundle(url, 0u);
    }

    public static DownloadHandlerAssetBundle Create(string url, uint crc)
    {
        return new DownloadHandlerAssetBundle(url, crc);
    }

    public static DownloadHandlerAssetBundle Create(string url, Hash128 hash, uint crc)
    {
        return new DownloadHandlerAssetBundle(url, hash, crc);
    }

    protected override byte[] GetData()
    {
        if (_cached != null) return _cached;
        try { return _stream.ToArray(); }
        catch { return Array.Empty<byte>(); }
    }

    internal override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0) return true;
        _stream.Write(data, 0, dataLength);
        return true;
    }

    internal override void CompleteContent()
    {
        try
        {
            _cached = _stream.ToArray();
            if (autoLoadAssetBundle && _cached.Length > 0)
                _assetBundle = AssetBundle.LoadFromMemory(_cached, crc);
        }
        catch
        {
            _cached = Array.Empty<byte>();
        }
        base.CompleteContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _stream.Dispose(); } catch { }
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

    public DownloadHandlerAudioClip()
    {
        audioType = AudioType.UNKNOWN;
    }

    public DownloadHandlerAudioClip(string url, AudioType audioType)
    {
        _ = url;
        this.audioType = audioType;
    }

    public DownloadHandlerAudioClip(AudioType audioType)
    {
        this.audioType = audioType;
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
    private bool _hasBinaryData;

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
        _hasBinaryData = true;
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
    WAVPACK,
    AUDIOQUEUE
}

public static class UnityWebRequestCompat
{
    [Obsolete("Use UnityWebRequest.Result.ConnectionError instead")]
    public const UnityWebRequest.Result NetworkError = UnityWebRequest.Result.ConnectionError;
}

[Obsolete("Use UnityWebRequest.Result instead")]
public enum NetworkError
{
    Ok,
    WrongConnection,
    VersionMismatch,
    NSURIError,
    CannotConnectToHost,
    ConnectionLost,
    ConnectionTimedOut,
    SSLConnectionError,
    DataProcessingError,
    Unknown
}
