// Abstractions.cs

namespace BusLibrary02.Core;

public abstract record MessageBase(string Key, string? SenderKey = null) : IMessage
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
