using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;
using Xunit;

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
        var mapper = new TidalModelMapper();
        var streamingQ = mapper.ToStreamingQuality(q);
        var back = mapper.FromStreamingQuality(streamingQ);
        Assert.Equal(q, back);
    }
}



