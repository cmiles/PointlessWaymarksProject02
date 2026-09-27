using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WaterDaze.Models;

namespace WaterDaze.Services;

public interface IUsgsService
{
    Task<List<DailyFlowRecord>> FetchDailyValuesAsync(string siteCode, CancellationToken cancellationToken = default);
    Task<List<DailyFlowRecord>> FetchDailyValuesAsync(string siteCode, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default);
    Task<List<DailyFlowRecord>> LoadFromLocalFileAsync(string filePath, CancellationToken cancellationToken = default);
}