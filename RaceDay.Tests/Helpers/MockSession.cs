using Microsoft.AspNetCore.Http;
using System.Text;

namespace RaceDay.Tests.Helpers;

/// <summary>
/// A simple in-memory ISession implementation for unit testing.
/// Allows controllers to be tested without a real HTTP request pipeline.
/// </summary>
public class MockSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public bool IsAvailable => true;
    public string Id => "test-session-id";
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);

    // Convenience methods to set/get string values (mirrors ISessionExtensions)
    public void SetString(string key, string value)
        => _store[key] = Encoding.UTF8.GetBytes(value);

    public string? GetString(string key)
        => _store.TryGetValue(key, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
}
