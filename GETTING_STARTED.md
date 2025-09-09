# Getting Started with Device Specification Analyzer

## Prerequisites

### 1. Install .NET SDK 9.0

#### Option 1: Download from Microsoft
1. Go to https://dotnet.microsoft.com/download/dotnet/9.0
2. Download the SDK installer for macOS ARM64
3. Run the installer and follow the prompts
4. Verify installation: `dotnet --version`

#### Option 2: Using Terminal (Homebrew required)
```bash
# Install .NET via Homebrew (requires sudo password)
brew install --cask dotnet

# Or use the official installer script
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest --channel 9.0
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

### 2. Verify .NET Installation
```bash
dotnet --version
# Should show: 9.0.x
```

## Project Setup

### 1. Navigate to Project Directory
```bash
cd /Users/jayrobertson/Documents/GitHub/DeviceSpecAnalyzer
```

### 2. Restore NuGet Packages
```bash
dotnet restore
```

### 3. Build the Solution
```bash
dotnet build
```

### 4. Database Setup

The application uses **SQLite** by default, which works cross-platform on macOS, Windows, and Linux.

- **No installation required** - SQLite is embedded and creates a local `DeviceSpecAnalyzer.db` file
- **Cross-platform** - Works on all operating systems  
- **Perfect for development** - Self-contained database file

If you prefer to use a different database (PostgreSQL, SQL Server, etc.):

1. Update the connection string in `src/DeviceSpecAnalyzer.Web/appsettings.json`
2. Update the database provider in `Program.cs` (e.g., `UseSqlServer()`, `UseNpgsql()`)

### 5. Run the Application
```bash
cd src/DeviceSpecAnalyzer.Web
dotnet run
```

The application will start and display the URLs where it's running (typically https://localhost:7001).

## First Run

### 1. Database Creation
The database will be automatically created on first run.

### 2. User Registration
1. Navigate to the application URL in your browser
2. Click "Register" to create your first user account
3. Fill in your email and password

### 3. Repository Setup
The application will automatically create the `SpecificationRepository` folder with subfolders:
- `POCT1A/` - For POCT1-A specification documents
- `ASTM/` - For ASTM protocol specifications  
- `HL7/` - For HL7 message specifications
- `Unprocessed/` - For documents that need manual classification

## Adding Specification Documents

### Method 1: File System (Automatic Processing)
1. Drop PDF files into the appropriate protocol folders in `SpecificationRepository/`
2. The system will automatically detect and process new files
3. Check the dashboard to see processing status

### Method 2: Web Upload
1. Navigate to the "Upload" page in the web interface
2. Drag and drop or select PDF files
3. Optionally provide manufacturer, device, and protocol information
4. Click "Upload & Process"

## Understanding the Analysis

### Document Processing
When a PDF is uploaded or detected:
1. **Text Extraction**: Content is extracted using PdfPig
2. **Protocol Detection**: POCT1-A, ASTM, or HL7 parsers analyze the content
3. **Section Parsing**: Key sections like message formats and data fields are identified
4. **Keyword Extraction**: Important terms are extracted for similarity analysis
5. **Similarity Calculation**: TF-IDF and cosine similarity are computed against existing documents

### Similarity Analysis
The system compares documents using:
- **Overall Similarity**: TF-IDF cosine similarity of full document text
- **Keyword Similarity**: Jaccard similarity of extracted keywords
- **Structural Similarity**: Protocol type, manufacturer, and document structure
- **Semantic Similarity**: Presence of common technical terms and concepts

### Results Interpretation
- **High Similarity (>80%)**: Documents are very similar, likely same device/protocol version
- **Moderate Similarity (60-80%)**: Significant overlap, may share common components
- **Low Similarity (30-60%)**: Some commonality, worth investigating for reusable patterns
- **Very Low Similarity (<30%)**: Documents are quite different

## Troubleshooting

### Common Issues

#### 1. .NET SDK Not Found
```bash
# Check if .NET is in PATH
which dotnet

# If not found, add to PATH
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

#### 2. Database Connection Issues
- SQLite database file (`DeviceSpecAnalyzer.db`) will be created automatically
- Check connection string in `appsettings.json`
- Ensure write permissions in the application directory
- Try running with verbose logging: `dotnet run --verbosity detailed`

#### 3. PDF Processing Errors
- Ensure PDF files are not corrupted
- Check file permissions on the SpecificationRepository folder
- Review logs for specific error messages

#### 4. Port Already in Use
```bash
# Find process using port 7001
lsof -ti:7001

# Kill the process (replace PID with actual process ID)
kill -9 <PID>

# Or run on different port
dotnet run --urls "https://localhost:7002;http://localhost:7003"
```

### Logging
View detailed logs by setting the log level in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "DeviceSpecAnalyzer": "Debug"
    }
  }
}
```

## Development Mode vs Production

### Development Features
- Detailed error pages
- Hot reload for code changes
- Verbose logging
- Database migrations applied automatically

### Production Considerations
- **For production use, consider upgrading to a server database:**
  - PostgreSQL (recommended for production)
  - SQL Server
  - MySQL/MariaDB
- Configure HTTPS certificates
- Set appropriate logging levels  
- Implement proper backup strategies for the document repository and SQLite database file
- For high-volume usage, migrate from SQLite to a dedicated database server

## Next Steps

1. **Add Sample Documents**: Upload some specification PDFs to see the system in action
2. **Explore Comparisons**: Use the comparison features to find similar documents
3. **Review Reports**: Check the reporting features for insights
4. **Configure Protocols**: Adjust protocol parsing rules for your specific document types
5. **Set Up Monitoring**: Configure logging and monitoring for production use

## Support

- Check the application logs for error details
- Review the README.md file for architecture information
- Verify all NuGet packages are properly restored
- Ensure the SpecificationRepository folder has proper read/write permissions