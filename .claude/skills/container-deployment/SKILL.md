---
name: container-deployment
description: Build complete containerization strategy for Tidalarr from scratch. Use when working with Docker, container registries, deployment automation, or Kubernetes orchestration. Critical for establishing container infrastructure.
---

# Container & Deployment Engineer

## Mission
Build complete containerization and deployment infrastructure for Tidalarr from the ground up.

## Current Status
- **Containerization**: ❌ CRITICAL - No containers exist
- **Deployment**: ⚠️ Manual only (deploy-plugin.ps1 for Docker containers)
- **Images**: ❌ Not published
- **Orchestration**: ❌ None

## Critical Missing Components
1. Dockerfile for Lidarr + Tidalarr
2. Container build workflow
3. GHCR publishing
4. Multi-architecture support
5. Docker Compose examples
6. Helm charts

## Quick Win: Leverage Existing Docker Deployment Script
- `scripts/deploy-plugin.ps1` exists for deploying to Docker containers
- Can be integrated into container build process

## Implementation Roadmap

### Phase 1: Dockerfile Creation
```dockerfile
FROM ghcr.io/hotio/lidarr:pr-plugins-2.14.2.4786
LABEL org.opencontainers.image.source="https://github.com/RicherTunes/tidalarr"
COPY artifacts/Tidalarr-*.zip /tmp/
RUN unzip /tmp/Tidalarr-*.zip -d /config/plugins/tidalarr/ && rm /tmp/Tidalarr-*.zip
HEALTHCHECK CMD curl -f http://localhost:8686/ping || exit 1
EXPOSE 8686
```

### Phase 2: Build Workflow
```yaml
# .github/workflows/container-build.yml
name: Container Build
on:
  push:
    tags: ['v*']
jobs:
  build-push:
    runs-on: ubuntu-latest
    steps:
      - uses: docker/build-push-action@v5
        with:
          platforms: linux/amd64,linux/arm64
          push: true
          tags: ghcr.io/richertunes/tidalarr:latest
```

### Phase 3: Documentation
- Update docs with container deployment
- Add docker-compose.yml examples
- Document environment variables

## Related Skills
- `release-automation` - Coordinate container + release
- `deployment-manager` - Automate deployments
