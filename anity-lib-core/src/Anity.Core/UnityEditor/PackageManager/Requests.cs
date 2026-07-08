using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEditor.PackageManager.Requests;

public class Request
{
  public event Action<Request>? completed;

  public bool IsCompleted { get; set; }
  public float progress { get; set; }
  public float progressInfo { get; set; }
  public string operationError { get; set; } = string.Empty;
  public Error Error { get; set; } = new Error(0, string.Empty);
  public StatusCode Status { get; set; } = StatusCode.InProgress;

  public IEnumerator GetEnumerator()
  {
    yield return null;
  }

  internal void InvokeCompleted()
  {
    completed?.Invoke(this);
  }
}

public sealed class Error
{
  public Error(int errorCode, string message)
  {
    this.errorCode = errorCode;
    this.message = message;
  }

  public int errorCode { get; }
  public string message { get; }
}

public enum StatusCode
{
  Success,
  Failure,
  InProgress
}

public sealed class PackageCollection : IReadOnlyList<PackageInfo>
{
  private readonly PackageInfo[] _items;

  public PackageCollection(PackageInfo[] items)
  {
    _items = items ?? Array.Empty<PackageInfo>();
  }

  public int Count => _items.Length;
  public PackageInfo this[int index] => _items[index];

  public IEnumerator<PackageInfo> GetEnumerator() => ((IEnumerable<PackageInfo>)_items).GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public PackageInfo? Find(string packageName)
  {
    foreach (var item in _items)
    {
      if (item.name == packageName)
      {
        return item;
      }
    }

    return null;
  }

  public bool Contains(string packageName)
  {
    return Find(packageName) is not null;
  }
}

public sealed class ListRequest : Request
{
  public PackageCollection Result { get; set; } = new PackageCollection(Array.Empty<PackageInfo>());
}

public sealed class SearchRequest : Request
{
  public PackageCollection Result { get; set; } = new PackageCollection(Array.Empty<PackageInfo>());
  public string SearchText { get; set; } = string.Empty;
}

public sealed class AddRequest : Request
{
  public PackageInfo Result { get; set; } = new PackageInfo(string.Empty, "0.0.0", PackageSource.Unknown, string.Empty);
}

public sealed class EmbedRequest : Request
{
  public PackageInfo Result { get; set; } = new PackageInfo(string.Empty, "0.0.0", PackageSource.Unknown, string.Empty);
}

public sealed class UpdateRequest : Request
{
  public PackageInfo Result { get; set; } = new PackageInfo(string.Empty, "0.0.0", PackageSource.Unknown, string.Empty);
}

public sealed class EmbedAndRemoveRequest : Request
{
  public PackageInfo Result { get; set; } = new PackageInfo(string.Empty, "0.0.0", PackageSource.Unknown, string.Empty);
}

public sealed class RemoveRequest : Request
{
  public string packageIdOrPath { get; set; } = string.Empty;
}

public sealed class UnityProjectRequest : Request
{
}

public sealed class ResolveRequest : Request
{
  public PackageCollection Result { get; set; } = new PackageCollection(Array.Empty<PackageInfo>());
}

public sealed class PackRequest : Request
{
  public string Result { get; set; } = string.Empty;
}

public sealed class DisposeRequest : Request
{
}

public sealed class EmbedAndAddRequest : Request
{
  public PackageInfo Result { get; set; } = new PackageInfo(string.Empty, "0.0.0", PackageSource.Unknown, string.Empty);
}

public sealed class DependencyResolveRequest : Request
{
  public PackageCollection Result { get; set; } = new PackageCollection(Array.Empty<PackageInfo>());
}
