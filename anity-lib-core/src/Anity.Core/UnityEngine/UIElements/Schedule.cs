using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

// Scheduler interfaces and implementation
public interface ISchedule
{
  IScheduledItem Execute(Action action, long delayMs = 0);
  IScheduledItem Execute(Action action, TimeSpan delay);
  IScheduledItem Execute(Action action, int framesDelay);
  IScheduledItem ExecuteUntil(Func<bool> predicateUntilDone, Action performAction, Func<long> delayMsCallback = null, long initialDelayMs = 0);
  IScheduledItem RepeatUntil(Func<bool> predicateUntilDone, Action performAction, Func<long> delayMsCallback = null, long initialDelayMs = 0);
  void Pause(IScheduledItem item);
  void Resume(IScheduledItem item);
  void Unschedule(IScheduledItem item);
}

public interface IScheduledItem
{
  bool isActive { get; }
  void Pause();
  void Resume();
}

public class UIElementsScheduler : ISchedule
{
  private readonly List<ScheduledItem> _scheduledItems = new();

  public IScheduledItem Execute(Action action, long delayMs = 0)
  {
    var item = new ScheduledItem(action, delayMs);
    _scheduledItems.Add(item);
    return item;
  }

  public IScheduledItem Execute(Action action, TimeSpan delay)
  {
    return Execute(action, (long)delay.TotalMilliseconds);
  }

  public IScheduledItem Execute(Action action, int framesDelay)
  {
    var item = new ScheduledItem(action, 0) { framesDelay = framesDelay };
    _scheduledItems.Add(item);
    return item;
  }

  public IScheduledItem ExecuteUntil(Func<bool> predicateUntilDone, Action performAction, Func<long> delayMsCallback = null, long initialDelayMs = 0)
  {
    var item = new ScheduledItem(performAction, initialDelayMs)
    {
      predicateUntilDone = predicateUntilDone,
      delayMsCallback = delayMsCallback
    };
    _scheduledItems.Add(item);
    return item;
  }

  public IScheduledItem RepeatUntil(Func<bool> predicateUntilDone, Action performAction, Func<long> delayMsCallback = null, long initialDelayMs = 0)
  {
    var item = new ScheduledItem(performAction, initialDelayMs)
    {
      predicateUntilDone = predicateUntilDone,
      delayMsCallback = delayMsCallback,
      repeating = true
    };
    _scheduledItems.Add(item);
    return item;
  }

  public void Pause(IScheduledItem item)
  {
    foreach (var scheduled in _scheduledItems)
    {
      if (ReferenceEquals(scheduled, item))
      {
        scheduled.Pause();
        return;
      }
    }
  }

  public void Resume(IScheduledItem item)
  {
    foreach (var scheduled in _scheduledItems)
    {
      if (ReferenceEquals(scheduled, item))
      {
        scheduled.Resume();
        return;
      }
    }
  }

  public void Unschedule(IScheduledItem item)
  {
    _scheduledItems.RemoveAll(s => ReferenceEquals(s, item));
  }
}

internal class ScheduledItem : IScheduledItem
{
  public Action action;
  public long delayMs;
  public int framesDelay;
  public Func<bool> predicateUntilDone;
  public Func<long> delayMsCallback;
  public bool repeating;
  public bool isActive { get; private set; } = true;
  private int _framesRemaining;

  public ScheduledItem(Action action, long delayMs)
  {
    this.action = action;
    this.delayMs = delayMs;
    _framesRemaining = framesDelay;
  }

  public void Pause() => isActive = false;
  public void Resume() => isActive = true;
}
