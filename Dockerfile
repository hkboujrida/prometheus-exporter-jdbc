# Build the .NET exporter
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PromExporter.slnx ./
COPY src/PromExporter.Jdbc/PromExporter.Jdbc.csproj src/PromExporter.Jdbc/
RUN dotnet restore src/PromExporter.Jdbc/PromExporter.Jdbc.csproj

COPY src/PromExporter.Jdbc/ src/PromExporter.Jdbc/
RUN dotnet publish src/PromExporter.Jdbc/PromExporter.Jdbc.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# curl for HEALTHCHECK only; non-root runtime user
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd -r -u 10001 exporter \
    && chown -R exporter:exporter /app

# Optional IBM i connectivity: installs the proprietary IBM i Access ODBC
# driver (+ unixODBC, registered as "IBM i Access ODBC Driver") from IBM's
# public repo so `provider: odbc` works out of the box.
# Build with: docker build --build-arg ACS_ODBC=true .
ARG ACS_ODBC=false
RUN if [ "$ACS_ODBC" = "true" ]; then \
      curl -fsSL https://public.dhe.ibm.com/software/ibmi/products/odbc/debs/dists/1.1.0/ibmi-acs-1.1.0.list \
        -o /etc/apt/sources.list.d/ibmi-acs.list \
      && apt-get update \
      && apt-get install -y --no-install-recommends ibm-iaccess unixodbc \
      && rm -rf /var/lib/apt/lists/*; \
    fi
USER exporter

EXPOSE 9853
ENV PORT=9853
ENV ASPNETCORE_URLS=

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s \
    CMD curl -fs "http://127.0.0.1:${PORT}/metrics" || exit 1

ENTRYPOINT ["dotnet", "PromExporter.Jdbc.dll"]
