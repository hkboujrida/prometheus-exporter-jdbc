# Azure DevOps Pipeline Setup for Harbor Registry

This document explains how to set up the Azure DevOps pipeline to build and push Docker images to a Harbor registry.

## Prerequisites

1. **Azure DevOps Organization and Project**
2. **Harbor Registry Access**
   - Harbor registry URL (e.g., `harbor.example.com`)
   - Harbor project name
   - Credentials (username/password or robot account)

## Setup Steps

### 1. Create Service Connection for Harbor

1. In your Azure DevOps project, go to **Project Settings** > **Service connections**
2. Click **New service connection** > **Docker Registry**
3. Select **Others** as the registry type
4. Fill in the details:
   - **Docker Registry**: `https://your-harbor-registry.com`
   - **Docker ID**: Your Harbor username
   - **Password**: Your Harbor password or robot account token
   - **Service connection name**: `harbor-connection`

### 2. Configure Pipeline Variables

In your pipeline, go to **Edit** > **Variables** and add/update these variables:

- `harborRegistry`: Your Harbor registry URL (e.g., `harbor.example.com`)
- `harborProject`: Your Harbor project name

### 3. Update Pipeline File

The `azure-pipelines.yml` file is already configured with:
- Build triggers on main branch pushes
- Docker build and push steps
- Harbor registry authentication
- Proper tagging strategy

### 4. Run the Pipeline

1. Commit and push the `azure-pipelines.yml` file to your repository
2. The pipeline will automatically trigger on pushes to the main branch
3. Monitor the build in Azure DevOps Pipelines

## Pipeline Features

- **Automated Builds**: Triggers on every push to main branch
- **Multi-tag Support**: Creates both versioned and latest tags
- **Harbor Integration**: Pushes to your Harbor registry
- **Clean Tagging**: Uses build numbers for versioning

## Usage in Kubernetes

Once images are pushed to Harbor, update your Kubernetes deployment to use:

```yaml
image: your-harbor-registry.com/your-project/prometheus-exporter-jdbc:latest
```

## Security Notes

- Store Harbor credentials securely using Azure DevOps service connections
- Consider using Harbor robot accounts for CI/CD pipelines
- Regularly rotate credentials and review access permissions

## Troubleshooting

- **Authentication Issues**: Verify service connection credentials
- **Registry URL**: Ensure the Harbor URL is correct and accessible
- **Project Permissions**: Confirm your Harbor user has push permissions to the project