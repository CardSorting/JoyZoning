using System.Threading.Channels;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Serializes Hermes kanban status PATCHes per task to avoid interleaved writers.</summary>
public sealed class KanbanStatusOutbox
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

    public ChannelReader<Guid> Reader => _queue.Reader;

    public ValueTask EnqueueAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        _queue.Writer.WriteAsync(taskId, cancellationToken);
}
