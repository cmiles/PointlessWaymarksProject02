using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WaterDaze.Models;

namespace WaterDaze.Services;

public interface IUsgsService
{
    Task<List<DailyFlowRecord>> FetchDailyValuesAsync(string siteCode, CancellationToken cancellationToken = default);
}