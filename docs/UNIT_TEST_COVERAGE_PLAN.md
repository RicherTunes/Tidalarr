# Tidalarr 100% Unit Test Coverage Plan
## Ultra-Comprehensive Testing Strategy

---

## 🎯 **Coverage Goal: 100% Line + Branch Coverage**

**Current Baseline**: ~60-70% estimated (77 tests covering major flows)  
**Target**: 100% line coverage + 100% branch coverage + 100% method coverage  
**Timeline**: Implement systematic testing for every component, method, and code path  

---

## 📊 **Component Coverage Analysis**

### **Core Layer Components**

#### **TidalConstants.cs** - Static Constants
**Current Coverage**: 0% (not tested)  
**Required Tests**: Property access validation
```csharp
[Test] void TidalConstants_ClientCredentials_AreNotEmpty()
[Test] void TidalConstants_ApiEndpoints_AreValidUrls()  
[Test] void TidalConstants_QualityParameters_ContainAllQualities()
[Test] void TidalConstants_MasterKey_IsValidBase64()
```

#### **TidalModels.cs** - Record Types
**Current Coverage**: ~30% (basic construction only)  
**Required Tests**: All property combinations, validation, edge cases
```csharp
// TidalTokens
[Test] void TidalTokens_IsExpired_WhenExpiresAtInPast_ReturnsTrue()
[Test] void TidalTokens_IsExpired_WithFiveMinuteBuffer_ReturnsTrue()
[Test] void TidalTokens_IsExpired_WithValidToken_ReturnsFalse()
[Test] void TidalTokens_Constructor_AllProperties_AreSetCorrectly()

// TidalTrackInfo  
[Test] void TidalTrackInfo_Constructor_WithNullArtists_ThrowsException()
[Test] void TidalTrackInfo_Constructor_WithEmptyTitle_ThrowsException()
[Test] void TidalTrackInfo_Constructor_WithValidData_SetsAllProperties()

// TidalAlbumInfo
[Test] void TidalAlbumInfo_Constructor_WithNullTracks_UsesEmptyList()
[Test] void TidalAlbumInfo_AvailableQualities_WithDuplicates_DeduplicatesCorrectly()

// TidalSearchResults
[Test] void TidalSearchResults_Constructor_WithNullAlbums_UsesEmptyList()
[Test] void TidalSearchResults_TotalCount_MatchesAlbumsAndTracksCount()

// TidalCallbackResult
[Test] void TidalCallbackResult_Success_CreatesSuccessResult()
[Test] void TidalCallbackResult_Failure_CreatesFailureWithMessage()

// TidalStreamInfo
[Test] void TidalStreamInfo_Constructor_WithEmptyChunkUrls_ThrowsException()
[Test] void TidalStreamInfo_IsEncrypted_WithNoneEncryption_ReturnsFalse()

// TidalManifest
[Test] void TidalManifest_Constructor_WithValidData_SetsAllProperties()
[Test] void TidalManifest_SampleRate_DefaultValue_Is44100()
```

#### **TidalDtos.cs** - API Response Models
**Current Coverage**: ~20% (basic deserialization only)  
**Required Tests**: JSON serialization/deserialization, null handling
```csharp
[Test] void TidalSearchResponseDto_Deserialize_WithValidJson_ReturnsCorrectObject()
[Test] void TidalSearchResponseDto_Deserialize_WithNullAlbums_HandlesGracefully()
[Test] void TidalTrackDto_Deserialize_WithMissingFields_UsesDefaults()
[Test] void TidalAlbumDto_Serialize_ThenDeserialize_RoundTripSuccessful()
[Test] void TidalPlaybackInfoDto_Deserialize_WithEncryption_SetsPropertiesCorrectly()
```

