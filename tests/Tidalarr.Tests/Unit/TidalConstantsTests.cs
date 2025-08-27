using Tidalarr.Core.Constants;
using Tidalarr.Core.Models;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// 100% Coverage: TidalConstants static class testing
/// </summary>
public class TidalConstantsTests
{
    [Fact]
    public void TidalConstants_ClientCredentials_AreNotEmpty()
    {
        Assert.NotEmpty(TidalConstants.CLIENT_ID_PKCE);
        Assert.NotEmpty(TidalConstants.CLIENT_SECRET_PKCE);
        Assert.NotEmpty(TidalConstants.REDIRECT_URI);
        
        Assert.Matches(@"^[A-Za-z0-9]+$", TidalConstants.CLIENT_ID_PKCE);
        Assert.Contains("=", TidalConstants.CLIENT_SECRET_PKCE);
        Assert.StartsWith("https://", TidalConstants.REDIRECT_URI);
    }
    
    [Fact] 
    public void TidalConstants_QualityParameters_ContainAllQualities()
    {
        var allQualities = Enum.GetValues<TidalQuality>();
        
        foreach (var quality in allQualities)
        {
            Assert.True(TidalConstants.QualityParameters.ContainsKey(quality));
            Assert.NotEmpty(TidalConstants.QualityParameters[quality]);
        }
        
        Assert.Equal("LOW", TidalConstants.QualityParameters[TidalQuality.Low]);
        Assert.Equal("HIGH", TidalConstants.QualityParameters[TidalQuality.High]);
        Assert.Equal("LOSSLESS", TidalConstants.QualityParameters[TidalQuality.Lossless]);
        Assert.Equal("HI_RES_LOSSLESS", TidalConstants.QualityParameters[TidalQuality.HiRes]);
    }
    
    [Fact]
    public void TidalConstants_MasterKey_IsValidBase64()
    {
        Assert.NotEmpty(TidalConstants.MASTER_KEY);
        var decoded = Convert.FromBase64String(TidalConstants.MASTER_KEY);
        Assert.NotEmpty(decoded);
        Assert.True(decoded.Length >= 32);
    }
}