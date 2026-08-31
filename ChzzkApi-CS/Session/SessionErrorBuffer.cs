using System.Collections;

namespace ChzzkApi_CS.Session;

/// <summary>
/// Collects failures raised inside socket receive callbacks.
/// <para>
/// While <see cref="ErrorOccurred"/> has a subscriber, errors go straight to it and are not stored.
/// With no subscriber they accumulate here up to <see cref="Capacity"/> and are read back with
/// <see cref="TryPop"/>, <see cref="PopAll"/>, or by enumerating this instance. Errors recorded
/// before a subscriber attaches are not replayed to it.
/// </para>
/// </summary>
public sealed class SessionErrorBuffer : IEnumerable<SessionError>
{
    public const int DefaultCapacity = 64;

    readonly Lock gate = new();
    readonly Queue<SessionError> errors = new();

    int capacity = DefaultCapacity;
    int droppedCount;

    /// <summary>
    /// Raised on the socket receive thread the moment an error is recorded, so a slow handler
    /// delays message delivery. Exceptions thrown by a handler are discarded.
    /// </summary>
    public event Action<SessionError>? ErrorOccurred;

    /// <summary>
    /// How many errors are kept while no <see cref="ErrorOccurred"/> subscriber is attached.
    /// The oldest entries are discarded once the buffer is full, and again if this is lowered.
    /// </summary>
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
        get { lock (gate) return errors.Count; }
    }

    /// <summary>Errors discarded because the buffer was full, accumulated over the session's lifetime.</summary>
    public int DroppedCount
    {
        get { lock (gate) return droppedCount; }
    }

    internal void Add(SessionError error)
    {
        if (ErrorOccurred is Action<SessionError> handler)
        {
            // An error channel that throws has nowhere left to report to.
            try { handler(error); }
            catch { }

            return;
        }

        lock (gate)
        {
            errors.Enqueue(error);
            DiscardOverflow();
        }
    }

    /// <summary>Removes and returns the oldest buffered error.</summary>
    public bool TryPop(out SessionError error)
    {
        lock (gate)
            return errors.TryDequeue(out error);
    }

    /// <summary>Removes every buffered error and returns them oldest first.</summary>
    public List<SessionError> PopAll()
    {
        lock (gate)
        {
            var popped = new List<SessionError>(errors);
            errors.Clear();

            return popped;
        }
    }

    /// <summary>
    /// Yields buffered errors oldest first, removing each as it is yielded. Enumeration is
    /// destructive: a second pass yields nothing, and operators such as Count() consume the buffer.
    /// </summary>
    public IEnumerator<SessionError> GetEnumerator()
    {
        while (TryPop(out var error))
            yield return error;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void DiscardOverflow()
    {
        while (errors.Count > capacity)
        {
            errors.Dequeue();
            droppedCount++;
        }
    }
}
