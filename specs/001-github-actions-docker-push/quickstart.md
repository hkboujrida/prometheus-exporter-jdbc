# Quickstart: Building & Pushing Docker Images

This feature automates the creation of Docker images for the project.

## How it Works

The system listens for changes to the `main` branch. When code is pushed, a GitHub Action automatically:
1. Checks out the code.
2. Builds the Docker image.
3. Logs into the GitHub Container Registry.
4. Pushes the image with version tags.

## Manual Trigger

You can manually trigger a build if needed (e.g., to re-run a failed build without a new commit):

1. Go to the **Actions** tab in the repository.
2. Select **Build and Push Docker Image** from the sidebar.
3. Click **Run workflow**.
4. Select the branch (usually `main`).
5. Click the green **Run workflow** button.

## Verification

To verify a successful push:

1. Navigate to the repository's main page.
2. Look for the **Packages** section (usually on the right sidebar).
3. Click on the package name.
4. Verify that a new version (tag) corresponds to your latest commit or action.

## Troubleshooting

- **Build Failed?**: Check the Actions logs. Common issues include Dockerfile syntax errors or failing commands.
- **Push Failed?**: Ensure the workflow has `packages: write` permissions (configured in the YAML).
