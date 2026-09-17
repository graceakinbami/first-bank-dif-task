namespace NovaPay.Tests;

/// <summary>A settable clock, so a test can move "now" across a WAT day boundary on demand.</summary>
public class FakeTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = initialUtcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
