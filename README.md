# credfeto-nuget-proxy
Simple Caching NuGet proxy server

Caches packages rather than metadata, but returns the metadata with caching headers so that an downstream cache can cache the responses for a longer time.

Rewrites the urls in the metadata for upstream urls to point to the public name of the server.

## Build Status

| Branch  | Status                                                                                                                                                                                                                                |
|---------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| main    | [![Build: Pre-Release](https://github.com/credfeto/credfeto-nuget-proxy/actions/workflows/build-and-publish-pre-release.yml/badge.svg)](https://github.com/credfeto/credfeto-nuget-proxy/actions/workflows/build-and-publish-pre-release.yml) |
| release | [![Build: Release](https://github.com/credfeto/credfeto-nuget-proxy/actions/workflows/build-and-publish-release.yml/badge.svg)](https://github.com/credfeto/credfeto-nuget-proxy/actions/workflows/build-and-publish-release.yml)             |

## Changelog

View [changelog](CHANGELOG.md)

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
