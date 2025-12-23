# Quickstart: Configuring Auth & Connection

This guide explains how to configure the database connection settings using the new Helm values.

## Option 1: Using an Existing Secret (Recommended for Prod)

1. **Create the Secret** manually:
   ```bash
   kubectl create secret generic my-db-cred --from-literal=db-password='superSecretPassword'
   ```

2. **Deploy** referencing the secret:
   ```yaml
   # values-prod.yaml
   env:
     hostname: "db-prod.internal"
     username: "app_user"
   
   secret:
     create: false
     name: "my-db-cred"
     key: "db-password"
   ```

   ```bash
   helm install my-exporter ./charts/prometheus-exporter-jdbc -f values-prod.yaml
   ```

## Option 2: Chart-Managed Secret (Quick Start / Dev)

Allow the chart to create the secret for you.

1. **Deploy** with password in values:
   ```yaml
   # values-dev.yaml
   env:
     hostname: "localhost"
     username: "dev_user"
   
   secret:
     create: true
     name: "exporter-auto-secret"
     password: "devPassword123"
   ```

   ```bash
   helm install my-exporter ./charts/prometheus-exporter-jdbc -f values-dev.yaml
   ```