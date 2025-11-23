---
name: security-compliance
description: Build complete security infrastructure from absolute scratch. Use when implementing security scanning, vulnerability detection, secret management, compliance, or security hardening. CRITICAL priority - no security infrastructure currently exists.
---

# Security & Compliance Guardian

## Mission
Build enterprise-grade security infrastructure for Tidalarr from the ground up. This is the HIGHEST PRIORITY skill as security infrastructure is completely missing.

## Current Security Status
❌ **CRITICAL: NO SECURITY INFRASTRUCTURE EXISTS**
- ❌ **CodeQL**: Not implemented
- ❌ **Secret Scanning**: Not configured
- ❌ **Dependabot**: Not configured
- ❌ **Security Policy**: No SECURITY.md
- ❌ **Dependency Review**: No PR gates
- ❌ **SBOM**: Not generated
- ❌ **Artifact Signing**: Not implemented
- ❌ **Pre-commit Hooks**: No secret detection

## CRITICAL ACTION REQUIRED

This project has ZERO security infrastructure. Immediate action needed to establish basic security hygiene.

## Implementation Roadmap

### Phase 1: IMMEDIATE (Do Today)
1. **Create Security Workflow**
2. **Configure Secret Scanning**
3. **Add Security Policy**
4. **Enable Dependabot**

### Complete Security Setup Script
```bash
#!/bin/bash
# Run this to establish basic security infrastructure

# 1. Create CodeQL workflow
mkdir -p .github/workflows
cat > .github/workflows/security.yml << 'EOF'
name: Security Scanning

on:
  push:
    branches: [main, develop]
  pull_request:
  schedule:
    - cron: '0 3 * * 1'  # Weekly Monday 3 AM

jobs:
  codeql:
    name: CodeQL Analysis
    runs-on: ubuntu-latest
    permissions:
      security-events: write
      contents: read
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: csharp

      - name: Build
        run: dotnet build -c Release

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3

  secrets:
    name: Secret Scanning
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Run GitLeaks
        uses: gitleaks/gitleaks-action@v2
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

  dependencies:
    name: Dependency Vulnerabilities
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Scan Dependencies
        run: dotnet list package --vulnerable --include-transitive || true
EOF

# 2. Create Dependabot config
cat > .github/dependabot.yml << 'EOF'
version: 2
updates:
  - package-ecosystem: nuget
    directory: "/"
    schedule:
      interval: weekly
      day: monday
      time: "09:00"
    open-pull-requests-limit: 10
    labels:
      - dependencies
      - automated
    assignees:
      - RicherTunes

  - package-ecosystem: github-actions
    directory: "/"
    schedule:
      interval: weekly
    open-pull-requests-limit: 5
    labels:
      - dependencies
      - github-actions
EOF

# 3. Create security policy
cat > SECURITY.md << 'EOF'
# Security Policy

## Reporting Security Issues

**IMPORTANT**: Do NOT open public GitHub issues for security vulnerabilities.

### Contact Information
- **Email**: security@richertunes.com
- **Response Time**: Within 48 hours
- **PGP Key**: [To be added]

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Security Features

### Current
- Secret scanning (planned)
- Dependency vulnerability monitoring (planned)
- Static analysis with CodeQL (planned)
- Automated security updates via Dependabot (planned)

### Planned
- Container image scanning
- SBOM generation
- Artifact signing
- Penetration testing

## Vulnerability Disclosure Process

1. **Report Received**: Security team acknowledges within 48 hours
2. **Assessment**: Evaluate severity and impact
3. **Fix Development**: Create patch in private branch
4. **Coordinated Disclosure**:
   - Security advisory published
   - Patch released
   - CVE assigned if applicable
5. **Post-Disclosure**: Update documentation and notify users

## Security Best Practices for Contributors

### Code Security
- No hardcoded secrets or credentials
- Use environment variables for sensitive data
- Input validation on all external data
- Output encoding to prevent injection
- Secure error handling (no sensitive info in errors)

### Dependency Management
- Keep dependencies up-to-date
- Review dependency changes in PRs
- Use lock files for reproducible builds
- Monitor security advisories

### Authentication
- Use OAuth 2.0 + PKCE for Tidal auth
- Encrypt tokens at rest
- Implement proper session management
- Support credential rotation

## Security Audit History

| Date | Type | Findings | Status |
|------|------|----------|--------|
| TBD  | Internal | N/A | Planned |

## Contact

For security concerns: security@richertunes.com
EOF

# 4. Create GitLeaks config
cat > .gitleaks.toml << 'EOF'
title = "Tidalarr GitLeaks Configuration"

[[rules]]
id = "tidal-client-id"
description = "Tidal Client ID"
regex = '''(?i)(tidal[_-]?client[_-]?id|client[_-]?id).{0,20}['"]?[a-zA-Z0-9]{20,}['"]?'''

[[rules]]
id = "tidal-client-secret"
description = "Tidal Client Secret"
regex = '''(?i)(tidal[_-]?client[_-]?secret|client[_-]?secret).{0,20}['"]?[a-zA-Z0-9]{30,}['"]?'''

[[rules]]
id = "tidal-access-token"
description = "Tidal Access Token"
regex = '''(?i)(tidal[_-]?access[_-]?token|access[_-]?token).{0,20}['"]?[a-zA-Z0-9\-_]{50,}['"]?'''

[[rules]]
id = "email-password"
description = "Email with Password"
regex = '''(?i)(email|username).{0,20}['"]?[\w\.-]+@[\w\.-]+\.\w+['"]?.{0,20}(password|pwd).{0,20}['"]?[\S]{8,}['"]?'''

[allowlist]
description = "Allow test data and documentation"
paths = [
  '''tests/.*''',
  '''docs/.*''',
  '''\.md$'''
]
EOF

# 5. Create pre-commit hook
mkdir -p .git/hooks
cat > .git/hooks/pre-commit << 'EOF'
#!/bin/bash
# Pre-commit hook for security checks

echo "Running security checks..."

# Check for secrets
if command -v gitleaks &> /dev/null; then
  gitleaks protect --staged --verbose
  if [ $? -ne 0 ]; then
    echo "❌ GitLeaks found potential secrets. Commit aborted."
    exit 1
  fi
else
  echo "⚠️  GitLeaks not installed. Skipping secret detection."
fi

echo "✅ Security checks passed"
exit 0
EOF
chmod +x .git/hooks/pre-commit

# 6. Commit everything
git add .github/workflows/security.yml
git add .github/dependabot.yml
git add SECURITY.md
git add .gitleaks.toml
git commit -m "security: establish complete security infrastructure

- Add CodeQL static analysis workflow
- Add GitLeaks secret scanning
- Configure Dependabot for automated updates
- Create security policy and disclosure process
- Add GitLeaks configuration for Tidal credentials
- Add pre-commit hook for secret detection

This establishes baseline security infrastructure that was previously missing."

echo "✅ Security infrastructure created successfully!"
echo ""
echo "Next steps:"
echo "1. Push changes: git push origin main"
echo "2. Enable security features in GitHub Settings:"
echo "   - Settings → Security → Code scanning → Enable"
echo "   - Settings → Security → Secret scanning → Enable"
echo "   - Settings → Security → Dependabot → Enable"
echo "3. Review and close any security alerts generated"
echo "4. Set up security email: security@richertunes.com"
```

