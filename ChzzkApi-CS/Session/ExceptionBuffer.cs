using System.Collections;

namespace ChzzkApi.Session;

public readonly record struct ExceptionInfo
{
    public const string HandlingContextDataKey = "ChzzkSession.HandlingContext";

    public readonly DateTimeOffset TimeUtc;
    public readonly Exception Exception;

    public ExceptionInfo(DateTimeOffset timeUtc, string source, Exception exception)
    {
        TimeUtc = timeUtc;
        Exception = exception;
        exception.Data[HandlingContextDataKey] = source;
    }
};

public sealed class ExceptionBuffer : IEnumerable<ExceptionInfo>
{
    public const int DefaultCapacity = 64;

    readonly Lock gate = new();
    readonly Queue<ExceptionInfo> exceptions = new();

    int capacity = DefaultCapacity;
    int droppedCount;

    private volatile Action<ExceptionInfo>? exceptionOccurred;

    public void SetExceptionOccurred(Action<ExceptionInfo>? handler)
    {
        exceptionOccurred = handler;
    }

    public int Capacity
    {
        get { lock (gate) return capacity; }
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);

            lock (gate)
            {
                capacity = value;
                DiscardOverflow();
            }
        }
    }

    public int Count
    {
        get { lock (gate) return exceptions.Count; }
    }

    public int DroppedCount
    {
        get { lock (gate) return droppedCount; }
    }

    internal void Add(ExceptionInfo exceptionInfo)
    {
        var handler = exceptionOccurred;

        if (handler is not null)
        {
            handler(exceptionInfo);
            return;
        }

        lock (gate)
        {
            exceptions.Enqueue(exceptionInfo);
            DiscardOverflow();
        }
    }

    public bool TryPop(out ExceptionInfo exceptionInfo)
    {
        lock (gate)
            return exceptions.TryDequeue(out exceptionInfo);
    }

    public List<ExceptionInfo> PopAll()
    {
        lock (gate)
        {
            var popped = new List<ExceptionInfo>(exceptions);
            exceptions.Clear();

            return popped;
        }
    }

    public IEnumerator<ExceptionInfo> GetEnumerator()
    {
        while (TryPop(out var exceptionInfo))
            yield return exceptionInfo;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void DiscardOverflow()
    {
        while (exceptions.Count > capacity)
        {
            exceptions.Dequeue();
            droppedCount++;
        }
    }
}
