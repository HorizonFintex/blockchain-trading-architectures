using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

Console.WriteLine("=== Sequential Blocking Single-Threaded Blockchain Test ===");
Console.WriteLine("Architecture 1: Sequential, Synchronous, Single-Wallet (Baseline)");
Console.WriteLine();

const string defaultRpcUrl = "http://hardhat-node:8545";
string rpcUrl = Environment.GetEnvironmentVariable("RPC_URL") ?? defaultRpcUrl;
const string contractAddress = "0xf4d4FF384b81B1a6041537C14D37A35a698ecAEc";
const int totalTransactions = 1000;

using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

try
{
    // Get the first account
    var fromAccount = await GetFirstAccountAsync(httpClient, rpcUrl);
    Console.WriteLine($"Using account: {fromAccount}");
    Console.WriteLine();

    // Get initial counter value
    var initialValue = await GetCounterValueAsync(httpClient, rpcUrl, contractAddress);
    Console.WriteLine($"Initial counter value: {initialValue}");
    Console.WriteLine();

    // Statistics
    var statistics = new TransactionStatistics();
    var overallStopwatch = Stopwatch.StartNew();

    Console.WriteLine($"Starting sequential submission of {totalTransactions} transactions...");
    Console.WriteLine();

    // Submit transactions sequentially
    for (int i = 0; i < totalTransactions; i++)
    {
        var txStopwatch = Stopwatch.StartNew();
        
        try
        {
            // Generate random order data
            var symbol = GetRandomSymbol();
            var orderId = (ulong)Random.Shared.NextInt64(1000000000, 9999999999);
            
            Console.Write($"[{i + 1}/{totalTransactions}] Submitting {symbol} order {orderId}... ");
            
            // Submit transaction
            var txHash = await SubmitBidTransactionAsync(httpClient, rpcUrl, contractAddress, fromAccount, symbol, orderId);
            Console.Write($"Submitted (TxHash: {txHash[..10]}...) ");
            
            // Wait for transaction to be mined (synchronous blocking)
            Console.Write("Waiting for mining... ");
            var receipt = await WaitForTransactionMiningAsync(httpClient, rpcUrl, txHash);
            
            txStopwatch.Stop();
            
            // Record statistics
            statistics.RecordTransaction(txStopwatch.Elapsed, receipt.GasUsed, receipt.BlockNumber);
            
            Console.WriteLine($"✓ Mined in block {receipt.BlockNumber} ({txStopwatch.ElapsedMilliseconds}ms, {receipt.GasUsed:N0} gas)");
            
            // Print summary every 50 transactions
            if ((i + 1) % 50 == 0)
            {
                var elapsed = overallStopwatch.Elapsed;
                var avgTxPerSec = (i + 1) / elapsed.TotalSeconds;
                Console.WriteLine($"--- Progress: {i + 1}/{totalTransactions} completed | Avg: {avgTxPerSec:F2} tx/sec | Elapsed: {elapsed.TotalSeconds:F1}s ---");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            statistics.RecordFailure();
            Console.WriteLine($"✗ FAILED: {ex.Message}");
        }
    }

    overallStopwatch.Stop();

    // Get final counter value
    Console.WriteLine();
    Console.WriteLine("Retrieving final counter value...");
    var finalValue = await GetCounterValueAsync(httpClient, rpcUrl, contractAddress);

    // Print statistics
    Console.WriteLine();
    Console.WriteLine("=".PadRight(60, '='));
    Console.WriteLine("=== Test Results ===");
    Console.WriteLine("=".PadRight(60, '='));
    Console.WriteLine($"Total Time: {overallStopwatch.Elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine($"Initial Counter Value: {initialValue}");
    Console.WriteLine($"Final Counter Value: {finalValue}");
    Console.WriteLine($"Counter Increased By: {finalValue - initialValue}");
    Console.WriteLine();
    
    statistics.PrintStatistics(overallStopwatch.Elapsed);
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

// Helper Methods

static async Task<string> GetFirstAccountAsync(HttpClient client, string rpcUrl)
{
    var request = new
    {
        jsonrpc = "2.0",
        method = "eth_accounts",
        @params = Array.Empty<object>(),
        id = 1
    };

    var response = await client.PostAsJsonAsync(rpcUrl, request);
    var result = await response.Content.ReadFromJsonAsync<JsonRpcResponse<string[]>>();
    
    if (result?.Result == null || result.Result.Length == 0)
        throw new Exception("No accounts available");
    
    return result.Result[0];
}

static async Task<long> GetCounterValueAsync(HttpClient client, string rpcUrl, string contractAddress)
{
    var request = new
    {
        jsonrpc = "2.0",
        method = "eth_call",
        @params = new object[]
        {
            new { to = contractAddress, data = "0x20965255" },
            "latest"
        },
        id = 1
    };

    var response = await client.PostAsJsonAsync(rpcUrl, request);
    var result = await response.Content.ReadFromJsonAsync<JsonRpcResponse<string>>();
    
    return Convert.ToInt64(result?.Result ?? "0x0", 16);
}

static async Task<string> SubmitBidTransactionAsync(HttpClient client, string rpcUrl, string contractAddress, 
    string fromAccount, string symbol, ulong orderId)
{
    var encodedData = EncodeBidData(symbol, orderId);
    
    var request = new
    {
        jsonrpc = "2.0",
        method = "eth_sendTransaction",
        @params = new object[]
        {
            new
            {
                from = fromAccount,
                to = contractAddress,
                data = encodedData,
                gas = "0x80000"
            }
        },
        id = 1
    };

    var response = await client.PostAsJsonAsync(rpcUrl, request);
    var result = await response.Content.ReadFromJsonAsync<JsonRpcResponse<string>>();
    
    if (result?.Result == null)
        throw new Exception("Failed to submit transaction");
    
    return result.Result;
}

static async Task<TransactionReceipt> WaitForTransactionMiningAsync(HttpClient client, string rpcUrl, string txHash)
{
    const int maxAttempts = 120; // 2 minutes with 1-second polling
    
    for (int attempt = 0; attempt < maxAttempts; attempt++)
    {
        var request = new
        {
            jsonrpc = "2.0",
            method = "eth_getTransactionReceipt",
            @params = new[] { txHash },
            id = 1
        };

        var response = await client.PostAsJsonAsync(rpcUrl, request);
        var result = await response.Content.ReadFromJsonAsync<JsonRpcResponse<TransactionReceiptDto?>>();
        
        if (result?.Result != null)
        {
            return new TransactionReceipt
            {
                TransactionHash = result.Result.TransactionHash,
                BlockNumber = Convert.ToInt64(result.Result.BlockNumber, 16),
                GasUsed = Convert.ToInt64(result.Result.GasUsed, 16)
            };
        }

        await Task.Delay(1000); // Wait 1 second before next poll
    }
    
    throw new Exception($"Transaction {txHash} was not mined within timeout period");
}

static string EncodeBidData(string symbol, ulong orderId)
{
    const string selector = "5f483313";
    const string stringOffset = "0000000000000000000000000000000000000000000000000000000000000040";
    
    var orderIdHex = orderId.ToString("X64");
    
    var symbolBytes = Encoding.UTF8.GetBytes(symbol);
    var stringLength = symbolBytes.Length.ToString("X64");
    
    var symbolHex = BitConverter.ToString(symbolBytes).Replace("-", "").ToLower();
    var paddingNeeded = (32 - (symbolBytes.Length % 32)) % 32;
    symbolHex += new string('0', paddingNeeded * 2);
    
    return "0x" + selector + stringOffset + orderIdHex + stringLength + symbolHex;
}

static string GetRandomSymbol()
{
    string[] symbols = { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA", "AMD", "INTC", "NFLX" };
    return symbols[Random.Shared.Next(symbols.Length)];
}

// Data Models

class JsonRpcResponse<T>
{
    public string? Jsonrpc { get; set; }
    public T? Result { get; set; }
    public int Id { get; set; }
}

class TransactionReceiptDto
{
    public string TransactionHash { get; set; } = "";
    public string BlockNumber { get; set; } = "";
    public string GasUsed { get; set; } = "";
}

class TransactionReceipt
{
    public string TransactionHash { get; set; } = "";
    public long BlockNumber { get; set; }
    public long GasUsed { get; set; }
}

class TransactionStatistics
{
    private readonly List<double> _transactionTimes = new();
    private readonly List<long> _gasUsed = new();
    private readonly HashSet<long> _uniqueBlocks = new();
    private int _failedTransactions = 0;

    public void RecordTransaction(TimeSpan duration, long gasUsed, long blockNumber)
    {
        _transactionTimes.Add(duration.TotalSeconds);
        _gasUsed.Add(gasUsed);
        _uniqueBlocks.Add(blockNumber);
    }

    public void RecordFailure()
    {
        _failedTransactions++;
    }

    public void PrintStatistics(TimeSpan totalTime)
    {
        var successfulTx = _transactionTimes.Count;
        
        Console.WriteLine("=== Performance Statistics ===");
        Console.WriteLine($"Successful Transactions: {successfulTx}");
        Console.WriteLine($"Failed Transactions: {_failedTransactions}");
        Console.WriteLine($"Success Rate: {(successfulTx * 100.0 / (successfulTx + _failedTransactions)):F2}%");
        Console.WriteLine();
        
        Console.WriteLine($"Overall Throughput: {successfulTx / totalTime.TotalSeconds:F2} tx/sec");
        Console.WriteLine($"Unique Blocks Used: {_uniqueBlocks.Count}");
        Console.WriteLine($"Average Transactions per Block: {(double)successfulTx / _uniqueBlocks.Count:F2}");
        Console.WriteLine();
        
        if (_transactionTimes.Count > 0)
        {
            var sortedTimes = _transactionTimes.OrderBy(x => x).ToList();
            Console.WriteLine("=== Transaction Timing (Submission + Mining) ===");
            Console.WriteLine($"Average: {_transactionTimes.Average():F3} seconds");
            Console.WriteLine($"Median: {sortedTimes[sortedTimes.Count / 2]:F3} seconds");
            Console.WriteLine($"Min: {sortedTimes.First():F3} seconds");
            Console.WriteLine($"Max: {sortedTimes.Last():F3} seconds");
            Console.WriteLine($"P95: {sortedTimes[(int)(sortedTimes.Count * 0.95)]:F3} seconds");
            Console.WriteLine($"P99: {sortedTimes[(int)(sortedTimes.Count * 0.99)]:F3} seconds");
            Console.WriteLine();
        }
        
        if (_gasUsed.Count > 0)
        {
            Console.WriteLine("=== Gas Usage ===");
            Console.WriteLine($"Average Gas Used: {_gasUsed.Average():F0}");
            Console.WriteLine($"Total Gas Used: {_gasUsed.Sum():N0}");
            Console.WriteLine($"Min Gas: {_gasUsed.Min():N0}");
            Console.WriteLine($"Max Gas: {_gasUsed.Max():N0}");
        }
    }
}
