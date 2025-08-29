using System.Threading.Channels;

namespace HttpGossip.Internal
{
    internal sealed class LogQueue : ILogQueueWriter
    {
        private readonly Channel<HttpGossipRecord> _channel;

        public LogQueue(int capacity)
        {
            var opts = new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite // never block requests
            };
            _channel = Channel.CreateBounded<HttpGossipRecord>(opts);
        }

        public bool TryWrite(HttpGossipRecord record) => _channel.Writer.TryWrite(record);

        public IAsyncEnumerable<HttpGossipRecord> ReadAllAsync(CancellationToken ct) =>
            _channel.Reader.ReadAllAsync(ct);
    }
}
