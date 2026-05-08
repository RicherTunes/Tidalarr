using Tidalarr.Core.Constants;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalConstants.
/// Source: src/Tidalarr/Core/Constants/TidalConstants.cs
/// </summary>
public class TidalConstantsCovTests
{
    #region OAuth Client Credentials - Lines 8-9

    [Fact]
    public void ClientIdPkce_HasExpectedValue()
    {
        // Source line 8: public const string CLIENT_ID_PKCE = "6BDSRdpK9hqEBTgU";
        Assert.Equal("6BDSRdpK9hqEBTgU", TidalConstants.CLIENT_ID_PKCE);
    }

    [Fact]
    public void ClientSecretPkce_HasExpectedValue()
    {
        // Source line 9: public const string CLIENT_SECRET_PKCE = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=";
        Assert.Equal("xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=", TidalConstants.CLIENT_SECRET_PKCE);
    }

    #endregion

    #region OAuth Parameters - Lines 11-12

    [Fact]
    public void ClientId_HasExpectedValue()
    {
        // Source line 11: public const string CLIENT_ID = "zU4XHVVkc2tDPo4t";
        Assert.Equal("zU4XHVVkc2tDPo4t", TidalConstants.CLIENT_ID);
    }

    [Fact]
    public void RedirectUri_HasExpectedValue()
    {
        // Source line 12: public const string REDIRECT_URI = "https://tidal.com/android/login/auth";
        Assert.Equal("https://tidal.com/android/login/auth", TidalConstants.REDIRECT_URI);
    }

    #endregion

    #region API Endpoints - Lines 15-17

    [Fact]
    public void ApiV1Base_HasExpectedValue()
    {
        // Source line 15: public const string API_V1_BASE = "https://api.tidal.com/v1/";
        Assert.Equal("https://api.tidal.com/v1/", TidalConstants.API_V1_BASE);
    }

    [Fact]
    public void AuthBase_HasExpectedValue()
    {
        // Source line 16: public const string AUTH_BASE = "https://auth.tidal.com/v1/oauth2/token";
        Assert.Equal("https://auth.tidal.com/v1/oauth2/token", TidalConstants.AUTH_BASE);
    }

    [Fact]
    public void LoginBase_HasExpectedValue()
    {
        // Source line 17: public const string LOGIN_BASE = "https://login.tidal.com/authorize";
        Assert.Equal("https://login.tidal.com/authorize", TidalConstants.LOGIN_BASE);
    }

    #endregion

    #region OAuth Parameters - Lines 22-24

    [Fact]
    public void OAuthScope_HasExpectedValue()
    {
        // Source line 22: public const string OAUTH_SCOPE = "r_usr w_usr w_sub offline_access";
        Assert.Equal("r_usr w_usr w_sub offline_access", TidalConstants.OAUTH_SCOPE);
    }

    [Fact]
    public void OAuthScope_ContainsOfflineAccess()
    {
        // Source line 22: offline_access for refresh tokens
        Assert.Contains("offline_access", TidalConstants.OAUTH_SCOPE);
    }

    [Fact]
    public void OAuthScope_ContainsAllRequiredScopes()
    {
        // Source line 22: Space-delimited scopes per OAuth2 spec
        var scopes = TidalConstants.OAUTH_SCOPE.Split(' ');
        Assert.Equal(4, scopes.Length);
        Assert.Contains("r_usr", scopes);
        Assert.Contains("w_usr", scopes);
        Assert.Contains("w_sub", scopes);
        Assert.Contains("offline_access", scopes);
    }

    [Fact]
    public void AppMode_HasExpectedValue()
    {
        // Source line 23: public const string APP_MODE = "android";
        Assert.Equal("android", TidalConstants.APP_MODE);
    }

    [Fact]
    public void Language_HasExpectedValue()
    {
        // Source line 24: public const string LANGUAGE = "EN";
        Assert.Equal("EN", TidalConstants.LANGUAGE);
    }

    #endregion

    #region Quality Mappings - Lines 27-33

    [Fact]
    public void QualityParameters_ContainsLowQuality()
    {
        // Source line 29: [TidalQuality.Low] = "LOW"
        Assert.Equal("LOW", TidalConstants.QualityParameters[TidalQuality.Low]);
    }

    [Fact]
    public void QualityParameters_ContainsHighQuality()
    {
        // Source line 30: [TidalQuality.High] = "HIGH"
        Assert.Equal("HIGH", TidalConstants.QualityParameters[TidalQuality.High]);
    }

