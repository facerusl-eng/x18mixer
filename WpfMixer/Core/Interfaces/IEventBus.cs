namespace WpfMixer.Core.Interfaces;

public interface IEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);
    void Publish<TEvent>(TEvent eventData);
}
