# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY MessagePublisher.csproj .
RUN dotnet restore "MessagePublisher.csproj"

# Copy source code and build
COPY . .
RUN dotnet build "MessagePublisher.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MessagePublisher.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy published files
COPY --from=publish /app/publish .

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN chown -R appuser:appuser /app
USER appuser

# Entry point
ENTRYPOINT ["dotnet", "MessagePublisher.dll"]