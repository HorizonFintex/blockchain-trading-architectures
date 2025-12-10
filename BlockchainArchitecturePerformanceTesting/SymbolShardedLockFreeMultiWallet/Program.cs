using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;

Console.WriteLine("=== Symbol-Sharded Lock-Free Multi-Wallet Blockchain Test ===");
Console.WriteLine("Architecture 5: Symbol-Sharded, Lock-Free, Multi-Wallet, Asynchronous");
Console.WriteLine("Multiple producer-consumer pairs with dedicated wallet and contract per symbol.");
Console.WriteLine();

const string rpcUrl = "http://10.41.33.100:8545";
const int totalTransactions = 1000;

// Symbol to contract and wallet mapping - each symbol gets its own dedicated resources
var symbolMappings = new Dictionary<string, (string contractAddress, string walletAddress)>
{
    { "AAPL", ("0xf55675f9ca35ed2a8740e65d474400a74f407212", "0x07954e1587f9adaf2f64a69376d2bbea60568896") },
    { "GOOGL", ("0x8e5829931f254773d2c998c5293b1de55b190953", "0x07987db5f5990cc60702bc388ec1fdab77bcf939") },
    { "MSFT", ("0xb16c15ba7cb84977d2dae1048b2290d3f64aac79", "0x087ff071e9be36eac002d2b4c2ef65a477fcdfde") },
    { "AMZN", ("0x591bac57c2e2fa1be43204a574e2c4510ef57ae1", "0x098ac09ff0eb020544f425ea65bc6d61327e756b") },
    { "TSLA", ("0x0d7353dfb52468b113345b377d786c4fcdd1c5b2", "0x0ecc4900035b7611219c04b6e1b9c836e1244a2a") },
    { "META", ("0x9faa9c8d9a3265bb12a89580f62dd25854a637d8", "0x112e6951c7fdb311007d24e809f43e50639e47db") },
    { "NVDA", ("0x49d06f71841059435ccd90ddb25aeea5aeba425b", "0x1228b861f152b083b8486077d39f7d8cc176be0b") },
    { "AMD", ("0x8ebf7e98c42a68e99581e98a371d2aa54403fc1b", "0x133a64532c628f29934b351e8f24295d74e733e1") },
    { "INTC", ("0x54432532c90b3ab8c3b9a7f46534c79f294dc5c8", "0x14b4cf60693232f389d9a5937cec9cfa5c39e340") },
    { "NFLX", ("0x2047270f24d8750cd2df945df1a7d5523ccbf9d0", "0x186f62501b218fd3e9058df0bef0213d90938260") }
};

// Each symbol gets its own HttpClient to eliminate any shared resource contention
var httpClients = new Dictionary<string, HttpClient>();
foreach (var symbol in symbolMappings.Keys)
{
    httpClients[symbol] = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
}

// Shared statistics across all threads (using lock-free concurrent collections)
var statistics = new TransactionStatistics();

