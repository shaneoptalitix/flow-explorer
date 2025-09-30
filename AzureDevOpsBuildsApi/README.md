## Key Behavior Changes

- **Latest Deployment Only**: Returns only the most recent deployment record per environment (after applying filters)
- **Default Result Filter**: Automatically filters to "succeeded" deployments unless explicitly overridden
- **Improved Performance**: Significantly faster due to processing only the latest deployment per environment
- **Enhanced Logging**: Detailed logging shows filtering steps and counts at each stage# Azure DevOps Environment Reporter

A C# Web API that queries Azure DevOps APIs to retrieve environment deployment information including builds, with filtering and sorting capabilities.

## Features

- Retrieves environments from Azure DevOps
- Gets deployment records for each environment (only the latest per environment after filtering)
- Fetches build details using owner.id as buildId
- Supports filtering by environment name, stage name, and deployment result
- **Defaults to showing only "succeeded" deployments** unless otherwise specified
- Sorts results by deployment finish time (descending)
- Swagger documentation for easy testing
- Proper error handling and logging

## Project Structure

```
AzureDevOpsReporter/
├── Controllers/
│   └── EnvironmentReportController.cs
├── Services/
│   ├── IAzureDevOpsService.cs
│   └── AzureDevOpsService.cs
├── Models/
│   └── AzureDevOpsModels.cs
├── Configuration/
│   └── AzureDevOpsConfig.cs
├── Program.cs
├── AzureDevOpsReporter.csproj
├── appsettings.json
└── README.md
```

## Configuration

Update the `appsettings.json` file with your Azure DevOps details:

```json
{
  "AzureDevOps": {
    "Organization": "your-organization-name",
    "Project": "your-project-name", 
    "PersonalAccessToken": "your-pat-token",
    "ApiVersion": "7.1-preview.1"
  }
}
```

### Getting a Personal Access Token

1. Go to Azure DevOps → User Settings → Personal Access Tokens
2. Create new token with the following scopes:
   - **Build**: Read
   - **Environment**: Read
   - **Release**: Read

## API Endpoints

### GET /api/EnvironmentReport

Retrieves environment reports with optional filtering.

**Query Parameters:**
- `environmentName` (optional): Filter by environment name (partial match, case-insensitive)
- `stageName` (optional): Filter by deployment stage name (partial match, case-insensitive)
- `result` (optional, defaults to "succeeded"): Filter by deployment result (exact match, case-insensitive)

**Response:**
Returns an array of environment reports with the following structure:

```json
[
  {
    "environmentId": 1,
    "environmentName": "Production",
    "environmentLastModifiedBy": "user@domain.com",
    "environmentLastModifiedOn": "2024-09-23T10:00:00Z",
    "deploymentRecordId": 100,
    "deploymentRecordEnvironmentId": 1,
    "deploymentRecordStageName": "Deploy to Production",
    "deploymentRecordDefinitionId": 50,
    "deploymentRecordDefinitionName": "MyApp-CD",
    "deploymentRecordOwnerId": 200,
    "deploymentRecordOwnerName": "Build 20240924.1",
    "deploymentRecordResult": "succeeded",
    "deploymentRecordFinishTime": "2024-09-24T08:00:00Z",
    "buildId": 200,
    "buildNumber": "20240924.1",
    "buildStatus": "completed",
    "buildStartTime": "2024-09-24T07:00:00Z",
    "buildSourceBranch": "refs/heads/main",
    "buildSourceVersion": "abc123def456",
    "buildTriggerMessage": "Updated feature X",
    "buildTriggerRepository": "MyApp"
  }
]
```

### GET /api/EnvironmentReport/sample

Returns a sample environment report structure for documentation purposes.

## Running the Application

1. **Clone or create the project structure** with all the files above
2. **Configure your settings** in `appsettings.json`
3. **Install dependencies**:
   ```bash
   dotnet restore
   ```
4. **Run the application**:
   ```bash
   dotnet run
   ```
5. **Access Swagger UI**: Navigate to `https://localhost:{port}` (port will be shown in console)

## Usage Examples

### Get all environment reports
```
GET /api/EnvironmentReport
```

### Get all environment reports (defaults to succeeded results)
```
GET /api/EnvironmentReport
```

### Filter by environment name (defaults to succeeded results)
```
GET /api/EnvironmentReport?environmentName=production
```

