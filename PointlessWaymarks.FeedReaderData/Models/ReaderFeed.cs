using Metalama.Patterns.Observability;

namespace PointlessWaymarks.FeedReaderData.Models;

[Observable]
public partial class ReaderFeed
{
    public int? AutoMarkReadAfterDays { get; set; }
    public int? AutoMarkReadMoreThanItems { get; set; }
    public string BasicAuthPassword { get; set; } = string.Empty;
    public string BasicAuthUsername { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime? FeedLastUpdatedDate { get; set; }
    public int Id { get; set; }
    public string LastResponse { get; set; } = string.Empty;
    public DateTime? LastSuccessfulUpdate { get; set; }
    public DateTime? LastFailedUpdate { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public Guid PersistentId { get; set; } = Guid.NewGuid();
    public string Tags { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool UseBasicAuth { get; set; }
}