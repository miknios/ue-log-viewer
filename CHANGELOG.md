# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-11-25

First public release.

### Added

- Live tailing of Unreal Engine log files, with detection of a new engine run (log rotated or truncated).
- Category list built from the file, with line counts, show-only, hide, All / None and a "show new categories" default.
- Error / Warning / Log level toggles with counts.
- Parsing of the standard `Category: Verbosity: message` line format and the compact `Category:Verbosity:` form with a thread id block.
- Substring and regex search.
- Follow-end mode that keeps working while filters are active.
- Multi-line messages (callstacks, dumps) grouped with the line that started them.
- Timestamp prefix toggle, multi-line copy, and Clear.
- Portable settings stored next to the executable.

[1.0.0]: https://github.com/miknios/ue-log-viewer/releases/tag/v1.0.0
