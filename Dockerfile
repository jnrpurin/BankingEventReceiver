# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY BankingApi.EventReceiver/BankingApi.EventReceiver.csproj BankingApi.EventReceiver/
RUN dotnet restore "BankingApi.EventReceiver/BankingApi.EventReceiver.csproj"

# Copy source code and build
COPY BankingApi.EventReceiver/ BankingApi.EventReceiver/
WORKDIR /src/BankingApi.EventReceiver
RUN dotnet build "BankingApi.EventReceiver.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "BankingApi.EventReceiver.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app

# Copy published files
COPY --from=publish /app/publish .

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN chown -R appuser:appuser /app
USER appuser

# Set environment variables
ENV DOTNET_ENVIRONMENT=Production

# Health check using dotnet process
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD pgrep -f "BankingApi.EventReceiver" > /dev/null || exit 1

# Entry point
ENTRYPOINT ["dotnet", "BankingApi.EventReceiver.dll"]