#### **TidalExceptions.cs** - Custom Exception Hierarchy  
**Current Coverage**: 0% (newly added)
**Required Tests**: Exception creation, inheritance, serialization
```csharp
[Test] void TidalException_Constructor_WithMessage_SetsMessageCorrectly()
[Test] void TidalException_Constructor_WithInnerException_SetsInnerException()
[Test] void TidalAuthenticationException_InheritsFrom_TidalException()
[Test] void TidalRateLimitException_Constructor_WithRetrySeconds_SetsProperty()
[Test] void TidalStreamUnavailableException_Constructor_WithTrackAndQuality_SetsProperties()
[Test] void TidalApiException_Constructor_WithStatusCode_SetsStatusCode()
[Test] void TidalManifestException_Constructor_WithManifestType_SetsType()
```

---

### **Domain Layer Components**

#### **PKCEGenerator.cs** - PKCE Challenge Generation
**Current Coverage**: ~80% (basic generation tested)  
**Missing Tests**: Edge cases, security validation
```csharp
[Test] void PKCEGenerator_GeneratePair_MultipleCalls_ProducesDifferentResults()
[Test] void PKCEGenerator_GeneratePair_CodeVerifier_MeetsRFC7636Requirements()
[Test] void PKCEGenerator_GeneratePair_CodeChallenge_IsValidBase64Url()
[Test] void PKCEGenerator_GeneratePair_CodeChallenge_IsSha256OfVerifier()
[Test] void PKCEGenerator_GenerateCodeVerifier_WithDifferentLengths_ReturnsCorrectLength()
[Test] void PKCEGenerator_CreateS256Challenge_WithKnownInput_ReturnsExpectedHash()
[Test] void PKCEGenerator_Base64UrlEncode_WithPaddingChars_RemovesPadding()
[Test] void PKCEGenerator_Base64UrlEncode_WithSpecialChars_ReplacesCorrectly()
```

#### **TidalOAuthService.cs** - OAuth Authentication
**Current Coverage**: ~70% (main flow tested)  
**Missing Tests**: Concurrent access, error scenarios, token lifecycle
```csharp
[Test] async Task TidalOAuthService_GetValidTokens_WhenExpired_RefreshesAutomatically()
[Test] async Task TidalOAuthService_GetValidTokens_ConcurrentCalls_OnlyRefreshesOnce()
[Test] async Task TidalOAuthService_ExchangeCode_WithInvalidCode_ThrowsSpecificException()
[Test] async Task TidalOAuthService_RefreshTokens_WithExpiredRefreshToken_ThrowsAuthException()
[Test] async Task TidalOAuthService_RefreshTokens_WithNetworkFailure_RetriesWithPolly()
[Test] void TidalOAuthService_ParseCallback_WithStateParameter_ValidatesCorrectly()
[Test] void TidalOAuthService_BuildAuthorizationUrl_ContainsAllRequiredParameters()
[Test] void TidalOAuthService_GenerateSecureState_ProducesUniqueCryptographicValues()
[Test] void TidalOAuthService_GenerateClientUniqueKey_ProducesValidGuid()
[Test] async Task TidalOAuthService_Dispose_ReleasesResources_Correctly()
```

#### **TidalApiClient.cs** - API Communication
**Current Coverage**: ~60% (basic API calls tested)  
**Missing Tests**: All error scenarios, resilience patterns, edge cases  
```csharp
[Test] async Task TidalApiClient_SearchAsync_WithEmptyQuery_ThrowsArgumentException()
[Test] async Task TidalApiClient_SearchAsync_WithLargeLimit_CapsAtMaximum()
[Test] async Task TidalApiClient_SearchAsync_WithRateLimitError_RetriesWithBackoff()
[Test] async Task TidalApiClient_SearchAsync_WithCircuitBreakerOpen_ThrowsCircuitBreakerException()
[Test] async Task TidalApiClient_GetTrackAsync_WithInvalidId_ThrowsResourceNotFoundException()
[Test] async Task TidalApiClient_GetTrackAsync_WithExpiredToken_RefreshesAndRetries()
[Test] async Task TidalApiClient_GetAlbumAsync_WithGeoRestricted_ThrowsUnavailableException()
[Test] async Task TidalApiClient_GetStreamInfo_WithUnsupportedQuality_ThrowsQualityException()
[Test] async Task TidalApiClient_GetStreamInfo_WithEncryptedStream_ReturnsEncryptionInfo()
[Test] void TidalApiClient_BuildAuthenticatedRequest_IncludesAllRequiredHeaders()
[Test] void TidalApiClient_BuildAuthenticatedRequest_IncludesSessionParameters()
[Test] async Task TidalApiClient_AllMethods_WithNullAuth_ThrowArgumentNullException()
```

