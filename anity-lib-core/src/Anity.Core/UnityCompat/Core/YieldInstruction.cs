using System;

namespace UnityEngine;

public class YieldInstruction { }

public abstract class CustomYieldInstruction : YieldInstruction
{
    public abstract bool keepWaiting { get; }
}

public class WaitForSeconds : YieldInstruction
{
    public float seconds { get; }

    public WaitForSeconds(float seconds)
    {
        this.seconds = seconds;
    }
}

public class WaitForEndOfFrame : YieldInstruction { }

public class WaitForFixedUpdate : YieldInstruction { }

public class WaitForWWW : YieldInstruction { }

public class WaitForAsyncOperation : CustomYieldInstruction
{
    public AsyncOperation asyncOperation { get; }

    public WaitForAsyncOperation(AsyncOperation asyncOperation)
    {
        this.asyncOperation = asyncOperation ?? throw new ArgumentNullException(nameof(asyncOperation));
    }

    public override bool keepWaiting => !asyncOperation.isDone;
}

public class ManagedCoroutine : YieldInstruction
{
    public object? inner { get; }
}
