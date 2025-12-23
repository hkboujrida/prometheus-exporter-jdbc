# Quickstart: Setup Azure DevOps Pipeline

## Prerequisites
1. **Azure DevOps Project**: Access to an active project.
2. **Harbor Registry**: URL and credentials.
3. **Variable Group**: Create a group named `HarborCredentials` with:
   - `HARBOR_URL`
   - `HARBOR_USERNAME`
   - `HARBOR_PASSWORD` (secret)

## Setup Steps

1. **Commit Pipeline**: Ensure `azure-pipelines-helm.yml` is in the repository root.
2. **Create Pipeline**:
   - Go to Azure DevOps > Pipelines.
   - Click "New Pipeline".
   - Select "Azure Repos Git".
   - Select this repository.
   - Select "Existing Azure Pipelines YAML file".
   - Select `/azure-pipelines-helm.yml`.
3. **Run Pipeline**:
   - Save and Run.
   - Grant permission to the Variable Group if prompted.

## Verification
1. Check Pipeline logs for "Push to Harbor" step.
2. Log in to Harbor and verify the chart exists in the target project.