#### **TidalQualityDetector.cs** - Quality Management
**Current Coverage**: ~90% (well tested)  
**Missing Tests**: Edge cases, error handling
```csharp
[Test] void TidalQualityDetector_DetectQuality_WithNullString_ReturnsDefault()
[Test] void TidalQualityDetector_DetectQuality_WithEmptyString_ReturnsDefault()  
[Test] void TidalQualityDetector_DetectQuality_WithWhitespace_ReturnsDefault()
[Test] void TidalQualityDetector_DetectAvailableQualities_WithNullTags_ReturnsBasicQualities()
[Test] void TidalQualityDetector_DetectAvailableQualities_WithEmptyTags_ReturnsBasicQualities()
[Test] void TidalQualityDetector_SelectBestQuality_WithEmptyAvailable_ReturnsDefault()
[Test] void TidalQualityDetector_SelectBestQuality_WithNullPreference_HandlesGracefully()
[Test] void TidalQualityDetector_GetQualityDisplayName_WithAllQualities_ReturnsCorrectNames()
[Test] void TidalQualityDetector_IsQualityAvailable_WithMixedTags_DetectsCorrectly()
```

#### **TidalManifestParser.cs** - Manifest Processing  
**Current Coverage**: ~70% (basic parsing tested)
**Missing Tests**: Malformed data, security validation, all code paths
```csharp
[Test] void TidalManifestParser_ParseManifest_WithMalformedBase64_ThrowsFormatException()
[Test] void TidalManifestParser_ParseManifest_WithEmptyManifest_ThrowsManifestException()
[Test] void TidalManifestParser_ParseDashManifest_WithMissingAdaptationSet_ThrowsException()
[Test] void TidalManifestParser_ParseDashManifest_WithMultipleAdaptationSets_SelectsFirst()
[Test] void TidalManifestParser_ParseBtsManifest_WithMissingUrls_ThrowsException()
[Test] void TidalManifestParser_ParseBtsManifest_WithInvalidJson_ThrowsException()
[Test] void TidalManifestParser_DetermineFileExtension_WithUnknownCodec_ReturnsDefault()
[Test] void TidalManifestParser_ExtractChunkUrls_WithComplexTimeline_GeneratesCorrectUrls()
[Test] void TidalManifestParser_ExtractChunkUrls_WithNoTimeline_ReturnsTemplateUrl()
[Test] void TidalManifestParser_ParseDashManifest_WithNamespaces_HandlesCorrectly()
```

#### **TidalStreamService.cs** - Stream Coordination
**Current Coverage**: ~50% (basic functionality)  
**Missing Tests**: Validation, error handling, all methods
```csharp
[Test] async Task TidalStreamService_GetStreamInfo_WithValidTrack_ReturnsStreamInfo()
[Test] async Task TidalStreamService_GetStreamInfo_WithInvalidTrack_ThrowsException()
[Test] async Task TidalStreamService_ValidateStreamAvailability_WithValidStream_ReturnsTrue()
[Test] async Task TidalStreamService_ValidateStreamAvailability_WithInvalidStream_ReturnsFalse()
[Test] async Task TidalStreamService_GetAvailableQualities_WithMultipleQualities_ReturnsAll()
[Test] async Task TidalStreamService_GetAvailableQualities_WithUnavailableTrack_ReturnsEmpty()
[Test] async Task TidalStreamService_GetStreamInfoWithParsing_WithCorruptManifest_ThrowsException()
```

