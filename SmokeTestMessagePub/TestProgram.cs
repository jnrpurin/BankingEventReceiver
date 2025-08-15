using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace MessagePublisher;

public class TestProgram
{
    private static readonly List<BankAccountSample> SampleAccounts = new()
    {
        new() { Id = Guid.Parse("7d445724-24ec-4d52-aa7a-ff2bac9f191d"), InitialBalance = 1000.00m },
        new() { Id = Guid.Parse("3bbaf4ca-5bfa-4922-a395-d755beac475f"), InitialBalance = 500.00m },
        new() { Id = Guid.Parse("f8e1a4b2-9c3d-4e5f-8a7b-1d2e3f4a5b6c"), InitialBalance = 2500.00m }
    };

    public static async Task Main(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING")
            ?? "Endpoint=sb://servicebus-emulator:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

        var queueName = "banking-transactions";

        Console.WriteLine("Banking Transaction Message Publisher");
        Console.WriteLine("=====================================");
        Console.WriteLine($"Using queue: {queueName}");
        Console.WriteLine();

        await using var client = new ServiceBusClient(connectionString);
        await using var sender = client.CreateSender(queueName);

        while (true)
        {
            try
            {
                Console.WriteLine("Select an option:");
                Console.WriteLine("1. Send random transaction");
                Console.WriteLine("2. Send credit transaction");
                Console.WriteLine("3. Send debit transaction");
                Console.WriteLine("4. Send invalid transaction (for testing)");
                Console.WriteLine("5. Send batch of random transactions");
                Console.WriteLine("6. Exit");
                Console.Write("Choice: ");

                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await SendRandomTransaction(sender);
                        break;
                    case "2":
                        await SendCreditTransaction(sender);
                        break;
                    case "3":
                        await SendDebitTransaction(sender);
                        break;
                    case "4":
                        await SendInvalidTransaction(sender);
                        break;
                    case "5":
                        await SendBatchTransactions(sender);
                        break;
                    case "6":
                        Console.WriteLine("Goodbye!");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
            }
        }
    }

    private static async Task SendRandomTransaction(ServiceBusSender sender)
    {
        var random = new Random();
        var account = SampleAccounts[random.Next(SampleAccounts.Count)];
        var isCredit = random.Next(2) == 0;
        var amount = Math.Round((decimal)(random.NextDouble() * 500 + 10), 2);

        var transaction = new TransactionMessage
        {
            Id = Guid.NewGuid(),
            MessageType = isCredit ? "Credit" : "Debit",
            BankAccountId = account.Id,
            Amount = amount
        };

        await SendTransaction(sender, transaction);
        Console.WriteLine($"✅ Sent random {transaction.MessageType} transaction: ${amount:F2} for account {account.Id}");
    }

    private static async Task SendCreditTransaction(ServiceBusSender sender)
    {
        var account = SelectAccount();
        if (account == null) return;

        Console.Write("Enter credit amount: $");
        if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0)
        {
            Console.WriteLine("Invalid amount.");
            return;
        }

