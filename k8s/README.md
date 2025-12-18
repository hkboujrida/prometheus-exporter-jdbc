# Prometheus JDBC Exporter - Kubernetes Deployment

This directory contains Kubernetes manifests for deploying the Prometheus JDBC Exporter.

## Prerequisites

- Kubernetes cluster
- kubectl configured
- Docker image built and available (update the image reference in deployment.yaml)

## Deployment Steps

1. **Update the Secret with your credentials:**

   Edit `secret.yaml` and replace the base64 encoded values with your actual credentials:

   ```bash
   # Encode your values
   echo -n 'YOUR_USERNAME' | base64
   echo -n 'YOUR_PASSWORD' | base64
   echo -n 'YOUR_HOSTNAME' | base64
   ```

   Update the `data` section in `secret.yaml` with these values.

2. **Update the image reference in deployment.yaml:**

   Change `prometheus-exporter-jdbc:latest` to your actual image location (e.g., `your-registry/prometheus-exporter-jdbc:v1.0.3`)

3. **Deploy to Kubernetes:**

   ```bash
   kubectl apply -f k8s/
   ```

4. **Verify deployment:**

   ```bash
   kubectl get pods
   kubectl get services
   ```

5. **Access metrics:**

   The service exposes metrics on port 9853. You can access it via:

   ```bash
   kubectl port-forward svc/prometheus-exporter-jdbc-service 9853:9853
   ```

   Then visit `http://localhost:9853/metrics` in your browser.

## Configuration

- **Secret**: Contains sensitive credentials (username, password, hostname)
- **ConfigMap**: Contains the exporter configuration (queries, intervals, etc.)
- **Deployment**: Runs the application with proper resource limits
- **Service**: Exposes the metrics endpoint within the cluster

## Security Notes

- The Secret contains sensitive database credentials
- Consider using external secret management (e.g., AWS Secrets Manager, Vault) in production
- The service is exposed as ClusterIP by default - change to LoadBalancer or use Ingress if external access is needed