#### **TidalChunkDownloader.cs** - Download Management
**Current Coverage**: ~60% (basic download tested)  
**Missing Tests**: Error scenarios, retry logic, progress reporting
```csharp
[Test] async Task TidalChunkDownloader_Download_WithFailedChunk_RetriesCorrectly()
[Test] async Task TidalChunkDownloader_Download_WithAllChunksFailed_ThrowsException()
[Test] async Task TidalChunkDownloader_Download_WithProgressReport_CallsProgressCorrectly()
[Test] async Task TidalChunkDownloader_Download_WithCancellation_StopsDownload()
[Test] async Task TidalChunkDownloader_ValidateChunkAccess_WithUnauthorized_ReturnsFalse()
[Test] async Task TidalChunkDownloader_ValidateChunkAccess_WithNetworkError_ReturnsFalse()
[Test] async Task TidalChunkDownloader_DownloadBytes_DisposesStreamCorrectly()
[Test] async Task TidalChunkDownloader_DownloadChunkWithRetry_ExhaustsRetries_ThrowsWithInnerException()
```

---

### **Application Layer Components**

#### **TidalSearchService.cs** - Search Orchestration
**Current Coverage**: ~40% (basic search tested)  
**Missing Tests**: Quality integration, search types, error handling
```csharp
[Test] async Task TidalSearchService_Search_WithEmptyQuery_ThrowsArgumentException()
[Test] async Task TidalSearchService_Search_WithQualityPreference_FiltersCorrectly()
[Test] async Task TidalSearchService_SearchByType_Album_ReturnsOnlyAlbums()
[Test] async Task TidalSearchService_SearchByType_Track_ReturnsOnlyTracks()
[Test] async Task TidalSearchService_SearchByType_All_ReturnsBoth()
[Test] async Task TidalSearchService_GetAlbumWithTracks_WithValidId_ReturnsComplete()
[Test] async Task TidalSearchService_EnhanceAlbumWithQuality_AddsCorrectQualities()
[Test] async Task TidalSearchService_EnhanceTrackWithQuality_SelectsBestQuality()
```

---

### **Infrastructure Layer Components**

#### **JsonTokenStorage.cs** - Token Persistence
**Current Coverage**: ~80% (good coverage)  
**Missing Tests**: File system edge cases, concurrency
```csharp
[Test] async Task JsonTokenStorage_Save_WithReadOnlyFile_ThrowsException()
[Test] async Task JsonTokenStorage_Save_WithInvalidPath_ThrowsException()  
[Test] async Task JsonTokenStorage_Load_WithLockedFile_ThrowsException()
[Test] async Task JsonTokenStorage_Load_ConcurrentAccess_HandlesCorrectly()
[Test] async Task JsonTokenStorage_Delete_FileInUse_HandlesGracefully()
[Test] void JsonTokenStorage_Constructor_WithNullPath_UsesDefault()
[Test] void JsonTokenStorage_GetDefaultPath_ReturnsValidPath()
[Test] void JsonTokenStorage_EnsureDirectory_CreatesIfNotExists()
```

#### **TidalResiliencePolicy.cs** - Resilience Patterns  
**Current Coverage**: 0% (static methods not tested)
**Required Tests**: Policy creation, configuration validation
```csharp
[Test] void TidalResiliencePolicy_CreateHttpRetry_ReturnsConfiguredPolicy()
[Test] void TidalResiliencePolicy_CreateTokenRefresh_HasCorrectRetryCount()
[Test] void TidalResiliencePolicy_CreateChunkDownload_HasFastFailure()
[Test] void TidalResiliencePolicy_CreateCircuitBreaker_HasCorrectThresholds()
[Test] void TidalResiliencePolicy_ShouldRetry_WithVariousStatusCodes_ReturnsCorrectly()
[Test] async Task TidalResiliencePolicy_HttpRetryPolicy_WithTransientError_RetriesCorrectly()
[Test] async Task TidalResiliencePolicy_CircuitBreaker_WithRepeatedFailures_OpensCircuit()
```

