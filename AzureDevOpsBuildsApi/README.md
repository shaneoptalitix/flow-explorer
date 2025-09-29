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

# Docker

docker compose up -d

gcloud auth configure-docker europe-west2-docker.pkg.dev

docker tag azuredevopsbuildsapi-azuredevops-reporter europe-west2-docker.pkg.dev/optalitix-dataflow-dev/flow-explorer/azuredevopsbuildsapi-azuredevops-reporter:latest

docker push europe-west2-docker.pkg.dev/optalitix-dataflow-dev/flow-explorer/azuredevopsbuildsapi-azuredevops-reporter:latest

docker compose down

http://localhost:8080/index.html (Swagger)
http://localhost:7237/index.html (Swagger)
https://localhost:8443/api (API)
http://localhost:8081/index.html (Web app)

# Local VSCode Debug

http://localhost:8080/index.html (Swagger)
https://localhost:8443/api (API)
http://localhost:8081/index.html (Web app)