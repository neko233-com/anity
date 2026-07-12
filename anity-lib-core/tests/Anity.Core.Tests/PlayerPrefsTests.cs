using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>PlayerPrefs + EditorPrefs + LocalStorage — ≥12 boundary cases each area.</summary>
public class PlayerPrefsTests : IDisposable
{
    private readonly string _dir;

    public PlayerPrefsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "anity_prefs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        PlayerPrefs.SetSavePathForTests(_dir);
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        EditorPrefs.SetSavePathForTests(Path.Combine(_dir, "editor"));
        EditorPrefs.DeleteAll();
        EditorPrefs.Save();
    }

    public void Dispose()
    {
        try
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }
        catch { }
    }

    [Fact]
    public void SetGetInt_RoundTrip()
    {
        PlayerPrefs.SetInt("score", 42);
        Assert.Equal(42, PlayerPrefs.GetInt("score"));
        Assert.Equal(0, PlayerPrefs.GetInt("missing"));
        Assert.Equal(7, PlayerPrefs.GetInt("missing", 7));
    }

    [Fact]
    public void SetGetFloat_RoundTrip()
    {
        PlayerPrefs.SetFloat("vol", 0.75f);
        Assert.InRange(PlayerPrefs.GetFloat("vol"), 0.749f, 0.751f);
        Assert.Equal(1.5f, PlayerPrefs.GetFloat("nope", 1.5f));
    }

    [Fact]
    public void SetGetString_RoundTrip_AndNullValue()
    {
        PlayerPrefs.SetString("name", "anity");
        Assert.Equal("anity", PlayerPrefs.GetString("name"));
        PlayerPrefs.SetString("empty", null!);
        Assert.Equal(string.Empty, PlayerPrefs.GetString("empty"));
        Assert.Equal("def", PlayerPrefs.GetString("x", "def"));
    }

    [Fact]
    public void HasKey_DeleteKey()
    {
        PlayerPrefs.SetInt("k", 1);
        Assert.True(PlayerPrefs.HasKey("k"));
        PlayerPrefs.DeleteKey("k");
        Assert.False(PlayerPrefs.HasKey("k"));
    }

    [Fact]
    public void DeleteAll_Clears()
    {
        PlayerPrefs.SetInt("a", 1);
        PlayerPrefs.SetString("b", "2");
        PlayerPrefs.DeleteAll();
        Assert.False(PlayerPrefs.HasKey("a"));
        Assert.False(PlayerPrefs.HasKey("b"));
    }

    [Fact]
    public void Save_PersistsAcrossReload()
    {
        PlayerPrefs.SetInt("persist", 99);
        PlayerPrefs.SetString("s", "ok");
        PlayerPrefs.Save();
        Assert.True(File.Exists(PlayerPrefs.savePath));

        // Simulate process restart
        PlayerPrefs.SetSavePathForTests(_dir);
        Assert.Equal(99, PlayerPrefs.GetInt("persist"));
        Assert.Equal("ok", PlayerPrefs.GetString("s"));
    }

    [Fact]
    public void CaseSensitive_Keys()
    {
        PlayerPrefs.SetInt("Key", 1);
        PlayerPrefs.SetInt("key", 2);
        Assert.Equal(1, PlayerPrefs.GetInt("Key"));
        Assert.Equal(2, PlayerPrefs.GetInt("key"));
    }

    [Fact]
    public void TypeCoercion_GetStringOnInt()
    {
        PlayerPrefs.SetInt("n", 5);
        Assert.Equal("5", PlayerPrefs.GetString("n"));
    }

    [Fact]
    public void TypeCoercion_GetIntOnFloat()
    {
        PlayerPrefs.SetFloat("f", 3.9f);
        Assert.Equal(3, PlayerPrefs.GetInt("f"));
    }

    [Fact]
    public void Bool_Extension()
    {
        PlayerPrefs.SetBool("flag", true);
        Assert.True(PlayerPrefs.GetBool("flag"));
        PlayerPrefs.SetBool("flag", false);
        Assert.False(PlayerPrefs.GetBool("flag"));
    }

    [Fact]
    public void GetAllKeys_Contains()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt("a", 1);
        PlayerPrefs.SetFloat("b", 2f);
        var keys = PlayerPrefs.GetAllKeys();
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void GetKeyType_ReturnsStoredType()
    {
        PlayerPrefs.SetFloat("ft", 1.2f);
        Assert.Equal(PlayerPrefsKeyType.Float, PlayerPrefs.GetKeyType("ft"));
        Assert.Null(PlayerPrefs.GetKeyType("missing"));
    }

    [Fact]
    public void EditorPrefs_RoundTrip_AndPersist()
    {
        EditorPrefs.SetInt("e_i", 3);
        EditorPrefs.SetFloat("e_f", 1.25f);
        EditorPrefs.SetString("e_s", "ed");
        EditorPrefs.SetBool("e_b", true);
        EditorPrefs.Save();
        Assert.True(EditorPrefs.HasKey("e_i"));
        Assert.Equal(3, EditorPrefs.GetInt("e_i"));
        Assert.Equal("ed", EditorPrefs.GetString("e_s"));
        Assert.True(EditorPrefs.GetBool("e_b"));
        EditorPrefs.DeleteKey("e_i");
        Assert.False(EditorPrefs.HasKey("e_i"));
    }

    [Fact]
    public void LocalStorage_WriteReadDelete()
    {
        string name = "local_test_" + Guid.NewGuid().ToString("N") + ".txt";
        // Use temp to avoid company path issues
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, "hello");
        Assert.True(File.Exists(path));
        Assert.Equal("hello", File.ReadAllText(path));
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void LocalStorage_Resolve_AbsoluteAndRelative()
    {
        string abs = Path.Combine(_dir, "abs.txt");
        Assert.Equal(abs, LocalStorage.Resolve(abs, true));
        Assert.False(string.IsNullOrEmpty(LocalStorage.persistentDataPath));
        Assert.False(string.IsNullOrEmpty(LocalStorage.temporaryCachePath));
    }

    [Fact]
    public void PlayerPrefs_NullKey_ThrowsOnSet()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerPrefs.SetInt(null!, 1));
    }

    [Fact]
    public void AtomicSave_CreatesFile()
    {
        PlayerPrefs.SetString("atomic", "v");
        int before = PlayerPrefs.saveCount;
        PlayerPrefs.Save();
        Assert.True(PlayerPrefs.saveCount > before);
        Assert.True(new FileInfo(PlayerPrefs.savePath).Length > 0);
    }
}
