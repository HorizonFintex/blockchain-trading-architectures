using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;

Console.WriteLine("=== Multi-Threaded Asynchronous Single-Wallet Blockchain Test ===");
Console.WriteLine("Architecture 3: Multi-Threaded, Asynchronous, Single-Wallet");
Console.WriteLine("Multiple concurrent threads submit asynchronously to a single blockchain wallet.");
Console.WriteLine();

const string rpcUrl = "http://10.41.33.100:8545";
const int totalTransactions = 1000;
const int submissionDelayMs = 500; // Increased from 100ms to 500ms to reduce nonce conflicts with 10 concurrent threads

// Symbol to contract address mapping
var symbolContracts = new Dictionary<string, string>
{
    { "AAPL", "0x3A1f0EF3dbbd16370B8606A4D03d0Ea9BF6E561f" },
    { "GOOGL", "0xe38d4CfA501C22A0dF3e9de0514d12FED2950968" },
    { "MSFT", "0x479B5016418fB8c7838C980b69360bfdbeEc6f57" },
    { "AMZN", "0x657E17E6a3E022e255342deb9d4513b6D2cD20A8" },
    { "TSLA", "0x3a39104910084C495fAF2815c51A856AE482b460" },
    { "META", "0xD05a885A416D0Be9493F77775Ab288407B23571c" },
    { "NVDA", "0x23E9A16D696db4F6338DBcD8CE0aD75343344C98" },
    { "AMD", "0xdB32C288f7915360c308DF84f93416ec40C9a808" },
    { "INTC", "0x56A50288A21025577562bB336725F22Aa2Ee65eD" },
    { "NFLX", "0x1cD19F8b0b2503814980b60b31ff5278882F59fF" }
};

using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
using var httpClientSemaphore = new SemaphoreSlim(1, 1);

// Shared statistics across all threads
var statistics = new TransactionStatistics();

