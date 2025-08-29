namespace HttpGossip.Internal
{
    internal interface ILogRepository
    {
        Task InsertAsync(HttpGossipRecord record, CancellationToken ct);
    }
}
