using System;
using System.IO;
using Microsoft.IO;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Robust.Shared.GameObjects;

/// <summary>
/// Generic memory manager for engine use.
/// </summary>
internal sealed class RobustMemoryManager
{
    // Let's be real this is a bandaid for pooling bullshit at an engine level and I don't know what
    // good memory management looks like for PVS or the RobustSerializer.

    private static readonly RecyclableMemoryStreamManager MemStreamManager = new(new RecyclableMemoryStreamManager.Options
    {
        ThrowExceptionOnToArray = true,
    });

    [Dependency] private readonly ILogManager _logManager = default!;

    public RobustMemoryManager()
    {
        MemStreamManager.StreamDoubleDisposed += (sender, args) =>
            throw new InvalidOperationException("Found double disposed stream.");

        MemStreamManager.StreamFinalized += (sender, args) =>
        {
            // This event runs on the GC finalizer thread. Throwing here terminates the process.
            var message = $"Pooled stream finalized without being disposed (id: {args.Id}, tag: {args.Tag}). {args.AllocationStack}";
            try
            {
                // Use the injected manager: the finalizer thread has no thread-local IoC context.
                _logManager.GetSawmill("memory").Error(message);
            }
            catch
            {
                // Logging may already be shut down. A diagnostic must never make finalization fatal.
                try { Console.Error.WriteLine(message); }
                catch { /* No usable logging destination remains. */ }
            }
        };

        MemStreamManager.StreamOverCapacity += (sender, args) =>
            throw new InvalidOperationException("Stream over memory capacity");
    }

    public static MemoryStream GetMemoryStream()
    {
        var stream = MemStreamManager.GetStream("RobustMemoryManager");
        DebugTools.Assert(stream.Position == 0);
        return stream;
    }

    public static MemoryStream GetMemoryStream(int length)
    {
        var stream = MemStreamManager.GetStream("RobustMemoryManager", length);
        DebugTools.Assert(stream.Position == 0);
        return stream;
    }
}