        var transaction = new TransactionMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "Credit",
            BankAccountId = account.Id,
            Amount = amount
        };

        await SendTransaction(sender, transaction);
        Console.WriteLine($"✅ Sent credit transaction: ${amount:F2} for account {account.Id}");
    }

    private static async Task SendDebitTransaction(ServiceBusSender sender)
    {
        var account = SelectAccount();
        if (account == null) return;

        Console.Write("Enter debit amount: $");
        if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0)
        {
            Console.WriteLine("Invalid amount.");
            return;
        }

        var transaction = new TransactionMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "Debit",
            BankAccountId = account.Id,
            Amount = amount
        };

        await SendTransaction(sender, transaction);
        Console.WriteLine($"✅ Sent debit transaction: ${amount:F2} for account {account.Id}");
    }

    private static async Task SendInvalidTransaction(ServiceBusSender sender)
    {
        Console.WriteLine("Select invalid transaction type:");
        Console.WriteLine("1. Invalid message type");
        Console.WriteLine("2. Invalid JSON");
        Console.WriteLine("3. Negative amount");
        Console.WriteLine("4. Non-existent account");
        Console.Write("Choice: ");

        var choice = Console.ReadLine();
        ServiceBusMessage message;

        switch (choice)
        {
            case "1":
                var invalidTypeTransaction = new
                {
                    Id = Guid.NewGuid(),
                    MessageType = "InvalidType",
                    BankAccountId = SampleAccounts[0].Id,
                    Amount = 100.00m
                };
                message = new ServiceBusMessage(JsonSerializer.Serialize(invalidTypeTransaction))
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                break;
            
            case "2":
                message = new ServiceBusMessage("{ invalid json }")
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                break;
            
            case "3":
                var negativeAmountTransaction = new TransactionMessage
                {
                    Id = Guid.NewGuid(),
                    MessageType = "Credit",
                    BankAccountId = SampleAccounts[0].Id,
                    Amount = -100.00m
                };
                message = new ServiceBusMessage(JsonSerializer.Serialize(negativeAmountTransaction))
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                break;
            
            case "4":
                var nonExistentAccountTransaction = new TransactionMessage
                {
                    Id = Guid.NewGuid(),
                    MessageType = "Credit",
                    BankAccountId = Guid.NewGuid(),
                    Amount = 100.00m
                };
                message = new ServiceBusMessage(JsonSerializer.Serialize(nonExistentAccountTransaction))
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                break;
            
            default:
                Console.WriteLine("Invalid choice.");
                return;
        }

        await sender.SendMessageAsync(message);
        Console.WriteLine($"✅ Sent invalid transaction for testing");
    }

    private static async Task SendBatchTransactions(ServiceBusSender sender)
    {
        Console.Write("Enter number of transactions to send: ");
        if (!int.TryParse(Console.ReadLine(), out var count) || count <= 0)
        {
            Console.WriteLine("Invalid count.");
            return;
        }

        var random = new Random();
        var messages = new List<ServiceBusMessage>();

        for (int i = 0; i < count; i++)
        {
            var account = SampleAccounts[random.Next(SampleAccounts.Count)];
            var isCredit = random.Next(2) == 0;
            var amount = Math.Round((decimal)(random.NextDouble() * 200 + 10), 2);

            var transaction = new TransactionMessage
            {
                Id = Guid.NewGuid(),
                MessageType = isCredit ? "Credit" : "Debit",
                BankAccountId = account.Id,
                Amount = amount
            };

            var message = new ServiceBusMessage(JsonSerializer.Serialize(transaction))
            {
                MessageId = transaction.Id.ToString()
            };

            messages.Add(message);
        }

        await sender.SendMessagesAsync(messages);
        Console.WriteLine($"✅ Sent batch of {count} transactions");
    }

    private static async Task SendTransaction(ServiceBusSender sender, TransactionMessage transaction)
    {
        var messageBody = JsonSerializer.Serialize(transaction);
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = transaction.Id.ToString()
        };

        await sender.SendMessageAsync(message);
    }

    private static BankAccountSample? SelectAccount()
    {
        Console.WriteLine("Select account:");
        for (int i = 0; i < SampleAccounts.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {SampleAccounts[i].Id} (Initial: ${SampleAccounts[i].InitialBalance:F2})");
        }
        Console.Write("Choice: ");

        if (int.TryParse(Console.ReadLine(), out var choice) && choice >= 1 && choice <= SampleAccounts.Count)
        {
            return SampleAccounts[choice - 1];
        }

        Console.WriteLine("Invalid choice.");
        return null;
    }
}

public class TransactionMessage
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public Guid BankAccountId { get; set; }
    public decimal Amount { get; set; }
}

public class BankAccountSample
{
    public Guid Id { get; set; }
    public decimal InitialBalance { get; set; }
}