using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HistoricTempests.Models;

namespace HistoricTempests.Services;

public interface IWeatherReportService
{
    Task<OverviewReport?> GetOverviewAsync(CancellationToken ct = default);
    Task<MonthlyYearlyComparisonsReport?> GetMonthlyYearlyComparisonsAsync(CancellationToken ct = default);
    Task<RecentDaysReport?> GetRecentDaysAsync(CancellationToken ct = default);
    Task<UpcomingWeekReport?> GetUpcomingWeekAsync(CancellationToken ct = default);
    Task<AllDailySummariesReport?> GetAllDailySummariesAsync(CancellationToken ct = default);
}
