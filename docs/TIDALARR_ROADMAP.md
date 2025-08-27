# Tidalarr Implementation Roadmap
## Phased Tidal Integration - From MVP to Excellence

---

## 🎯 Mission: Tidal Integration for Lidarr

**Primary Goal**: Enable Lidarr users to search and download music from Tidal  
**Quality Standard**: Production-ready, reliable, user-friendly  
**Timeline**: 2 weeks MVP, 3 weeks production-ready, 4+ weeks with ecosystem contributions  

---

## 📅 Phase-Based Roadmap

### **Phase 1: MVP (Weeks 1-2) - "Make It Work"**

**Week 1: Authentication Foundation**
- **Day 1**: Project setup + OAuth URL generation
- **Day 2**: OAuth callback handling + token exchange
- **Day 3**: Token storage (JSON) + basic validation
- **Day 4**: API client foundation + authentication headers
- **Day 5**: Basic search implementation

**Week 1 Deliverables:**
- [ ] User can start OAuth flow in browser
- [ ] User can complete authentication with redirect URL  
- [ ] Tokens are stored and loaded correctly
- [ ] Basic Tidal API calls work with authentication

**Week 2: Core Functionality**
- **Day 1**: Search results parsing + quality detection
- **Day 2**: Stream URL acquisition + manifest parsing (MPD focus)
- **Day 3**: Basic chunk downloading (sequential order)
- **Day 4**: Lidarr indexer integration with shared library
- **Day 5**: Basic download client integration

**Week 2 Deliverables:**
- [ ] Search works in Lidarr interface
- [ ] Can download single track successfully  
- [ ] Basic metadata applied to downloaded files
- [ ] Quality selection works (High, Lossless, HiRes)

**MVP Success Criteria:**
- User can authenticate with Tidal through Lidarr
- User can search for music and see results
- User can download tracks in their preferred quality
- Downloaded files play correctly with basic metadata

### **Phase 2: Production Hardening (Week 3) - "Make It Reliable"**

**Day 1**: Token refresh + concurrent request handling  
**Day 2**: Error handling + retry logic with Polly  
**Day 3**: Manifest validation + chunk download recovery  
**Day 4**: Album download + progress tracking  
**Day 5**: Comprehensive testing + bug fixes  

**Phase 2 Deliverables:**
- [ ] Handles token expiration automatically
- [ ] Recovers from network failures gracefully
- [ ] Downloads complete albums reliably
- [ ] Clear error messages for common failures
- [ ] 90%+ download success rate

### **Phase 3: Excellence + Contributions (Week 4+) - "Make It Outstanding"**

**Ecosystem Contributions:**
- OAuth 2.0 framework for shared library
- Advanced caching patterns
- Performance monitoring utilities
- Plugin development templates

**Performance Optimizations:**
- Intelligent caching with usage patterns
- Adaptive concurrency control  
- Request batching and deduplication
- Resource management

---

## 🔧 Technical Implementation Checklist

### **Core Components to Build**

#### Authentication (Week 1)
- [ ] `TidalOAuthService` - OAuth PKCE flow
- [ ] `TidalTokenManager` - Token lifecycle  
- [ ] `PKCEGenerator` - Code challenge generation
- [ ] `JsonTokenStorage` - Token persistence

#### API Integration (Week 1-2)  
- [ ] `TidalApiClient` - Core API operations
- [ ] `TidalRequestBuilder` - Request construction
- [ ] `TidalResponseParser` - Response parsing
- [ ] `TidalEndpoints` - URL management
- [ ] `TidalErrorClassifier` - Error handling

#### Streaming (Week 2)
- [ ] `TidalStreamService` - Stream URL acquisition
- [ ] `TidalManifestParser` - DASH manifest parsing
- [ ] `TidalChunkDownloader` - Sequential chunk download
- [ ] `TidalQualityDetector` - Quality identification

#### Lidarr Integration (Week 2)
- [ ] `TidalSettings` - Configuration UI
- [ ] `TidalIndexer` - Search integration  
- [ ] `TidalDownloadClient` - Download integration
- [ ] `TidalModule` - Plugin registration

---

## 🎮 Development Environment Setup

### **Repository Structure**
```
Tidalarr/
├── src/Tidalarr/                          # Main plugin project
├── src/Tidalarr.CLI/                      # Test bed CLI
├── tests/                                 # Test projects
├── docs/                                  # Documentation
└── scripts/                               # Build automation
```

