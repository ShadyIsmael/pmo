namespace NEXUS.SharedKernel.ValueObjects;

public class Period : BaseValueObject
{
    public int Year { get; }
    public int Month { get; }
    public int Quarter => (Month - 1) / 3 + 1;

    public Period(int year, int month)
    {
        if (month < 1 || month > 12)
            throw new ArgumentException("Month must be between 1 and 12");
        Year = year;
        Month = month;
    }

    public static Period Current => new(DateTime.UtcNow.Year, DateTime.UtcNow.Month);

    public DateTime StartDate => new(Year, Month, 1);
    public DateTime EndDate => StartDate.AddMonths(1).AddDays(-1);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Year;
        yield return Month;
    }
}