try
{
    // Get the first account (single wallet for all transactions)
    var fromAccount = await GetFirstAccountAsync(httpClient, rpcUrl);
    Console.WriteLine($"Using account: {fromAccount}");
    Console.WriteLine($"Number of symbols/threads: {symbolContracts.Count}");
    Console.WriteLine($"Transactions per symbol: {totalTransactions / symbolContracts.Count}");
    Console.WriteLine();

    var overallStopwatch = Stopwatch.StartNew();

    Console.WriteLine($"Starting multi-threaded submission of {totalTransactions} transactions...");
    Console.WriteLine($"Each symbol has its own producer-consumer thread pair");
    Console.WriteLine();

    // Create a task for each symbol
    var symbolTasks = new List<Task>();
    var transactionsPerSymbol = totalTransactions / symbolContracts.Count;

    foreach (var kvp in symbolContracts)
    {
        var symbol = kvp.Key;
        var contractAddress = kvp.Value;
        
        var symbolTask = Task.Run(async () =>
        {
            await ProcessSymbolAsync(symbol, contractAddress, fromAccount, transactionsPerSymbol, 
                httpClient, rpcUrl, statistics, overallStopwatch, httpClientSemaphore);
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

// Process transactions for a single symbol with producer-consumer pattern
static async Task ProcessSymbolAsync(string symbol, string contractAddress, string fromAccount, 
    int transactionCount, HttpClient httpClient, string rpcUrl, TransactionStatistics statistics, 
    Stopwatch overallStopwatch, SemaphoreSlim semaphore)
{
    var transactionQueue = new BlockingCollection<string>(new ConcurrentQueue<string>());
    var submissionTimes = new ConcurrentDictionary<string, DateTime>();
    const string completionSignal = "COMPLETE";
    
    Console.WriteLine($"[{symbol}] Starting producer-consumer pair for {transactionCount} transactions");

    // Consumer task
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
                if (processedCount % 10 == 1)
                {
                    Console.WriteLine($"[{symbol}] Consumer: Processing tx {processedCount}, waiting for mining...");
                }
                
                var receipt = await WaitForTransactionMiningAsync(httpClient, rpcUrl, txHash);
                
                if (submissionTimes.TryGetValue(txHash, out var submitTime))
                {
                    var totalTime = DateTime.UtcNow - submitTime;
                    statistics.RecordTransaction(totalTime, receipt.GasUsed, receipt.BlockNumber, symbol, txHash);
                    
                    minedCount++;
                    
                    if (minedCount % 10 == 0)
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

    // Producer task
    var producerTask = Task.Run(async () =>
    {
        int submittedCount = 0;
        
        for (int i = 0; i < transactionCount; i++)
        {
            try
            {
                var orderId = (ulong)Random.Shared.NextInt64(1000000000, 9999999999);

                var txHash = await SubmitBidTransactionAsync(httpClient, rpcUrl, contractAddress, 
                    fromAccount, symbol, orderId, semaphore);
                
                submissionTimes[txHash] = DateTime.UtcNow;
                transactionQueue.Add(txHash);
                
                submittedCount++;
                
                if (submittedCount % 10 == 0)
                {
                    Console.WriteLine($"[{symbol}] Producer: {submittedCount}/{transactionCount} submitted [Queue: {transactionQueue.Count}]");
                }
                
                // Add delay to manage nonce distance across all threads
                if (i < transactionCount - 1)
                {
                    await Task.Delay(submissionDelayMs);
                }
            }
            catch (Exception ex) when (ex.Message.Contains("nonce"))
            {
                statistics.RecordSubmissionFailure(symbol);
                Console.WriteLine($"[{symbol}] Producer: Nonce error on tx {i + 1}: {ex.Message}");
                Console.WriteLine($"[{symbol}] Producer: Pausing for 3 seconds to let blockchain catch up...");
                await Task.Delay(3000);
                
                // Retry this transaction after the pause
                i--; // Decrement so we retry the same transaction
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
    Console.WriteLine($"[{symbol}] ProcessSymbolAsync: Both producer and consumer completed");
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

static async Task<string> SubmitBidTransactionAsync(HttpClient client, string rpcUrl, string contractAddress,
    string fromAccount, string symbol, ulong orderId, SemaphoreSlim semaphore)
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

  await semaphore.WaitAsync();
  try
  {
    var response = await client.PostAsJsonAsync(rpcUrl, request);
    var responseContent = await response.Content.ReadAsStringAsync();

    // Configure JSON options for case-insensitive property matching
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
  finally
  {
    semaphore.Release();
  }
}

static async Task<TransactionReceipt> WaitForTransactionMiningAsync(HttpClient client, string rpcUrl, string txHash)
{
    const int maxAttempts = 60; // Reduced from 120 to 60 seconds to fail faster on nonce conflicts

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

        // Log progress for long-running waits
        if (attempt > 0 && attempt % 10 == 0)
        {
            Console.WriteLine($"    Still waiting for {txHash[..10]}... ({attempt}s elapsed)");
        }

        await Task.Delay(1000);
    }

    throw new Exception($"Transaction {txHash} was not mined within {maxAttempts} seconds (likely nonce conflict or rejected)");
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

class TransactionStatistics
{
    private readonly List<double> _transactionTimes = new();
    private readonly List<long> _gasUsed = new();
    private readonly HashSet<long> _uniqueBlocks = new();
    private readonly Dictionary<string, int> _successBySymbol = new();
    private readonly Dictionary<string, int> _submissionFailuresBySymbol = new();
    private readonly Dictionary<string, int> _miningFailuresBySymbol = new();
    private readonly object _lock = new();

    public void RecordTransaction(TimeSpan duration, long gasUsed, long blockNumber, string symbol, string txHash)
    {
        lock (_lock)
        {
            _transactionTimes.Add(duration.TotalSeconds);
            _gasUsed.Add(gasUsed);
            _uniqueBlocks.Add(blockNumber);
            
            if (!_successBySymbol.ContainsKey(symbol))
                _successBySymbol[symbol] = 0;
            _successBySymbol[symbol]++;
        }
    }

    public void RecordSubmissionFailure(string symbol)
    {
        lock (_lock)
        {
            if (!_submissionFailuresBySymbol.ContainsKey(symbol))
                _submissionFailuresBySymbol[symbol] = 0;
            _submissionFailuresBySymbol[symbol]++;
        }
    }

    public void RecordMiningFailure(string symbol)
    {
        lock (_lock)
        {
            if (!_miningFailuresBySymbol.ContainsKey(symbol))
                _miningFailuresBySymbol[symbol] = 0;
            _miningFailuresBySymbol[symbol]++;
        }
    }

    public void PrintStatistics(TimeSpan totalTime, int totalAttempted)
    {
        lock (_lock)
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
}
