using System.IO;
using System.Threading.Tasks;
using PwTrackTrimmer.Models;

namespace PwTrackTrimmer.Services
{
    public interface ITrackFileService
    {
        Task<TrackDocument> LoadAsync(Stream stream, string fileName);
        Task SaveAsync(TrackDocument document, Stream outputStream);
    }
}
