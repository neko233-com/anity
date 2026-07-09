using System;

namespace UnityEngine;

/// <summary>
/// Base class for yield instructions to use with coroutines.
/// </summary>
public class YieldInstruction { }

/// <summary>
/// Suspends the coroutine execution for the given amount of seconds.
/// </summary>
public class WaitForSeconds : YieldInstruction
{
    public float seconds { get; }

    public WaitForSeconds(float seconds)
    {
        this.seconds = seconds;
    }
}

/// <summary>
/// Suspends the coroutine execution until the end of the current frame.
/// </summary>
public class WaitForEndOfFrame : YieldInstruction { }

/// <summary>
/// Suspends the coroutine execution until the next physics update.
/// </summary>
public class WaitForFixedUpdate : YieldInstruction { }

/// <summary>
/// Yield while the specified WWW object is downloading.
/// </summary>
public class WaitForWWW : YieldInstruction { }

/// <summary>
/// Yield while a specified AsyncOperation is being processed.
/// </summary>
public class WaitForAsyncOperation : YieldInstruction
{
    public AsyncOperation asyncOperation { get; }

    public WaitForAsyncOperation(AsyncOperation asyncOperation)
    {
        this.asyncOperation = asyncOperation ?? throw new ArgumentNullException(nameof(asyncOperation));
    }
}

/// <summary>
/// Allows yielding a coroutine from a coroutine.
/// </summary>
public class Coroutine : YieldInstruction
{
    internal IntPtr ptr;
    internal object? coroutineOwner;
}

/// <summary>
/// Allows yielding a coroutine from a coroutine (managed version).
/// </summary>
public class ManagedCoroutine : YieldInstruction
{
    public object? inner { get; }
}
