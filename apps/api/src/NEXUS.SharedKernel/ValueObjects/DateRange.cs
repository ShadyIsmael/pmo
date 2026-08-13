namespace NEXUS.SharedKernel.ValueObjects;

public class DateRange : BaseValueObject
{
    public DateTime Start { get; }
    public DateTime End { get; }

    public DateRange(DateTime start, DateTime end)
    {
        if (start >= end)
            throw new ArgumentException("Start date must be before end date");
        Start = start;
        End = end;
    }

    public TimeSpan Duration => End - Start;
    public int Days => (int)Duration.TotalDays;

    public bool Overlaps(DateRange other) => Start < other.End && End > other.Start;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
