# CodeClone Desktop

**Detect and track code duplication across your codebase.**

## Overview

CodeClone Desktop is a Windows desktop application that surfaces code clones, duplicate patterns, and structural redundancies in your projects. It parses output from the `codeclone` CLI analyzer and presents findings through an interactive dashboard with severity scoring, trend tracking, and actionable fix suggestions.

### Key Features

- **Clone Analysis**: Parse and display results from the `codeclone` CLI tool
- **Severity Scoring**: Diagnostics ranked by impact with evidence snippets
- **Trend Snapshots**: Point-in-time captures with git metadata for tracking improvements over time
- **Dashboard Metrics**: High-level duplication percentages and risk scores for quick assessment
- **Action Items**: Prioritized fix recommendations with impact scores
- **Local-first**: All analysis runs locally — no data leaves your machine

## NuGet Packages

| Package | Description |
|---------|-------------|
| [CodeClone.Domain](https://www.nuget.org/packages/CodeClone.Domain) | Domain models for code clone analysis — parse results, diagnostics, severity-scored insights, trend snapshots, and dashboard metrics. AOT-ready with source-generated JSON. |

```bash
dotnet add package CodeClone.Domain
```

## Building from Source

```powershell
# Prerequisites: .NET 9.0 SDK, Windows 10+, VS 2022 with MAUI workload

git clone https://github.com/mcp-tool-shop-org/CodeClone-Desktop.git
cd CodeClone-Desktop
dotnet build
```

## Project Structure

```
CodeClone-Desktop/
├── src/
│   ├── CodeClone.App/       # MAUI desktop app (UI, ViewModels)
│   └── CodeClone.Domain/    # Domain models and JSON parsing
└── ...
```

## License

MIT License - See [LICENSE](LICENSE) for details.