try
{
    Console.WriteLine("Symbol to Contract and Wallet Mappings:");
    foreach (var kvp in symbolMappings)
    {
        Console.WriteLine($"  {kvp.Key,-8} ? Contract: {kvp.Value.contractAddress} | Wallet: {kvp.Value.walletAddress}");
    }
    Console.WriteLine();
    Console.WriteLine($"Number of symbols/threads: {symbolMappings.Count}");
    Console.WriteLine($"Transactions per symbol: {totalTransactions / symbolMappings.Count}");
    Console.WriteLine();

    var overallStopwatch = Stopwatch.StartNew();

    Console.WriteLine($"Starting lock-free multi-threaded submission of {totalTransactions} transactions...");
    Console.WriteLine($"Each symbol has its own:");
    Console.WriteLine($"  - Dedicated producer-consumer thread pair");
    Console.WriteLine($"  - Dedicated wallet (independent nonce sequence)");
    Console.WriteLine($"  - Dedicated contract (isolated state)");
    Console.WriteLine($"  - Dedicated HttpClient (no shared resources)");
    Console.WriteLine($"NO artificial delays, NO semaphores, NO locks - pure parallelism!");
    Console.WriteLine();

    // Create a task for each symbol - completely isolated processing
    var symbolTasks = new List<Task>();
    var transactionsPerSymbol = totalTransactions / symbolMappings.Count;

    foreach (var kvp in symbolMappings)
    {
        var symbol = kvp.Key;
        var contractAddress = kvp.Value.contractAddress;
        var walletAddress = kvp.Value.walletAddress;
        var httpClient = httpClients[symbol];
        
        var symbolTask = Task.Run(async () =>
        {
            await ProcessSymbolAsync(symbol, contractAddress, walletAddress, transactionsPerSymbol, 
                httpClient, rpcUrl, statistics);
        });
        
        symbolTasks.Add(symbolTask);
    }

    // Wait for all symbol processing to complete
    await Task.WhenAll(symbolTasks);

    overallStopwatch.Stop();

    // Print statistics
    Console.WriteLine();
    Console.WriteLine("=".PadRight(60, '='));
    Console.WriteLine("=== Test Results ===");
    Console.WriteLine("=".PadRight(60, '='));
    Console.WriteLine($"Total Time: {overallStopwatch.Elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine();

    statistics.PrintStatistics(overallStopwatch.Elapsed, totalTransactions);
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
finally
{
    // Clean up HttpClients
    foreach (var client in httpClients.Values)
    {
        client.Dispose();
    }
}

// Process transactions for a single symbol with producer-consumer pattern
// Each symbol is completely isolated - no shared resources with other symbols
static async Task ProcessSymbolAsync(string symbol, string contractAddress, string walletAddress,
    int transactionCount, HttpClient httpClient, string rpcUrl, TransactionStatistics statistics)
{
    // Lock-free queue using ConcurrentQueue
    var transactionQueue = new BlockingCollection<string>(new ConcurrentQueue<string>());
    
    // Lock-free dictionary for submission times
    var submissionTimes = new ConcurrentDictionary<string, DateTime>();
    
    const string completionSignal = "COMPLETE";
    
    Console.WriteLine($"[{symbol}] Starting isolated producer-consumer pair for {transactionCount} transactions");
    Console.WriteLine($"[{symbol}]   Wallet: {walletAddress}");
    Console.WriteLine($"[{symbol}]   Contract: {contractAddress}");

    // Consumer task - polls for mined transactions
    var consumerTask = Task.Run(async () =>
    {
        int minedCount = 0;
        int processedCount = 0;
        
        Console.WriteLine($"[{symbol}] Consumer: Started and waiting for transactions");
        
        foreach (var txHash in transactionQueue.GetConsumingEnumerable())
        {
            if (txHash == completionSignal)
            {
                Console.WriteLine($"[{symbol}] Consumer: Received completion signal");
                break;
            }

            processedCount++;
            
            try
            {
                if (processedCount % 20 == 1)
                {
                    Console.WriteLine($"[{symbol}] Consumer: Processing tx {processedCount}, waiting for mining...");
                }
                
                var receipt = await WaitForTransactionMiningAsync(httpClient, rpcUrl, txHash);
                
                if (submissionTimes.TryRemove(txHash, out var submitTime))
                {
                    var totalTime = DateTime.UtcNow - submitTime;
                    statistics.RecordTransaction(totalTime, receipt.GasUsed, receipt.BlockNumber, symbol, txHash);
                    
                    minedCount++;
                    
                    if (minedCount % 20 == 0)
                    {
                        Console.WriteLine($"[{symbol}] Consumer: {minedCount}/{transactionCount} mined (block {receipt.BlockNumber})");
                    }
                }
            }
            catch (Exception ex)
            {
                statistics.RecordMiningFailure(symbol);
                Console.WriteLine($"[{symbol}] Consumer: Failed to mine tx {processedCount} ({txHash[..10]}): {ex.Message}");
            }
        }
        
        Console.WriteLine($"[{symbol}] Consumer finished: {minedCount}/{processedCount} mined successfully");
    });

    // Producer task - submits transactions
    var producerTask = Task.Run(async () =>
    {
        int submittedCount = 0;
        
        for (int i = 0; i < transactionCount; i++)
        {
            try
            {
                var orderId = (ulong)Random.Shared.NextInt64(1000000000, 9999999999);

                // NO LOCKS, NO SEMAPHORES - each symbol has dedicated resources!
                var txHash = await SubmitBidTransactionAsync(httpClient, rpcUrl, contractAddress, 
                    walletAddress, symbol, orderId);
                
                submissionTimes[txHash] = DateTime.UtcNow;
                transactionQueue.Add(txHash);
                
                submittedCount++;
                
                if (submittedCount % 20 == 0)
                {
                    Console.WriteLine($"[{symbol}] Producer: {submittedCount}/{transactionCount} submitted [Queue: {transactionQueue.Count}]");
                }
                
                // NO ARTIFICIAL DELAY - independent wallet + dedicated resources = no contention!
            }
            catch (Exception ex)
            {
                statistics.RecordSubmissionFailure(symbol);
                Console.WriteLine($"[{symbol}] Producer: Failed to submit tx {i + 1}: {ex.Message}");
            }
        }

        Console.WriteLine($"[{symbol}] Producer: Finished submitting {submittedCount}/{transactionCount}, sending completion signal");
        transactionQueue.Add(completionSignal);
        transactionQueue.CompleteAdding();
    });

    await Task.WhenAll(producerTask, consumerTask);
    Console.WriteLine($"[{symbol}] Complete: Both producer and consumer finished");
}

// Helper Methods - No locks or semaphores needed!

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
    var responseContent = await response.Content.ReadAsStringAsync();

    var jsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    try
    {
        var result = System.Text.Json.JsonSerializer.Deserialize<JsonRpcResponse<string>>(responseContent, jsonOptions);

        if (result?.Result == null)
        {
            var errorResult = System.Text.Json.JsonSerializer.Deserialize<JsonRpcErrorResponse>(responseContent, jsonOptions);
            if (errorResult?.Error != null)
            {
                throw new Exception($"RPC Error: {errorResult.Error.Message} (Code: {errorResult.Error.Code})");
            }
            throw new Exception($"Failed to submit transaction. Response: {responseContent}");
        }

        return result.Result;
    }
    catch (Exception ex) when (ex.Message.StartsWith("RPC Error"))
    {
        throw;
    }
    catch (System.Text.Json.JsonException jsonEx)
    {
        throw new Exception($"JSON parsing error: {jsonEx.Message}. Response: {responseContent}");
    }
    catch (Exception ex)
    {
        throw new Exception($"Failed to parse response: {ex.Message}. Response: {responseContent}");
    }
}

static async Task<TransactionReceipt> WaitForTransactionMiningAsync(HttpClient client, string rpcUrl, string txHash)
{
    const int maxAttempts = 120;

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

        await Task.Delay(1000);
    }

    throw new Exception($"Transaction {txHash} was not mined within {maxAttempts} seconds");
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

// Data Models

class JsonRpcResponse<T>
{
    public string? Jsonrpc { get; set; }
    public T? Result { get; set; }
    public int Id { get; set; }
}

class JsonRpcErrorResponse
{
    public string? Jsonrpc { get; set; }
    public JsonRpcError? Error { get; set; }
    public int Id { get; set; }
}

class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
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

// Lock-free statistics using concurrent collections
class TransactionStatistics
{
    private readonly ConcurrentBag<double> _transactionTimes = new();
    private readonly ConcurrentBag<long> _gasUsed = new();
    private readonly ConcurrentDictionary<long, byte> _uniqueBlocks = new();
    private readonly ConcurrentDictionary<string, int> _successBySymbol = new();
    private readonly ConcurrentDictionary<string, int> _submissionFailuresBySymbol = new();
    private readonly ConcurrentDictionary<string, int> _miningFailuresBySymbol = new();

    public void RecordTransaction(TimeSpan duration, long gasUsed, long blockNumber, string symbol, string txHash)
    {
        _transactionTimes.Add(duration.TotalSeconds);
        _gasUsed.Add(gasUsed);
        _uniqueBlocks.TryAdd(blockNumber, 0);
        _successBySymbol.AddOrUpdate(symbol, 1, (_, count) => count + 1);
    }

    public void RecordSubmissionFailure(string symbol)
    {
        _submissionFailuresBySymbol.AddOrUpdate(symbol, 1, (_, count) => count + 1);
    }

    public void RecordMiningFailure(string symbol)
    {
        _miningFailuresBySymbol.AddOrUpdate(symbol, 1, (_, count) => count + 1);
    }

    public void PrintStatistics(TimeSpan totalTime, int totalAttempted)
    {
        var successfulTx = _transactionTimes.Count;
        var totalSubmissionFailures = _submissionFailuresBySymbol.Values.Sum();
        var totalMiningFailures = _miningFailuresBySymbol.Values.Sum();
        var totalFailures = totalSubmissionFailures + totalMiningFailures;

        Console.WriteLine("=== Performance Statistics ===");
        Console.WriteLine($"Total Attempted: {totalAttempted}");
        Console.WriteLine($"Successful Transactions: {successfulTx}");
        Console.WriteLine($"Submission Failures: {totalSubmissionFailures}");
        Console.WriteLine($"Mining Failures: {totalMiningFailures}");
        Console.WriteLine($"Total Failures: {totalFailures}");
        Console.WriteLine($"Success Rate: {(successfulTx * 100.0 / totalAttempted):F2}%");
        Console.WriteLine();

        if (successfulTx > 0)
        {
            Console.WriteLine($"Overall Throughput: {successfulTx / totalTime.TotalSeconds:F2} tx/sec");
            Console.WriteLine($"Unique Blocks Used: {_uniqueBlocks.Count}");
            Console.WriteLine($"Average Transactions per Block: {(double)successfulTx / _uniqueBlocks.Count:F2}");
            Console.WriteLine();
        }

        // Per-symbol statistics
        Console.WriteLine("=== Per-Symbol Statistics ===");
        var allSymbols = _successBySymbol.Keys
            .Union(_submissionFailuresBySymbol.Keys)
            .Union(_miningFailuresBySymbol.Keys)
            .OrderBy(s => s);

        foreach (var symbol in allSymbols)
        {
            var success = _successBySymbol.GetValueOrDefault(symbol, 0);
            var subFailures = _submissionFailuresBySymbol.GetValueOrDefault(symbol, 0);
            var minFailures = _miningFailuresBySymbol.GetValueOrDefault(symbol, 0);
            var total = success + subFailures + minFailures;
            
            Console.WriteLine($"{symbol,-8}: Success: {success,3}, Sub Fail: {subFailures,3}, Mine Fail: {minFailures,3}, Total: {total,3}");
        }
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
