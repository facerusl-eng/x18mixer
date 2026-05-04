using System.Collections.Concurrent;
using WpfMixer.Core.Interfaces;

namespace WpfMixer.Core.Services;

public sealed class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        var list = _handlers.GetOrAdd(typeof(TEvent), _ => []);
        lock (list) list.Add(handler);
        return new Subscription(() =>
        {
            lock (list) list.Remove(handler);
        });
    }

    public void Publish<TEvent>(TEvent eventData)
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;
        Delegate[] snapshot;
        lock (list) snapshot = [.. list];

        foreach (var h in snapshot)
        {
            if (h is Action<TEvent> typed)
                typed(eventData);
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