#### **TidalTelemetry.cs** - Observability
**Current Coverage**: 0% (newly added)  
**Required Tests**: Logging verification, activity tracking
```csharp
[Test] void TidalTelemetry_TrackDownloadStarted_LogsCorrectly()
[Test] void TidalTelemetry_TrackDownloadCompleted_LogsWithMetrics()
[Test] void TidalTelemetry_TrackDownloadFailed_LogsException()
[Test] void TidalTelemetry_TrackApiCall_LogsWithLatency()
[Test] void TidalTelemetry_TrackAuthentication_LogsSuccessAndFailure()
[Test] void TidalTelemetry_StartActivity_ReturnsDisposableActivity()
[Test] void TidalTelemetry_StartActivity_WithNullActivity_ReturnsNullActivity()
[Test] void TidalTelemetry_CircuitBreakerEvents_LogWithCorrectLevel()
```

---

### **Integration Layer Components**

#### **TidalSettings.cs** - Configuration Management
**Current Coverage**: ~70% (validation tested)  
**Missing Tests**: All validation paths, property edge cases
```csharp
[Test] void TidalSettings_IsValid_WithEmptyRedirectUrl_ReturnsFalseWithMessage()
[Test] void TidalSettings_IsValid_WithInvalidUrl_ReturnsFalseWithMessage()  
[Test] void TidalSettings_IsValid_WithWrongDomain_ReturnsFalseWithMessage()
[Test] void TidalSettings_IsValid_WithInvalidMarket_ReturnsFalseWithMessage()
[Test] void TidalSettings_IsValid_WithAllValidValues_ReturnsTrue()
[Test] void TidalSettings_IsValidMarket_WithAllSupportedMarkets_ReturnsTrue()
[Test] void TidalSettings_IsValidMarket_WithUnsupportedMarket_ReturnsFalse()
[Test] void TidalSettings_IsValidMarket_WithNullMarket_ReturnsFalse()
[Test] void TidalSettings_Constructor_SetsDefaultValues_Correctly()
[Test] void TidalSettings_InheritsFromBaseStreaming_HasAllBaseProperties()
```

#### **TidalIndexer.cs** - Search Integration
**Current Coverage**: ~30% (basic constructor)  
**Missing Tests**: Search functionality, error handling, mapping
```csharp
[Test] async Task TidalIndexer_Search_WithValidQuery_ReturnsReleaseInfo()
[Test] async Task TidalIndexer_Search_WithNoResults_ReturnsEmptyList()
[Test] async Task TidalIndexer_Search_WithAuthFailure_ThrowsAuthException()  
[Test] async Task TidalIndexer_Search_WithApiError_ThrowsApiException()
[Test] void TidalIndexer_MapToReleaseInfo_WithAlbums_CreatesCorrectReleases()
[Test] void TidalIndexer_MapToReleaseInfo_WithTracks_CreatesSingles()
[Test] void TidalIndexer_GetHighestQuality_WithMultipleQualities_ReturnsMax()
[Test] void TidalIndexer_GetHighestQuality_WithEmptyQualities_ReturnsDefault()
[Test] void TidalIndexer_ParsePreferredQuality_WithAllValidStrings_ReturnsCorrectEnum()
[Test] void TidalIndexer_ParsePreferredQuality_WithInvalidString_ReturnsDefault()
```

