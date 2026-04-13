using Servercyde.Monitoring.Core.Database;

namespace Servercyde.Monitoring.Core;

public interface IMonitor
{
    Task<DatabaseSummary[]> GetSummaries(
        string profile, 
        CancellationToken cancellationToken);
}


