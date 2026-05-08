using Tidalarr.Core.Constants;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Coverage tests for TidalConstants constants not covered by TidalConstantsTests.
/// Target: src/Tidalarr/Core/Constants/TidalConstants.cs
/// </summary>
public class TidalConstantsCovTests
{
    /// <summary>
    /// Covers CLIENT_ID constant (line 11).
    /// Proof: grep -n "CLIENT_ID" src/Tidalarr/Core/Constants/TidalConstants.cs
    ///   8:    public const string CLIENT_ID_PKCE = "6BDSRdpK9hqEBTgU";
    ///   11:    public const string CLIENT_ID = "zU4XHVVkc2tDPo4t";
    /// </summary>
    [Fact]
    public void TidalConstants_ClientId_HasExpectedValue()
    {
        // Line 11: public const string CLIENT_ID = "zU4XHVVkc2tDPo4t";
        Assert.Equal("zU4XHVVkc2tDPo4t", TidalConstants.CLIENT_ID);
    }

    /// <summary>
    /// Covers OAUTH_SCOPE constant (line 22).
    /// Proof: grep -n "OAUTH_SCOPE" src/Tidalarr/Core/Constants/TidalConstants.cs
    ///   22:    public const string OAUTH_SCOPE = "r_usr w_usr w_sub offline_access";
    /// </summary>
    [Fact]
    public void TidalConstants_OAuthScope_ContainsRequiredScopes()
    {
        // Line 22: public const string OAUTH_SCOPE = "r_usr w_usr w_sub offline_access";
        Assert.Equal("r_usr w_usr w_sub offline_access", TidalConstants.OAUTH_SCOPE);
        Assert.Contains("r_usr", TidalConstants.OAUTH_SCOPE);
        Assert.Contains("w_usr", TidalConstants.OAUTH_SCOPE);
        Assert.Contains("w_sub", TidalConstants.OAUTH_SCOPE);
        Assert.Contains("offline_access", TidalConstants.OAUTH_SCOPE);
    }

    /// <summary>
    /// Covers APP_MODE constant (line 23).
    /// Proof: grep -n "APP_MODE" src/Tidalarr/Core/Constants/TidalConstants.cs
    ///   23:    public const string APP_MODE = "android";
    /// </summary>
    [Fact]
    public void TidalConstants_AppMode_HasExpectedValue()
    {
        // Line 23: public const string APP_MODE = "android";
        Assert.Equal("android", TidalConstants.APP_MODE);
    }

    /// <summary>
    /// Covers LANGUAGE constant (line 24).
    /// Proof: grep -n "LANGUAGE" src/Tidalarr/Core/Constants/TidalConstants.cs
    ///   24:    public const string LANGUAGE = "EN";
    /// </summary>
    [Fact]
    public void TidalConstants_Language_HasExpectedValue()
    {
        // Line 24: public const string LANGUAGE = "EN";
        Assert.Equal("EN", TidalConstants.LANGUAGE);
    }

    /// <summary>
    /// Covers DEFAULT_SEARCH_LIMIT constant (line 36).
    /// Proof: grep -n "DEFAULT_SEARCH_LIMIT" src/Tidalarr/Core/Constants/TidalConstants.cs
    ///   36:    public const int DEFAULT_SEARCH_LIMIT = 100;
    /// </summary>
    [Fact]
    public void TidalConstants_DefaultSearchLimit_HasExpectedValue()
    {
        // Line 36: public const int DEFAULT_SEARCH_LIMIT = 100;
        Assert.Equal(100, TidalConstants.DEFAULT_SEARCH_LIMIT);
    }

    /// <summary>
    /// Covers MAX_SEARCH_LIMIT constant (line 37).
    /// Proof: grep -n "MAX_SEARCH_LIMIT" src/Tidalarr/Core/Constants/TidalConstants.cs
    ///   37:    public const int MAX_SEARCH_LIMIT = 1000;
    /// </summary>
    [Fact]
    public void TidalConstants_MaxSearchLimit_HasExpectedValue()
    {
        // Line 37: public const int MAX_SEARCH_LIMIT = 1000;
        Assert.Equal(1000, TidalConstants.MAX_SEARCH_LIMIT);
    }

    /// <summary>
    /// Covers DEFAULT_ITEM_LIMIT constant (line 38).
    /// Proof: grep -n "DEFAULT_ITEM_LIMIT" src/Tidalarr/Core/Constants/TidalConstants.cs
    ///   38:    public const int DEFAULT_ITEM_LIMIT = 1000;
    /// </summary>
    [Fact]
    public void TidalConstants_DefaultItemLimit_HasExpectedValue()
    {
        // Line 38: public const int DEFAULT_ITEM_LIMIT = 1000;
        Assert.Equal(1000, TidalConstants.DEFAULT_ITEM_LIMIT);
    }

    /// <summary>
    /// Validates search limit constraints.
    /// </summary>
    [Fact]
    public void TidalConstants_SearchLimits_AreWithinValidRange()
    {
        // Lines 36-37: search limits should be positive and max >= default
        Assert.True(TidalConstants.DEFAULT_SEARCH_LIMIT > 0);
        Assert.True(TidalConstants.MAX_SEARCH_LIMIT > 0);
        Assert.True(TidalConstants.MAX_SEARCH_LIMIT >= TidalConstants.DEFAULT_SEARCH_LIMIT);
    }
}
