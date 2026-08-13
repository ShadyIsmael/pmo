namespace NEXUS.Modules.Reporting.Application;

public interface IReportDataProvider
{
    Task<object> GetDataAsync(string query, IDictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
}
