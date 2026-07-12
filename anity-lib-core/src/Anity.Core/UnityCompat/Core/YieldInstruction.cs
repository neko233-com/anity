using System;
using System.Collections;

namespace UnityEngine;

public class YieldInstruction { }

public abstract class CustomYieldInstruction : YieldInstruction, IEnumerator
{
    public abstract bool keepWaiting { get; }

    public object Current => null;

    public bool MoveNext()
    {
        return keepWaiting;
    }

    public void Reset() { }
}

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

public class WaitForSecondsRealtime : YieldInstruction
{
    public float waitTime { get; }

    public WaitForSecondsRealtime(float time)
    {
        waitTime = time;
    }
}

public class ManagedCoroutine : YieldInstruction
{
    public object? inner { get; }
}