### **First Commands**
```bash
# Initialize project
dotnet new sln -n Tidalarr
dotnet new classlib -n Tidalarr -o src/Tidalarr
dotnet sln add src/Tidalarr/Tidalarr.csproj

# Add shared library
cd src/Tidalarr
dotnet add package Lidarr.Plugin.Common --version 1.0.0
dotnet add package Polly --version 7.2.4

# Create test project
cd ../../
dotnet new xunit -n Tidalarr.Tests -o tests/Tidalarr.Tests
dotnet sln add tests/Tidalarr.Tests/Tidalarr.Tests.csproj
```

---

## ✅ Daily Success Criteria

### **Week 1 Daily Goals**
**Monday**: OAuth URL generates correctly  
**Tuesday**: Can exchange code for tokens  
**Wednesday**: Tokens save/load from JSON files  
**Thursday**: API calls include correct authentication  
**Friday**: Basic search returns Tidal results  

### **Week 2 Daily Goals**  
**Monday**: Search results show in Lidarr interface  
**Tuesday**: Can get stream URLs for tracks  
**Wednesday**: Can download and assemble chunks  
**Thursday**: Download client integrates with Lidarr  
**Friday**: Complete track download works end-to-end  

### **Week 3 Daily Goals**
**Monday**: Token refresh works without user intervention  
**Tuesday**: Network failures don't crash downloads  
**Wednesday**: Album downloads work reliably  
**Thursday**: Error messages are clear and actionable  
**Friday**: All major edge cases handled  

---

## 🎯 Critical Implementation Notes

### **Essential TidalSharp Knowledge to Preserve**

1. **OAuth Parameters** (MUST be exact):
   ```csharp
   CLIENT_ID_PKCE = "6BDSRdpK9hqEBTgU";
   CLIENT_SECRET_PKCE = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=";
   REDIRECT_URI = "https://tidal.com/android/login/auth";
   ```

2. **API Request Requirements**:
   - Always include `sessionId` and `countryCode`
   - Use `Bearer {token}` authorization header
   - API v1 endpoints (don't try to modernize)
   - Request parameters exactly as TidalSharp does

3. **Streaming Critical Details**:
   - Chunks MUST be downloaded sequentially
   - Manifest parsing: Focus on MPD format first
   - Quality detection from `mediaMetadata.tags`
   - Decryption key: `UIlTTEMmmLfGowo/UC60x2H45W6MdGgTRfo/umg4754=`

### **Shared Library Integration Points**
- Use `BaseStreamingSettings` for configuration UI
- Use shared library caching patterns  
- Use shared library quality mapping
- Use shared library HTTP client patterns

---

## 🚦 Risk Management

### **High Risk (Monitor Daily)**
- OAuth flow reliability (this gates everything)
- Token refresh edge cases (silent failures hurt UX)
- Chunk download order (corruption if wrong)
- API rate limiting (can break temporarily)

### **Medium Risk (Monitor Weekly)**  
- Manifest parsing accuracy (affects streaming)
- Quality detection reliability (affects user satisfaction)
- Memory usage for large albums (performance impact)
- Error message clarity (affects support burden)

### **Low Risk (Monitor Monthly)**
- Search result ranking (nice to have)
- Cover art download (aesthetic only)
- Performance optimization (works first, fast second)
- Ecosystem contributions (valuable but not critical)

---

## 🎉 Success Celebration Milestones

### **🥉 Bronze Medal (End of Week 1)**
**Achievement**: OAuth authentication works
**Celebration**: Can log into Tidal through Lidarr settings

### **🥈 Silver Medal (End of Week 2)**  
**Achievement**: Can download a track
**Celebration**: First successful Tidal download through Lidarr

### **🥇 Gold Medal (End of Week 3)**
**Achievement**: Production-ready reliability
**Celebration**: Can reliably download complete albums

### **🏆 Championship (Week 4+)**
**Achievement**: Ecosystem contributions merged
**Celebration**: Other developers can build plugins faster using our framework

---

## 📞 Support and Escalation

### **Daily Blockers**
If stuck > 4 hours on any component, escalate to:
1. Check TidalSharp reference implementation
2. Test with TidalCLI for debugging
3. Review shared library examples
4. Ask for architectural guidance

### **Weekly Review**
Every Friday: Assess progress against phase goals
- On track: Continue with current approach  
- Behind: Cut scope to meet timeline
- Ahead: Consider pulling items from next phase

---

## 🚀 Ready to Execute

**Next Action**: Initialize project structure and begin Week 1, Day 1 tasks
**Focus**: OAuth URL generation - the foundation everything else builds on
**Success Metric**: Generate working Tidal OAuth URL that opens in browser

Let's build Tidalarr one component at a time, keeping it simple, focusing on what matters, and delivering value quickly while maintaining architectural quality for the future.