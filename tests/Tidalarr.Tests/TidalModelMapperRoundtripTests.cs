using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

public class TidalModelMapperRoundtripTests
{
    [Theory]
    [InlineData(TidalQuality.Low)]
    [InlineData(TidalQuality.High)]
    [InlineData(TidalQuality.Lossless)]
    [InlineData(TidalQuality.HiRes)]
    public void Quality_Roundtrip_MappingMaintainsTier(TidalQuality q)
    {
        TidalModelMapper mapper = new TidalModelMapper();
        Lidarr.Plugin.Abstractions.Models.StreamingQuality streamingQ = mapper.ToStreamingQuality(q);
        TidalQuality back = mapper.FromStreamingQuality(streamingQ);
        Assert.Equal(q, back);
    }
}




