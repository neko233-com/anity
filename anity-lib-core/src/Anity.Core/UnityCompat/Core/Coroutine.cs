using System.Threading.Tasks;

namespace UnityEngine;

public class Coroutine
{
  private bool _stopped;

  public Coroutine(Task task, object? routine = null, string? methodName = null)
  {
    Task = task;
    Routine = routine;
    MethodName = methodName;
  }

  public Task Task { get; }
  public object? Routine { get; }
  public string? MethodName { get; }

  public bool IsRunning => Task is not null && !Task.IsCompleted && !_stopped;

  internal void Cancel()
  {
    _stopped = true;
  }
}