#### **TidalDownloadClient.cs** - Download Integration
**Current Coverage**: ~40% (basic download logic)  
**Missing Tests**: Error scenarios, file management, progress tracking
```csharp
[Test] async Task TidalDownloadClient_DownloadTrack_WithLargeFile_StreamsToDisk()
[Test] async Task TidalDownloadClient_DownloadTrack_WithSmallFile_UsesMemory()
[Test] async Task TidalDownloadClient_DownloadTrack_WithInvalidTrack_ReturnsFailure()
[Test] async Task TidalDownloadClient_DownloadAlbum_WithMultipleTracks_DownloadsAll()
[Test] async Task TidalDownloadClient_DownloadAlbum_WithFailedTrack_ContinuesWithOthers()
[Test] async Task TidalDownloadClient_ValidateDownload_WithUnavailableTrack_ReturnsFalse()
[Test] void TidalDownloadClient_EstimateSize_WithAllQualities_ReturnsCorrectSizes()
[Test] void TidalDownloadClient_GetTempFilePath_WithSpecialChars_SanitizesCorrectly()
[Test] async Task TidalDownloadClient_StreamToFile_WithIOException_HandlesGracefully()
[Test] void TidalDownloadClient_ParsePreferredQuality_WithAllInputs_ReturnsCorrect()
```

#### **TidalModule.cs** - Service Registration
**Current Coverage**: ~50% (basic registration)  
**Missing Tests**: Service resolution, configuration validation, all factory methods
```csharp
[Test] void TidalModule_RegisterServices_RegistersAllRequiredServices()
[Test] void TidalModule_RegisterServices_RegistersCorrectLifetimes()
[Test] void TidalModule_RegisterServices_RegistersHttpClientFactory()
[Test] void TidalModule_RegisterServices_RegistersResiliencePolicies()
[Test] void TidalModule_CreateIndexer_WithValidSettings_ReturnsIndexer()
[Test] void TidalModule_CreateIndexer_WithInvalidSettings_ThrowsException()
[Test] void TidalModule_CreateDownloadClient_WithValidSettings_ReturnsClient()
[Test] void TidalModule_ValidateConfiguration_WithAllValidations_ReturnsCorrect()
[Test] void TidalModule_Constants_HaveCorrectValues()
```

#### **TidalProtocol.cs** - Protocol Management  
**Current Coverage**: 0% (not tested)
**Required Tests**: URL validation, parsing, construction
```csharp
[Test] void TidalProtocol_IsValidUrl_WithValidTidalUrl_ReturnsTrue()
[Test] void TidalProtocol_IsValidUrl_WithInvalidUrl_ReturnsFalse()
[Test] void TidalProtocol_ParseUrl_WithAlbumUrl_ReturnsCorrectTypeAndId()
[Test] void TidalProtocol_ParseUrl_WithTrackUrl_ReturnsCorrectTypeAndId()
[Test] void TidalProtocol_ParseUrl_WithInvalidUrl_ThrowsArgumentException()
[Test] void TidalProtocol_BuildAlbumUrl_WithValidId_ReturnsCorrectUrl()
[Test] void TidalProtocol_BuildTrackUrl_WithValidId_ReturnsCorrectUrl()
```

---

## 🎯 **100% Coverage Strategy**

### **Phase 1: Core Component Completion (Day 1)**
1. **Complete Constants Testing** - Test all static values and mappings
2. **Complete Models Testing** - Test all record constructors, properties, validation
3. **Complete Exceptions Testing** - Test all exception scenarios and inheritance
4. **Complete DTOs Testing** - Test serialization/deserialization edge cases

### **Phase 2: Domain Logic Completion (Day 2)**  
1. **Complete Authentication Testing** - All OAuth flows, error scenarios, concurrency
2. **Complete API Client Testing** - All endpoints, error handling, resilience integration
3. **Complete Quality Testing** - All detection scenarios, edge cases, mappings
4. **Complete Streaming Testing** - Manifest parsing, error recovery, validation

