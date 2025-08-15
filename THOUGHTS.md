## What things did you considered of during the implementation?

1. Architecture and Design Patterns

    1.1 Separation of Responsibilities

    1.2 Patterns:
    - Repository Pattern
    - Strategy Pattern
    - Factory Pattern
    - Pub Sub, Dep. Injection Pattern

2. Resilience and Production Quality

    2.1 Idempotency

    2.2 Retry Logic

    2.3 Concurrency Control

3. Observability and Auditing

    3.1 Structured Logging

    3.2 Auditing

4. Docker Configuration

    4.1 Use of containers to scale easy

    4.2 Azure Service Bus Emulator

    4.3 SQL Server database, container with persistence

5. Ease of Use

    5.1 Smart Makefile

    5.2 Message Publisher

## Anything was unclear?

    1. Deployment Model: 
        How will the system be deployed in production (Kubernetes, Docker Swarm)?
        Horizontal scaling strategies were not addressed

    2. Backup and Recovery
        No defined backup strategy for data
        No disaster recovery plan for catastrophic failures

    3. Security
        No secrets management implemented (Azure Key Vault, HashiCorp Vault)
        No authentication/authorization between services

## Aspects That Could Be Improved 
#### with more time and effort on it

1. Current Implementation Limitations

    - Missing Circuit Breaker: no protection against consecutive database failures; system may keep retrying indefinitely in degradation scenarios.

    - Limited Metrics: no integration with monitoring systems (Prometheus, Application Insights), lack of business metrics (TPS, latency, error rate).

    - Hardcoded Configuration: some values still hardcoded (retry delays, timeouts); should be externalized via configuration

2. Recommended Future Improvements

    - Performance: Optimized connection pooling for high concurrency; Data partitioning by account ID; Read replicas for non-transactional queries

    - Monitoring: More robust health checks; Dead letter queue monitoring and alerting; Business metrics dashboards

    - Scalability: Auto-scaling based on queue depth; Sharding strategy for multiple instances; Event sourcing for full audit trail