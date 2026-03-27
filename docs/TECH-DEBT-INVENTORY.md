> **Note:** This document is historical and may not reflect current architecture. It tracked tech debt from the initial v1.0 development when Tidalarr was a direct TidalSharp port. The architecture has since been rewritten to use clean components and the shared library. See CLAUDE.md for current guidance.

# Technical Debt Inventory
## Tidalarr v1.0 - Accepted Technical Debt

---

## Purpose
This document explicitly tracks technical debt accepted during Tidalarr v1.0 development. Each item represents a conscious trade-off between immediate functionality and long-term maintainability.

---

## Inherited from TidalSharp

### 🔴 Critical Risk
1. **API v1 Dependency**
   - **Description**: Using Tidal API v1 which may be deprecated
   - **Risk**: API could stop working without notice
   - **Mitigation**: Monitor Tidal API status, prepare v2 migration plan
   - **Files**: `TidalCore/API.cs`

2. **Hardcoded Credentials**
   - **Description**: Client IDs and secrets hardcoded in source
   - **Risk**: Credentials could be revoked, security exposure
   - **Current Values**:
     ```
     CLIENT_ID = "zU4XHVVkc2tDPo4t"
     CLIENT_SECRET = "VJKhDFqJPqvsPVNBV6ukXTJmwlvbttP7wlMlrc72se4="
     CLIENT_ID_PKCE = "6BDSRdpK9hqEBTgU"
     CLIENT_SECRET_PKCE = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8="
     MASTER_KEY = "UIlTTEMmmLfGowo/UC60x2H45W6MdGgTRfo/umg4754="
     ```
   - **Files**: `TidalCore/Globals.cs`, `TidalCore/Decryption.cs`

### 🟡 Medium Risk
3. **Incomplete BTS Manifest Support**
   - **Description**: BTS format parsing only partially implemented
   - **Impact**: Some streams may fail to download
   - **Workaround**: MPD format works reliably
   - **Files**: `TidalCore/Manifest.cs`

4. **Basic Error Handling**
   - **Description**: Only simple retry logic with random delays
   - **Impact**: Poor user experience on failures
   - **Current Logic**: 429 responses trigger 0.5-1.5 second random delay
   - **Files**: `TidalCore/API.cs`

5. **FFMPEG Dependency**
   - **Description**: Requires external FFMPEG for some operations
   - **Impact**: Additional setup requirement for users
   - **Usage**: FLAC extraction, re-encoding
   - **Files**: `TidalCore/Download.cs`

### 🟢 Low Risk
6. **No Test Coverage**
   - **Description**: Original TidalSharp has no unit tests
   - **Impact**: Regression risk, harder to refactor
   - **Files**: All TidalCore files

7. **Tightly Coupled Code**
   - **Description**: API, Session, Download logic intertwined
   - **Impact**: Difficult to modify individual components
   - **Files**: `TidalCore/API.cs`, `TidalCore/Session.cs`

8. **Synchronous File Operations**
   - **Description**: Some I/O operations not async
   - **Impact**: Potential UI blocking in Lidarr
   - **Files**: `TidalCore/Download.cs`

---

## From Our Implementation Approach

### 🟡 Medium Risk
9. **Adapter Pattern Overhead**
   - **Description**: Extra abstraction layer between Lidarr and TidalSharp logic
   - **Impact**: Slight performance overhead, debugging complexity
   - **Justification**: Allows clean integration without modifying core logic
   - **Files**: All files in `Services/` directory

10. **No Response Caching**
    - **Description**: Every search/metadata request hits Tidal API
    - **Impact**: Slower performance, higher API usage
    - **Future Fix**: Add caching layer in adapters
    - **Files**: `Services/TidalApiAdapter.cs`

11. **No Rate Limiting Strategy**
    - **Description**: No proactive rate limit management
    - **Impact**: Potential for 429 errors and temporary blocks
    - **Current Handling**: Basic retry from TidalSharp
    - **Files**: `Services/TidalApiAdapter.cs`

### 🟢 Low Risk
12. **Limited Configuration Options**
    - **Description**: Minimal user-configurable settings
    - **Impact**: Less flexibility for power users
    - **Files**: `Integration/TidalarrSettings.cs`

13. **No Telemetry or Metrics**
    - **Description**: No performance or usage tracking
    - **Impact**: Hard to diagnose issues in production
    - **Files**: None - feature not implemented

---

## Known Limitations

### Functional Limitations
1. **Search Results**: Maximum 300 results (3 pages × 100 items)
2. **Download Format**: Only supports formats Tidal provides (no transcoding)
3. **Regional Restrictions**: Quality availability varies by region
4. **Concurrent Downloads**: No optimization for parallel downloads

### Performance Limitations
1. **Sequential Chunk Download**: Required by Tidal, can't parallelize
2. **No Prefetching**: Each operation is on-demand
3. **Memory Usage**: Entire track loaded into memory during download

---

## Risk Matrix

| Risk Level | Count | Action Required |
|------------|-------|-----------------|
| 🔴 Critical | 2 | Monitor closely, prepare contingency |
| 🟡 Medium | 6 | Plan for v2.0 improvements |
| 🟢 Low | 5 | Address if time permits |

---

## Mitigation Strategies

### Immediate (For V1.0)
1. **Document all workarounds** in user guide
2. **Monitor Tidal API status** regularly
3. **Test with multiple account types** (Free, Premium, HiFi)
4. **Provide clear error messages** where possible

### Short-term (V1.1 - V1.5)
1. **Add basic caching** to reduce API calls
2. **Improve error messages** for common failures
3. **Add configuration** for retry behavior
4. **Create integration tests** using real Tidal account

### Long-term (V2.0)
1. **Migrate to Tidal API v2** when proven stable
2. **Externalize credentials** to secure configuration
3. **Complete BTS support** or remove if unused
4. **Refactor to decouple** TidalSharp components
5. **Add comprehensive test suite**
6. **Implement proper rate limiting**
7. **Add response caching layer**
8. **Optimize memory usage** for large downloads

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2024-01 | Port TidalSharp directly | Proven functionality > clean architecture for v1 |
| 2024-01 | Keep API v1 | Still working, v2 migration too risky |
| 2024-01 | Accept hardcoded credentials | Same as TidalSharp, works for now |
| 2024-01 | Defer BTS support | MPD works for most content |
| 2024-01 | No caching in v1 | Simplicity > performance initially |

---

## Review Schedule

- **Weekly**: During v1.0 development - assess new debt
- **Post-Release**: 2 weeks after v1.0 - prioritize fixes
- **Quarterly**: Ongoing - reassess risk levels
- **API Changes**: Immediate review if Tidal changes detected

---

## Acceptance Criteria for Debt

Technical debt is acceptable in v1.0 if:
1. It doesn't prevent core functionality
2. It has a documented workaround
3. It can be fixed without major refactoring
4. The risk is understood and communicated
5. It significantly accelerates initial delivery

---

## Contact

**Primary Maintainer**: [Your Team]
**Escalation Path**: If critical debt items (🔴) show signs of failure, immediately begin contingency implementation.

---

*This document is a living record. Update it whenever technical debt is added, modified, or resolved.*
