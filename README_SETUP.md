# Banking Event Receiver - Setup Guide

## Quick Start

### 1. Start the Full Stack
```bash
make docker-run
```
This will:
- Clean any previous environment
- Start SQL Server and Service Bus Emulator
- Wait for containers to be ready
- Run database migrations
- Start the message worker

### 2. Monitor/check the System
```bash
# Check container health
make check-health

# Monitor all containers
make monitor
```

### 3. Send Test Messages
```bash
# Start publisher in background
make start-publisher

# Interactive message publisher
make publish-messages
```

### 4. Cleanup
```bash
make clean
```

## Architecture Overview

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   SQL Server    │    │ Service Bus     │    │  Message        │
│   (Database)    │    │ Emulator        │    │  Worker         │
│                 │    │                 │    │                 │
│ - BankAccounts  │◄───┤ - Queues        │◄───┤ - Polling       │
│ - Transactions  │    │ - Dead Letter   │    │ - Processing    │
│ - Audit Logs    │    │ - Retry Logic   │    │ - Error Handling│
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Key Features

- **Production-Ready**: Proper error handling, retry logic, logging
- **Idempotent**: Duplicate message detection
- **Resilient**: Exponential backoff (5s, 25s, 125s)
- **Observable**: Structured logging with correlation IDs
- **Auditable**: Complete transaction audit trail
- **Containerized**: Full Docker environment with Azure Service Bus Emulator

## Test Scenarios

The system includes pre-seeded bank accounts:
- `7d445724-24ec-4d52-aa7a-ff2bac9f191d` - Initial Balance: $1,000.00
- `3bbaf4ca-5bfa-4922-a395-d755beac475f` - Initial Balance: $500.00  
- `f8e1a4b2-9c3d-4e5f-8a7b-1d2e3f4a5b6c` - Initial Balance: $2,500.00

## Available Commands

| Command | Description |
|---------|-------------|
| `make docker-run` | Start full stack |
| `make publish-messages` | Interactive message publisher |
| `make check-health` | Check container health |
| `make monitor` | Monitor all logs |
| `make clean` | Clean environment |
| `make migrate` | Run database migrations |

## Troubleshooting

### Container Issues
```bash
# Check all container status
docker-compose ps

# View specific container logs
docker-compose logs worker
docker-compose logs sqlserver
docker-compose logs servicebus-emulator
```

### Database Issues
```bash
# Test database connection
make db-query

# Access SQL Server directly
make shell-sql
```

### Message Processing Issues
```bash
# View worker logs in real-time
make logs-all
```