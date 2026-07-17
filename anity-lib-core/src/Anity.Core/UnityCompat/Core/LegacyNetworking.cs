using System;
using System.ComponentModel;

namespace UnityEngine;

internal static class LegacyNetworkingRemoved
{
  internal const string Message = "The legacy networking system has been removed in Unity 2018.2. Use Unity Multiplayer and NetworkIdentity instead.";

  internal static NotSupportedException Exception() => new(Message);
}

[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete(LegacyNetworkingRemoved.Message, true)]
public enum RPCMode
{
}

[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete(LegacyNetworkingRemoved.Message, true)]
public enum NetworkStateSynchronization
{
}

[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete(LegacyNetworkingRemoved.Message, true)]
public struct NetworkPlayer
{
  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public NetworkPlayer(string ip, int port)
    => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public string ipAddress => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public int port => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public string guid => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public string externalIP => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public int externalPort => throw LegacyNetworkingRemoved.Exception();
}

[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete(LegacyNetworkingRemoved.Message, true)]
public struct NetworkViewID
{
  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public static NetworkViewID unassigned => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public bool isMine => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public NetworkPlayer owner => throw LegacyNetworkingRemoved.Exception();
}

[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete(LegacyNetworkingRemoved.Message, true)]
[NativeClass(null)]
public class NetworkView : Behaviour
{
  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public void RPC(string name, NetworkPlayer target, params object[] args)
    => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public void RPC(string name, RPCMode mode, params object[] args)
    => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public Component observed
  {
    get => throw LegacyNetworkingRemoved.Exception();
    set => throw LegacyNetworkingRemoved.Exception();
  }

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public NetworkStateSynchronization stateSynchronization
  {
    get => throw LegacyNetworkingRemoved.Exception();
    set => throw LegacyNetworkingRemoved.Exception();
  }

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public NetworkViewID viewID
  {
    get => throw LegacyNetworkingRemoved.Exception();
    set => throw LegacyNetworkingRemoved.Exception();
  }

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public int group
  {
    get => throw LegacyNetworkingRemoved.Exception();
    set => throw LegacyNetworkingRemoved.Exception();
  }

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public bool isMine => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public NetworkPlayer owner => throw LegacyNetworkingRemoved.Exception();
}

[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete(LegacyNetworkingRemoved.Message, true)]
public struct NetworkMessageInfo
{
  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public double timestamp => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public NetworkPlayer sender => throw LegacyNetworkingRemoved.Exception();

  [EditorBrowsable(EditorBrowsableState.Never)]
  [Obsolete(LegacyNetworkingRemoved.Message, true)]
  public NetworkView networkView => throw LegacyNetworkingRemoved.Exception();
}
