# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-11

### Added
- Initial release of `com.setsuodu.bugreport`.
- `BugReporter` MonoBehaviour: local persistent queue, periodic flush, offline retry.
- C# layer capture via `Application.logMessageReceived` and `AppDomain.UnhandledException`.
- `ReportSender` for ingest reports and attachment init/complete APIs.
- Device info collector (platform, OS, model, device id).
- Editor menu: Tools → BugReport → Create BugReporter GameObject.
- Sample: `ErrorTriggers` with intentional Error / Exception / Crash-style methods for end-to-end testing.
