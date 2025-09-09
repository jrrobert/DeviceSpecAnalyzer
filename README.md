# Device Specification Analyzer

A web-based tool for analyzing and comparing medical device specification documents to accelerate driver development.

## Prerequisites

### Install .NET SDK

1. Download .NET 9.0 SDK from: https://dotnet.microsoft.com/download/dotnet/9.0
2. Choose the installer for macOS ARM64
3. Run the installer and follow the prompts
4. Verify installation by running: `dotnet --version`

### Alternative Installation via Terminal

If you prefer terminal installation:
```bash
# Download and install .NET SDK
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest --channel 9.0
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

## Project Structure

```
DeviceSpecAnalyzer/
├── DeviceSpecAnalyzer.sln                  # Solution file
├── src/
│   ├── DeviceSpecAnalyzer.Web/             # Blazor Server web application
│   ├── DeviceSpecAnalyzer.Core/            # Business logic and services
│   ├── DeviceSpecAnalyzer.Data/            # Data access layer with EF Core
│   └── DeviceSpecAnalyzer.Processing/      # Document processing and NLP
├── tests/
│   ├── DeviceSpecAnalyzer.Tests.Unit/      # Unit tests
│   └── DeviceSpecAnalyzer.Tests.Integration/ # Integration tests
├── docs/                                   # Documentation
├── scripts/                                # Build and deployment scripts
└── SpecificationRepository/                # Document storage directory
    ├── POCT1A/
    ├── ASTM/
    ├── HL7/
    └── Unprocessed/
```

## Features

- **Document Repository Management**: Organize specification PDFs by protocol type
- **Automatic Text Extraction**: Extract and index content from PDF specifications
- **Similarity Analysis**: Compare new specifications against existing documents
- **Protocol-Specific Parsing**: Understand POCT1A, ASTM, and HL7 message structures
- **Web-Based Interface**: Team-accessible Blazor application
- **Background Processing**: Asynchronous document analysis
- **Export Capabilities**: Generate comparison reports

## Getting Started

After installing .NET SDK:

1. Create the solution and projects:
   ```bash
   cd /Users/jayrobertson/Documents/GitHub/DeviceSpecAnalyzer
   dotnet new sln
   # Project creation commands will be provided once .NET is installed
   ```

2. Restore packages and build:
   ```bash
   dotnet restore
   dotnet build
   ```

3. Run the application:
   ```bash
   cd src/DeviceSpecAnalyzer.Web
   dotnet run
   ```

## Technology Stack

- **Backend**: ASP.NET Core 9.0 with C#
- **Frontend**: Blazor Server
- **Database**: SQLite with Entity Framework Core
- **PDF Processing**: PdfPig (MIT license)
- **NLP**: ML.NET and custom TF-IDF implementation
- **Authentication**: ASP.NET Core Identity