### Filter by stage and result
```
GET /api/EnvironmentReport?stageName=deploy&result=failed
```

### Combined filters
```
GET /api/EnvironmentReport?environmentName=prod&stageName=deploy&result=succeeded
```

### Get all results (not just succeeded)
```
GET /api/EnvironmentReport?result=
```

## Dependencies

- .NET 8.0
- Microsoft.AspNetCore.OpenApi (8.0.10)
- Swashbuckle.AspNetCore (6.8.1)
- System.Text.Json (8.0.5)
- Microsoft.Extensions.Http (8.0.1)

## Error Handling

The API includes comprehensive error handling:
- Missing builds are handled gracefully (reports still generated without build data)
- HTTP errors are logged and handled appropriately
- Configuration errors are caught at startup
- Invalid API responses are handled

## Logging

The application includes structured logging for:
- API requests and responses
- Azure DevOps API calls
- Error conditions
- Processing statistics

## Security Notes

- Store your Personal Access Token securely
- Consider using Azure Key Vault for production deployments
- The PAT should have minimal required permissions
- Use HTTPS in production

# Deploy Azure DevOps API to Azure App Service

## Prerequisites

- Azure subscription
- Visual Studio Code with extensions:
  - **Azure App Service** extension (ms-azuretools.vscode-azureappservice)
  - **C# Dev Kit** extension (ms-dotnettools.csdevkit)
- Azure CLI installed ([Download](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli))
- .NET 8.0 SDK installed

---

## Option 1: Deploy via VS Code Azure Extension (Easiest)

### Step 1: Install Required Extensions

1. Open VS Code
2. Go to Extensions (Ctrl+Shift+X / Cmd+Shift+X)
3. Search and install:
   - **Azure App Service** (by Microsoft)
   - **Azure Account** (by Microsoft)
   - **C# Dev Kit** (by Microsoft)

### Step 2: Sign in to Azure

1. Press `F1` or `Ctrl+Shift+P` (Cmd+Shift+P on Mac)
2. Type: `Azure: Sign In`
3. Follow the browser authentication flow
4. Close browser when complete

### Step 3: Publish the Project

```bash
# In VS Code terminal (Ctrl+` or Cmd+`)
cd AzureDevOpsBuildsApi
dotnet publish -c Release -o ./publish
```

### Step 4: Deploy Using Azure Extension

1. **Open Azure Extension**:
   - Click Azure icon in left sidebar (Activity Bar)
   - Expand "App Service" section

2. **Create and Deploy**:
   - Right-click on your subscription
   - Select **"Create New Web App... (Advanced)"**
   
3. **Configuration Wizard**:
   - **Enter globally unique name**: `azdo-reporter-api-<yourname>`
   - **Select runtime stack**: `.NET 8 (LTS)`
   - **Select OS**: `Linux` (cheaper) or `Windows`
   - **Select resource group**: Create new → `azdo-reporter-rg`
   - **Select App Service plan**: Create new → `azdo-reporter-plan`
     - Select location: `East US` (or preferred)
     - Select pricing tier: `Basic B1` or `Free F1` for testing
   - **Skip Application Insights**: For now (can add later)

4. **Deploy Application**:
   - After creation, right-click on the new Web App
   - Select **"Deploy to Web App..."**
   - Browse to the `AzureDevOpsBuildsApi/publish` folder
   - Click **Deploy**
   - Confirm when prompted

5. **Wait for Deployment**:
   - Watch output window for progress
   - You'll see "Deployment successful" when complete

### Step 5: Configure Application Settings

**Method A: Via VS Code**
1. In Azure extension, expand your Web App
2. Right-click **"Application Settings"**
3. Select **"Add New Setting"**
4. Add each setting:
   ```
   AzureDevOps__Organization = your-organization
   AzureDevOps__Project = your-project
   AzureDevOps__PersonalAccessToken = your-pat-token
   AzureDevOps__ApiVersion = 7.1
   ```

**Method B: Via Command Palette**
1. Press `F1`
2. Type: `Azure App Service: Edit Setting`
3. Follow prompts for each setting

### Step 6: Restart and Test

1. Right-click your Web App in Azure extension
2. Select **"Restart"**
3. Wait for restart
4. Right-click again → **"Browse Website"**
5. Your API should open in browser at `https://azdo-reporter-api-<yourname>.azurewebsites.net`