    [Fact]
    public void QualityParameters_ContainsLosslessQuality()
    {
        // Source line 31: [TidalQuality.Lossless] = "LOSSLESS"
        Assert.Equal("LOSSLESS", TidalConstants.QualityParameters[TidalQuality.Lossless]);
    }

    [Fact]
    public void QualityParameters_ContainsHiResQuality()
    {
        // Source line 32: [TidalQuality.HiRes] = "HI_RES_LOSSLESS"
        Assert.Equal("HI_RES_LOSSLESS", TidalConstants.QualityParameters[TidalQuality.HiRes]);
    }

    [Fact]
    public void QualityParameters_HasExactlyFourEntries()
    {
        // Source lines 27-33: Dictionary with 4 quality mappings
        Assert.Equal(4, TidalConstants.QualityParameters.Count);
    }

    [Fact]
    public void QualityParameters_ContainsAllTidalQualityValues()
    {
        // Source lines 27-33: All enum values should have mappings
        Assert.True(TidalConstants.QualityParameters.ContainsKey(TidalQuality.Low));
        Assert.True(TidalConstants.QualityParameters.ContainsKey(TidalQuality.High));
        Assert.True(TidalConstants.QualityParameters.ContainsKey(TidalQuality.Lossless));
        Assert.True(TidalConstants.QualityParameters.ContainsKey(TidalQuality.HiRes));
    }

    [Fact]
    public void QualityParameters_ValuesAreUpperCase()
    {
        // Source lines 29-32: API expects uppercase quality strings
        foreach (var kvp in TidalConstants.QualityParameters)
        {
            Assert.Equal(kvp.Value, kvp.Value.ToUpperInvariant());
        }
    }

    #endregion

    #region API Limits - Lines 36-38

    [Fact]
    public void DefaultSearchLimit_HasExpectedValue()
    {
        // Source line 36: public const int DEFAULT_SEARCH_LIMIT = 100;
        Assert.Equal(100, TidalConstants.DEFAULT_SEARCH_LIMIT);
    }

    [Fact]
    public void MaxSearchLimit_HasExpectedValue()
    {
        // Source line 37: public const int MAX_SEARCH_LIMIT = 1000;
        Assert.Equal(1000, TidalConstants.MAX_SEARCH_LIMIT);
    }

    [Fact]
    public void DefaultItemLimit_HasExpectedValue()
    {
        // Source line 38: public const int DEFAULT_ITEM_LIMIT = 1000;
        Assert.Equal(1000, TidalConstants.DEFAULT_ITEM_LIMIT);
    }

    [Fact]
    public void MaxSearchLimit_IsGreaterThanDefaultSearchLimit()
    {
        // Source lines 36-37: MAX should exceed DEFAULT
        Assert.True(TidalConstants.MAX_SEARCH_LIMIT > TidalConstants.DEFAULT_SEARCH_LIMIT);
    }

    [Fact]
    public void DefaultItemLimit_EqualsMaxSearchLimit()
    {
        // Source lines 37-38: Both are 1000
        Assert.Equal(TidalConstants.MAX_SEARCH_LIMIT, TidalConstants.DEFAULT_ITEM_LIMIT);
    }

    #endregion

    #region URL Format Validation

    [Fact]
    public void ApiV1Base_EndsWithSlash()
    {
        // Source line 15: Trailing slash for proper URL combining
        Assert.EndsWith("/", TidalConstants.API_V1_BASE);
    }

    [Fact]
    public void RedirectUri_IsValidHttpsUrl()
    {
        // Source line 12: Must be valid URL for OAuth redirect
        Assert.StartsWith("https://", TidalConstants.REDIRECT_URI);
    }

    [Fact]
    public void AuthBase_IsValidHttpsUrl()
    {
        // Source line 16: Must be valid URL for token endpoint
        Assert.StartsWith("https://", TidalConstants.AUTH_BASE);
    }

    [Fact]
    public void LoginBase_IsValidHttpsUrl()
    {
        // Source line 17: Must be valid URL for login page
        Assert.StartsWith("https://", TidalConstants.LOGIN_BASE);
    }

    [Fact]
    public void ApiV1Base_IsValidHttpsUrl()
    {
        // Source line 15: Must be valid URL for API base
        Assert.StartsWith("https://", TidalConstants.API_V1_BASE);
    }

    #endregion
}
