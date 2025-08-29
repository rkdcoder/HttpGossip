namespace HttpGossip
{
    // Public: can appear on public APIs (like HttpGossipMiddleware)
    public interface ILogQueueWriter
    {
        bool TryWrite(HttpGossipRecord record);
    }
}