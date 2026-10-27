using EarlyEagles.Application.Abstractions.Auditing;
using EarlyEagles.Application.Auditing;

namespace EarlyEagles.Tests.Unit.Auditing;

public class ListAuditEventsQueryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validator_Rejects_Limit_Outside_1_To_100(int limit)
    {
        var result = new ListAuditEventsQueryValidator().Validate(new ListAuditEventsQuery(limit, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ListAuditEventsQuery.Limit));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validator_Accepts_Limit_In_Range(int limit)
    {
        Assert.True(new ListAuditEventsQueryValidator().Validate(new ListAuditEventsQuery(limit, null)).IsValid);
    }

    [Fact]
    public void Validator_Rejects_A_Tampered_Cursor()
    {
        var result = new ListAuditEventsQueryValidator().Validate(new ListAuditEventsQuery(10, "garbage"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ListAuditEventsQuery.Cursor));
    }

    [Fact]
    public async Task Handler_Returns_A_Cursor_When_More_Rows_Exist()
    {
        var reader = new FakeReader(Rows(4));

        var page = await new ListAuditEventsQueryHandler(reader).Handle(new ListAuditEventsQuery(3, null), CancellationToken.None);

        Assert.Equal(4, reader.LastTake);
        Assert.Equal(3, page.Items.Count);
        Assert.NotNull(page.NextCursor);
        Assert.True(AuditEventCursor.TryDecode(page.NextCursor, out var cursor));
        Assert.Equal(page.Items[^1].Id, cursor!.Value.Id);
        Assert.Equal(page.Items[^1].OccurredAt, cursor.Value.OccurredAt);
    }

    [Fact]
    public async Task Handler_Returns_No_Cursor_On_The_Last_Page()
    {
        var page = await new ListAuditEventsQueryHandler(new FakeReader(Rows(3))).Handle(new ListAuditEventsQuery(3, null), CancellationToken.None);

        Assert.Equal(3, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Handler_Passes_The_Decoded_Cursor_To_The_Reader()
    {
        var reader = new FakeReader([]);
        var cursor = new AuditEventCursor(T0, Guid.NewGuid());

        await new ListAuditEventsQueryHandler(reader).Handle(new ListAuditEventsQuery(5, cursor.Encode()), CancellationToken.None);

        Assert.Equal(cursor, reader.LastAfter);
    }

    private static AuditEventSummary[] Rows(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new AuditEventSummary(Guid.NewGuid(), $"Action{i}", true, null, null, T0.AddMinutes(-i)))
            .ToArray();

    private sealed class FakeReader : IAuditLogReader
    {
        private readonly IReadOnlyList<AuditEventSummary> rows;

        public FakeReader(IReadOnlyList<AuditEventSummary> rows) => this.rows = rows;

        public int LastTake { get; private set; }

        public AuditEventCursor? LastAfter { get; private set; }

        public Task<IReadOnlyList<AuditEventSummary>> ListAsync(AuditEventCursor? after, int take, CancellationToken cancellationToken)
        {
            LastAfter = after;
            LastTake = take;
            return Task.FromResult<IReadOnlyList<AuditEventSummary>>(rows.Take(take).ToArray());
        }
    }
}