### **Phase 3: Infrastructure Completion (Day 3)**
1. **Complete Storage Testing** - File system operations, concurrency, error handling
2. **Complete Resilience Testing** - Policy creation, configuration, behavior validation  
3. **Complete Telemetry Testing** - Logging verification, activity tracking, metrics

### **Phase 4: Integration Completion (Day 4)**
1. **Complete Settings Testing** - All validation paths, property combinations
2. **Complete Plugin Testing** - Service registration, factory methods, integration
3. **Complete Download Testing** - All download scenarios, error paths, file management
4. **Complete Search Testing** - All search types, quality integration, mapping

---

## 📋 **Coverage Measurement Plan**

### **Tools and Metrics**
- **coverlet.msbuild** for line and branch coverage
- **ReportGenerator** for HTML coverage reports  
- **Target**: 100% line coverage, 95%+ branch coverage
- **Exclude**: Generated code, external dependencies

### **Coverage Commands**  
```bash
# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutput=coverage/ /p:CoverletOutputFormat=opencover

# Generate HTML report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage/coverage.opencover.xml -targetdir:coverage/html

# View results
start coverage/html/index.html
```

### **Coverage Targets by Component**
- **Core Models**: 100% (simple record types)
- **Authentication**: 95% (some error paths may be unreachable)  
- **API Client**: 98% (mock some network scenarios)
- **Streaming**: 95% (some codec edge cases)
- **Storage**: 90% (some file system scenarios)
- **Integration**: 98% (comprehensive business logic testing)

---

## 🧪 **Test Quality Standards**

### **Every Test Must:**
1. **Follow AAA Pattern** - Arrange, Act, Assert
2. **Test One Thing** - Single responsibility per test
3. **Be Independent** - No test dependencies  
4. **Be Deterministic** - Same result every time
5. **Have Descriptive Names** - Clear what's being tested
6. **Include Edge Cases** - Null, empty, boundary values
7. **Test Error Paths** - Exception scenarios and error handling

### **Test Categories**
- **Unit Tests**: Isolated component testing (target: 200+ tests)
- **Integration Tests**: Component interaction (existing: ~20 tests)
- **Edge Case Tests**: Boundary conditions and error scenarios  
- **Property Tests**: Exhaustive input validation

### **Mocking Strategy**
- **Mock External Dependencies**: HTTP calls, file system, time
- **Use Real Objects**: For domain logic and simple components
- **Verify Interactions**: Ensure dependencies called correctly
- **Test Isolation**: No shared state between tests

---

## 🎯 **Implementation Priority**

**HIGH PRIORITY** (Critical for 100% coverage):
1. **TidalConstants** - Quick wins, static testing
2. **TidalExceptions** - Exception hierarchy validation  
3. **TidalApiClient** - Core functionality, many code paths
4. **TidalManifestParser** - Complex logic, many branches

**MEDIUM PRIORITY** (Important gaps):
1. **TidalStreamService** - Service coordination logic
2. **TidalDownloadClient** - File management and error handling
3. **TidalModule** - Service registration validation
4. **TidalSettings** - Configuration validation paths

**LOW PRIORITY** (Already well tested):
1. **PKCEGenerator** - Already ~80% covered
2. **TidalQualityDetector** - Already ~90% covered  
3. **JsonTokenStorage** - Already ~80% covered

---

## 📊 **Success Metrics**

### **Coverage Targets**
- **Line Coverage**: 100% (every line executed)
- **Branch Coverage**: 95%+ (every if/else, try/catch)
- **Method Coverage**: 100% (every public method tested)
- **Total Tests**: 200+ unit tests (currently ~77)

### **Quality Gates**  
- **Build**: Must pass with 100% coverage
- **Performance**: Tests run in <30 seconds
- **Maintainability**: Tests are readable and maintainable
- **Reliability**: No flaky tests, deterministic results

This ultra-comprehensive plan ensures every line of code is tested, every edge case is covered, and we achieve true 100% unit test coverage for production-ready quality! 🎯