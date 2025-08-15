.PHONY: docker-run clean migrate publish-messages logs stop test help

include .env
export

docker-run: clean
	docker-compose up -d sqlserver servicebus-emulator sqledge
	sleep 30
	$(MAKE) migrate
	docker-compose up -d worker

build:
	docker-compose build worker

clean:
	docker-compose down -v --remove-orphans
	docker system prune -f --volumes

migrate:
	@until docker-compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$(MSSQL_SA_PASSWORD)" -Q "SELECT 1" -C > /dev/null 2>&1; do \
		echo "Waiting for SQL Server to be ready"; \
		sleep 5; \
	done
	cd BankingApi.EventReceiver && dotnet ef database update --connection "Server=localhost,1435;Database=BankingApiTest;User Id=sa;Password=$(MSSQL_SA_PASSWORD);TrustServerCertificate=true;Encrypt=false;"

publish-messages: ## Simulate banking transactions (interactive mode)
	@echo "Starting interactive message publisher..."
	docker-compose --profile publisher run --rm message-publisher

start-publisher: ## Start message publisher in background
	@echo "Starting message publisher container..."
	docker-compose --profile publisher up -d message-publisher

test:
	@echo "Running integration tests..."
	cd BankingApi.EventReceiver && dotnet test

check-health:
	@echo "Checking container health..."
	docker-compose ps
	@echo "Worker container logs (last 20 lines):"
	docker-compose logs --tail=20 worker

monitor:
	docker-compose logs -f --tail=50

## SQL Server commands
shell-sql: 
	docker-compose exec sqlserver bash

db-query:
	docker-compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$(MSSQL_SA_PASSWORD)" -d BankingApiTest -Q "SELECT COUNT(*) as TotalAccounts FROM BankAccounts" -C
