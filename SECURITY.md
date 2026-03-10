# Security Policy

## Reporting Security Issues

**IMPORTANT**: Do NOT open public GitHub issues for security vulnerabilities.

### Contact Information
- **Email**: security@richertunes.com (or xfear26@hotmail.com)
- **Response Time**: Within 48 hours
- **Expected Resolution**: Based on severity (Critical: 7 days, High: 30 days)

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: (Active development) |

## Security Features

### Current Infrastructure
- ✅ **CodeQL Static Analysis**: Automated C# security scanning
- ✅ **Secret Scanning**: GitLeaks monitors for Tidal credentials
- ✅ **Dependency Monitoring**: Dependabot automated updates
- ✅ **Vulnerability Scanning**: NuGet package vulnerability detection
- ✅ **Security Policy**: Documented disclosure process

### Planned Enhancements
- Container image scanning (when Docker images published)
- SBOM generation for releases
- Artifact signing with Cosign/GPG
- Third-party penetration testing
- Bug bounty program (future)

## Vulnerability Disclosure Process

### 1. Report Received
Security team acknowledges receipt within 48 hours

### 2. Assessment & Triage
- **Critical** (CVSS 9.0-10.0): Immediate action, 7-day resolution
- **High** (CVSS 7.0-8.9): Priority fix, 30-day resolution
- **Medium** (CVSS 4.0-6.9): Standard fix, 90-day resolution
- **Low** (CVSS 0.1-3.9): Routine fix, next release

### 3. Fix Development
- Private security branch created
- Patch developed and tested
- Backports to supported versions
- Security advisory drafted

### 4. Coordinated Disclosure
- 90-day disclosure timeline (negotiable)
- Security advisory published
- CVE assigned if applicable
- Users notified via releases and security tab

### 5. Post-Disclosure
- Lessons learned documented
- Prevention measures implemented
- Security audit updated
- Contributor acknowledged (with permission)

## Security Best Practices for Contributors

### Code Security

#### Input Validation
- **All external data must be validated**: User input, API responses, file contents
- **Whitelist approach**: Define what's allowed, reject everything else
- **Type validation**: Ensure data types match expectations

#### Authentication & Credentials
- **No hardcoded secrets**: Use environment variables or encrypted storage
- **Tidal credentials**: OAuth tokens in secure configuration only
- **OAuth tokens**: Encrypt at rest using Lidarr.Plugin.Common token storage
- **Session management**: Implement proper expiration and refresh logic

#### Injection Prevention
- **SQL Injection**: Use parameterized queries (N/A - no SQL database)
- **Command Injection**: Sanitize all shell command inputs
- **Path Traversal**: Validate and sanitize file paths before access
- **XSS**: Encode output for any web-facing components

#### Error Handling
- **No sensitive data in errors**: Strip credentials, paths, internal state
- **User-friendly messages**: Generic errors to users, detailed logs internally
- **Fail securely**: Default to denying access on errors

### Dependency Management

#### Regular Updates
- Monitor Dependabot PRs weekly
- Review security advisories for dependencies
- Test updates in isolation before merging
- Document breaking changes

#### Dependency Hygiene
- Minimize dependency count
- Prefer well-maintained packages
- Review transitive dependencies
- Use lock files for reproducibility

#### Known Vulnerable Packages
Current vulnerable packages (example - update regularly):
- [To be populated by security scans]

### Tidal API Security

#### Authentication
- OAuth 2.0 + PKCE flow implementation
- Token refresh before expiration
- Secure token storage (encrypted at rest)
- Never log tokens or credentials

#### Rate Limiting
- Respect Tidal API rate limits
- Implement backoff on 429 responses
- Cache responses to reduce API calls
- Monitor API usage patterns

#### Data Validation
- Validate all API responses
- Handle malformed data gracefully
- Sanitize metadata before storage
- Verify file checksums for downloads

### Data Protection

#### Download Security
- Verify HTTPS for all Tidal connections
- Validate content types and file sizes
- Check file integrity (checksums)
- Scan for malicious content (optional)

#### Temporary Files
- Secure cleanup of temporary downloads
- No credentials in temp files
- Appropriate file permissions
- Automatic cleanup on failure

#### Logging
- Never log credentials, tokens, or PII
- Sanitize paths and file names
- Redact sensitive fields in structured logs
- Secure log file permissions

## Security Audit History

| Date | Type | Auditor | Findings | Remediation |
|------|------|---------|----------|-------------|
| 2026-03-10 | Internal | Claude Code | Security policy created from ecosystem template | SECURITY.md added |
| TBD | External | TBD | N/A | Planned |

## Threat Model

### Assets
1. **User credentials**: Tidal account credentials, OAuth tokens
2. **Application data**: Downloaded music, metadata cache, search history
3. **Configuration**: Plugin settings, API credentials, quality preferences
4. **System access**: File system access, network access, Lidarr integration

### Threats

#### 1. Credential Theft
- **Attack vector**: Exposed tokens, weak encryption, logging
- **Impact**: Unauthorized Tidal account access
- **Mitigation**: Encrypted storage, no logging, secure transmission

#### 2. API Abuse
- **Attack vector**: Rate limit violations, quota exhaustion
- **Impact**: Account suspension, service degradation
- **Mitigation**: Rate limiting, caching, backoff strategies

#### 3. Data Tampering
- **Attack vector**: Modified downloads, corrupted metadata
- **Impact**: Malicious files, incorrect tagging
- **Mitigation**: Checksum validation, HTTPS enforcement

#### 4. Supply Chain Attacks
- **Attack vector**: Compromised dependencies, malicious packages
- **Impact**: Code execution, data exfiltration
- **Mitigation**: Dependency scanning, subresource integrity, code review

#### 5. Path Traversal
- **Attack vector**: Malicious file paths in downloads
- **Impact**: Write to unauthorized locations
- **Mitigation**: Path sanitization, permission checks

### Attack Surface

**External**:
- Tidal API endpoints
- NuGet package sources
- GitHub Actions workflows
- Docker base images (future)

**Internal**:
- Lidarr plugin interface
- File system operations
- Network requests
- Configuration storage

## Compliance

### Data Privacy
- **Minimal data collection**: Only what's needed for functionality
- **No telemetry**: No usage analytics or tracking
- **User control**: Users manage their own credentials
- **Third-party data**: Only Tidal API, no other services

### Open Source Security
- **OpenSSF Best Practices**: Following badge criteria
- **CWE Top 25**: Addressing common weaknesses
- **OWASP Top 10**: Preventing web vulnerabilities

### License Compliance
- MIT License
- Compatible dependency licenses
- No GPL/AGPL dependencies (Dependabot blocks these)

## Security Contacts

### Primary Contact
- **Email**: xfear26@hotmail.com
- **GitHub**: @RicherTunes

### Security Team
- Open to community security researchers
- Coordinated disclosure welcomed
- Acknowledgments in SECURITY.md

## Acknowledgments

We appreciate security researchers who responsibly disclose vulnerabilities. Contributors will be acknowledged here with permission:

- [Your name here for responsible disclosure]

## Additional Resources

- [Tidal Developer Documentation](https://developer.tidal.com/)
- [Lidarr Security Guidelines](https://wiki.servarr.com/lidarr)
- [Lidarr.Plugin.Common Security](https://github.com/RicherTunes/Lidarr.Plugin.Common/blob/main/SECURITY.md)
- [OWASP Secure Coding Practices](https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/)
- [CWE/SANS Top 25](https://cwe.mitre.org/top25/archive/2023/2023_top25_list.html)

---

**Last Updated**: 2026-03-10
**Version**: 1.0
**Maintained By**: RicherTunes
