using System.Text;

namespace UnityEngine;

/// <summary>
/// Legacy WWW class.
/// </summary>
public class WWW : IDisposable
{
    public string url { get; }
    public AssetBundle? assetBundle => null;
    public byte[] bytes => Array.Empty<byte>();
    public int bytesDownloaded => 0;
    public string error => string.Empty;
    public bool isDone => true;
    public float progress => 1f;
    public float uploadProgress => 1f;
    public string text => string.Empty;
    public Texture2D? texture => null;
    public Texture2D? textureNonReadable => null;
    public AudioClip? audioClip => null;
    public MovieTexture? movie => null;
    public int size => 0;
    public Dictionary<string, string> responseHeaders => new();

    public WWW(string url)
    {
        this.url = url;
    }

    public WWW(string url, WWWForm form)
    {
        this.url = url;
        _ = form;
    }

    public WWW(string url, byte[] postData)
    {
        this.url = url;
        _ = postData;
    }

    public WWW(string url, byte[] postData, Dictionary<string, string> headers)
    {
        this.url = url;
        _ = postData;
        _ = headers;
    }

    public void Dispose() { }
    public void LoadImageIntoTexture(Texture2D tex) { }
    public static string EscapeURL(string url) => Uri.EscapeDataString(url);
    public static string UnEscapeURL(string url) => Uri.UnescapeDataString(url);
    public static AudioClip GetAudioClip(string url) => new AudioClip();
    public static AssetBundle GetAssetBundle(string url) => new AssetBundle();

    public static ThreadPriority threadPriority { get; set; }
}

public class MovieTexture : Texture
{
    public bool isReadyToPlay => true;
    public bool loop { get; set; }
    public void Play() { }
    public void Stop() { }
    public void Pause() { }
}
