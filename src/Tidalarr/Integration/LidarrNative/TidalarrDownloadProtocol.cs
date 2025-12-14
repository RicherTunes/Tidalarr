using NzbDrone.Core.Indexers;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Download protocol marker class for Tidalarr streaming service.
/// Required by Lidarr plugins branch for protocol identification.
/// </summary>
public class TidalarrDownloadProtocol : IDownloadProtocol
{
}