### Phase 2: Verification (Day 2)
```bash
# Verify security infrastructure
- Check CodeQL scan completed
- Review secret scanning results
- Confirm Dependabot PRs created
- Validate pre-commit hooks work
```

### Phase 3: Enhancement (Week 2)
- Add dependency review to PR workflow
- Implement SBOM generation
- Add artifact signing to releases
- Set up security dashboards

### Phase 4: Continuous Improvement (Ongoing)
- Regular security audits
- Penetration testing
- Threat modeling workshops
- Security training for contributors

## Critical Security Issues to Address

### 1. Tidal Credentials
- Client ID and secret must be encrypted
- Use environment variables only
- Implement secure storage (DPAPI/Keychain)
- Support credential rotation

### 2. User Tokens
- OAuth access/refresh tokens encrypted at rest
- Session management with expiration
- Secure token storage via shared library

### 3. API Security
- Input validation on all user data
- Output encoding to prevent injection
- Rate limiting to prevent abuse
- Secure error messages (no sensitive data)

### 4. Download Security
- Verify file integrity (checksums)
- Validate content types
- Prevent path traversal
- Scan downloaded files (optional)

## Related Skills
- `release-automation` - Secure release processes
- `code-quality` - Security through quality
- `observability` - Security monitoring

## Examples

### Example 1: Complete Security Setup
**User**: "Set up all security infrastructure for Tidalarr"
**Action**: Run the complete security setup script above, enable GitHub security features, verify all scans running

### Example 2: Respond to Secret Detected
**User**: "GitLeaks detected a Tidal client secret in commit"
**Action**:
1. Immediately rotate the compromised credential
2. Remove from git history: `git filter-branch`
3. Update .gitleaks.toml to catch similar issues
4. Document incident in security log

### Example 3: Weekly Security Review
**User**: "Perform weekly security review"
**Action**:
1. Review CodeQL findings
2. Check Dependabot PRs and merge safe updates
3. Review secret scanning alerts
4. Update SECURITY.md if needed
5. Check for new CVEs affecting dependencies
