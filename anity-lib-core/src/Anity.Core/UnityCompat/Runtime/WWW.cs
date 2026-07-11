using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine.Networking;

namespace UnityEngine;

public class WWW : IDisposable
{
    private UnityWebRequest _webRequest;
    private UnityWebRequestAsyncOperation? _operation;
    private bool _disposed;

    public string url { get; }
    public AssetBundle? assetBundle { get; private set; }
    public byte[] bytes
    {
        get
        {
            if (_webRequest?.downloadHandler != null)
                return _webRequest.downloadHandler.data;
            return Array.Empty<byte>();
        }
    }
    public int bytesDownloaded => (int)(_webRequest?.downloadedBytes ?? 0);
    public string error => _webRequest?.error ?? string.Empty;
    public bool isDone => _webRequest?.isDone ?? true;
    public float progress => _webRequest?.downloadProgress ?? 1f;
    public float uploadProgress => _webRequest?.uploadProgress ?? 1f;
    public string text
    {
        get
        {
            if (_webRequest?.downloadHandler != null)
                return _webRequest.downloadHandler.text;
            return string.Empty;
        }
    }
    public Texture2D? texture { get; private set; }
    public Texture2D? textureNonReadable { get; private set; }
    public AudioClip? audioClip { get; private set; }
    public MovieTexture? movie { get; private set; }
    public int size => bytes.Length;
    public Dictionary<string, string> responseHeaders => _webRequest?.GetRequestHeaders() ?? new Dictionary<string, string>();

    public WWW(string url)
    {
        this.url = url;
        _webRequest = UnityWebRequest.Get(url);
        StartLoading();
    }

    public WWW(string url, WWWForm form)
    {
        this.url = url;
        _webRequest = UnityWebRequest.Post(url, form);
        StartLoading();
    }

    public WWW(string url, byte[] postData)
    {
        this.url = url;
        _webRequest = UnityWebRequest.Put(url, postData);
        _webRequest.method = "POST";
        StartLoading();
    }

    public WWW(string url, byte[] postData, Dictionary<string, string> headers)
    {
        this.url = url;
        _webRequest = new UnityWebRequest(url, "POST", new DownloadHandlerBuffer(), new UploadHandlerRaw(postData));
        if (headers != null)
        {
            foreach (var kvp in headers)
            {
                _webRequest.SetRequestHeader(kvp.Key, kvp.Value);
            }
        }
        StartLoading();
    }

    private void StartLoading()
    {
        _operation = _webRequest.SendWebRequest();
        WaitForCompletion();
        ProcessResult();
    }

    private void WaitForCompletion()
    {
        if (_operation == null) return;
        int waitCount = 0;
        while (!_operation.isDone && waitCount < 100)
        {
            Thread.Sleep(1);
            waitCount++;
        }
    }

    private void ProcessResult()
    {
        if (_webRequest == null) return;

        if (_webRequest.downloadHandler is DownloadHandlerTexture texHandler)
        {
            texture = texHandler.texture;
            textureNonReadable = texHandler.texture;
        }
        else if (_webRequest.downloadHandler is DownloadHandlerAudioClip audioHandler)
        {
            audioClip = audioHandler.audioClip;
        }
        else if (_webRequest.downloadHandler is DownloadHandlerAssetBundle abHandler)
        {
            assetBundle = abHandler.assetBundle;
        }
    }

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
            _webRequest?.Dispose();
        }
        _disposed = true;
    }

    public void LoadImageIntoTexture(Texture2D tex)
    {
        if (tex == null || bytes.Length == 0) return;
        tex.LoadImage(bytes);
    }

    public static string EscapeURL(string url) => Uri.EscapeDataString(url);
    public static string UnEscapeURL(string url) => Uri.UnescapeDataString(url);

    public static AudioClip GetAudioClip(string url)
    {
        using var www = new WWW(url);
        return www.audioClip ?? new AudioClip();
    }

    public static AudioClip GetAudioClip(string url, bool compressed)
    {
        _ = compressed;
        return GetAudioClip(url);
    }

    public static AudioClip GetAudioClip(string url, bool compressed, bool stream)
    {
        _ = stream;
        return GetAudioClip(url, compressed);
    }

    public static AudioClip GetAudioClip(string url, AudioType audioType)
    {
        _ = audioType;
        return GetAudioClip(url);
    }

    public static Texture2D GetTexture(string url)
    {
        using var www = new WWW(url);
        return www.texture ?? new Texture2D(2, 2);
    }

    public static Texture2D GetTexture(string url, bool nonReadable)
    {
        _ = nonReadable;
        return GetTexture(url);
    }

    public static AssetBundle LoadUnityWeb(string url)
    {
        using var www = new WWW(url);
        return www.assetBundle ?? new AssetBundle();
    }

    public static AssetBundle LoadFromCacheOrDownload(string url, int version)
    {
        _ = version;
        return LoadUnityWeb(url);
    }

    public static AssetBundle LoadFromCacheOrDownload(string url, int version, uint crc)
    {
        _ = crc;
        return LoadFromCacheOrDownload(url, version);
    }

    public static AssetBundle LoadFromCacheOrDownload(string url, Hash128 hash, uint crc)
    {
        _ = hash;
        _ = crc;
        return LoadUnityWeb(url);
    }

    public static WaitForSeconds WaitForSeconds(float seconds)
    {
        return new WaitForSeconds(seconds);
    }

    public static ThreadPriority threadPriority { get; set; } = ThreadPriority.Normal;
}

public class MovieTexture : Texture
{
    public bool isReadyToPlay => true;
    public bool loop { get; set; }
    public AudioClip audioClip { get; set; } = new AudioClip();

    public void Play() { }
    public void Stop() { }
    public void Pause() { }
}

public enum ThreadPriority
{
    Low = 0,
    BelowNormal = 1,
    Normal = 2,
    High = 4
}
