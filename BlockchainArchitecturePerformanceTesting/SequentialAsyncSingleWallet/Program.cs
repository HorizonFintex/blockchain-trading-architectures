using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

Console.WriteLine("=== Sequential Asynchronous Single-Wallet Blockchain Test ===");
Console.WriteLine("Architecture 2: Sequential, Asynchronous, Single-Wallet");
Console.WriteLine("Single submission thread; separate thread polls for mined transactions.");
Console.WriteLine();

const string rpcUrl = "http://10.41.33.100:8545";
const string contractAddress = "0xf4d4FF384b81B1a6041537C14D37A35a698ecAEc";
//const string contractAddress = "0x1cD19F8b0b2503814980b60b31ff5278882F59fF";
const int totalTransactions = 1000;
const string completionSignal = "COMPLETE";

using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

// Shared queue for transaction hashes between producer and consumer threads
var transactionQueue = new BlockingCollection<string>(new ConcurrentQueue<string>());

// Dictionary to track submission times for each transaction
var submissionTimes = new ConcurrentDictionary<string, DateTime>();

// Statistics
var statistics = new TransactionStatistics();

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

    var overallStopwatch = Stopwatch.StartNew();

    Console.WriteLine($"Starting asynchronous submission of {totalTransactions} transactions...");
    Console.WriteLine("Producer thread: Submits transactions sequentially");
    Console.WriteLine("Consumer thread: Polls for mined transactions asynchronously");
    Console.WriteLine();

    // Start consumer thread (polls for mined transactions)
    var consumerTask = Task.Run(async () =>
    {
        int minedCount = 0;
        
        foreach (var txHash in transactionQueue.GetConsumingEnumerable())
        {
            if (txHash == completionSignal)
            {
                Console.WriteLine("[Consumer] Received completion signal. Exiting...");
                break;
            }

            try
            {
                var pollStopwatch = Stopwatch.StartNew();
                var receipt = await WaitForTransactionMiningAsync(httpClient, rpcUrl, txHash);
                pollStopwatch.Stop();
                
                // Calculate total time from submission to mining
                if (submissionTimes.TryGetValue(txHash, out var submitTime))
                {
                    var totalTime = DateTime.UtcNow - submitTime;
                    statistics.RecordTransaction(totalTime, receipt.GasUsed, receipt.BlockNumber, txHash);
                    
                    minedCount++;
                    Console.WriteLine($"[Consumer] ✓ Tx {minedCount} mined in block {receipt.BlockNumber} (polling: {pollStopwatch.ElapsedMilliseconds}ms, total: {totalTime.TotalMilliseconds:F0}ms, gas: {receipt.GasUsed:N0})");
                }
                else
                {
                    Console.WriteLine($"[Consumer] Warning: No submission time found for {txHash}");
                }

                if (minedCount % 50 == 0)
                {
                    var elapsed = overallStopwatch.Elapsed;
                    Console.WriteLine($"[Consumer] --- Progress: {minedCount} mined | Elapsed: {elapsed.TotalSeconds:F1}s ---");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                statistics.RecordMiningFailure();
                Console.WriteLine($"[Consumer] ✗ Failed to mine {txHash}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"[Consumer] Finished processing. Total mined: {minedCount}");
    });

    // Producer thread (submits transactions)
    var producerTask = Task.Run(async () =>
    {
        int submittedCount = 0;
        const int submissionDelayMs = 50; // Add delay to prevent nonce distance issues
        
        for (int i = 0; i < totalTransactions; i++)
        {
            try
            {
                var symbol = GetRandomSymbol();
                var orderId = (ulong)Random.Shared.NextInt64(1000000000, 9999999999);

                Console.Write($"[Producer] [{i + 1}/{totalTransactions}] Submitting {symbol} order {orderId}... ");

                var txHash = await SubmitBidTransactionAsync(httpClient, rpcUrl, contractAddress, fromAccount, symbol, orderId);
                
                // Record submission time
                submissionTimes[txHash] = DateTime.UtcNow;
                
                // Add to queue for consumer to process
                transactionQueue.Add(txHash);
                
                submittedCount++;
                Console.WriteLine($"Submitted (TxHash: {txHash[..10]}...) [Queue size: {transactionQueue.Count}]");

                if (submittedCount % 50 == 0)
                {
                    var elapsed = overallStopwatch.Elapsed;
                    var avgTxPerSec = submittedCount / elapsed.TotalSeconds;
                    Console.WriteLine($"[Producer] --- Progress: {submittedCount}/{totalTransactions} submitted | Avg: {avgTxPerSec:F2} tx/sec | Elapsed: {elapsed.TotalSeconds:F1}s ---");
                    Console.WriteLine();
                }
                
                // Add small delay to prevent overwhelming the blockchain's nonce system
                if (i < totalTransactions - 1) // Don't delay after the last transaction
                {
                    await Task.Delay(submissionDelayMs);
                }
            }
            catch (Exception ex)
            {
                statistics.RecordSubmissionFailure();
                Console.WriteLine($"✗ Failed to submit: {ex.Message}");
                
                // On nonce error, add a longer delay before retry
                if (ex.Message.Contains("nonce"))
                {
                    Console.WriteLine($"[Producer] Nonce issue detected, waiting 2 seconds before continuing...");
                    await Task.Delay(2000);
                }
            }
        }

        Console.WriteLine($"[Producer] Finished. Submitted: {submittedCount}/{totalTransactions}. Sending completion signal...");
        
        // Signal completion to consumer
        transactionQueue.Add(completionSignal);
        transactionQueue.CompleteAdding();
    });

    // Wait for both threads to complete
    await Task.WhenAll(producerTask, consumerTask);

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

    statistics.PrintStatistics(overallStopwatch.Elapsed, totalTransactions);
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
    var responseContent = await response.Content.ReadAsStringAsync();
    
    try
    {
        var result = await response.Content.ReadFromJsonAsync<JsonRpcResponse<string>>();

        if (result?.Result == null)
        {
            // Check if there's an error in the response
            var errorResult = await response.Content.ReadFromJsonAsync<JsonRpcErrorResponse>();
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
    catch (Exception ex)
    {
        throw new Exception($"Failed to parse response: {ex.Message}. Response: {responseContent}");
    }
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
    private readonly List<string> _transactionHashes = new();
    private int _submissionFailures = 0;
    private int _miningFailures = 0;
    private readonly object _lock = new();

    public void RecordTransaction(TimeSpan duration, long gasUsed, long blockNumber, string txHash)
    {
        lock (_lock)
        {
            _transactionTimes.Add(duration.TotalSeconds);
            _gasUsed.Add(gasUsed);
            _uniqueBlocks.Add(blockNumber);
            _transactionHashes.Add(txHash);
        }
    }

    public void RecordSubmissionFailure()
    {
        lock (_lock)
        {
            _submissionFailures++;
        }
    }

    public void RecordMiningFailure()
    {
        lock (_lock)
        {
            _miningFailures++;
        }
    }

    public void PrintStatistics(TimeSpan totalTime, int totalAttempted)
    {
        lock (_lock)
        {
            var successfulTx = _transactionTimes.Count;
            var totalFailures = _submissionFailures + _miningFailures;

            Console.WriteLine("=== Performance Statistics ===");
            Console.WriteLine($"Total Attempted: {totalAttempted}");
            Console.WriteLine($"Successful Transactions: {successfulTx}");
            Console.WriteLine($"Submission Failures: {_submissionFailures}");
            Console.WriteLine($"Mining Failures: {_miningFailures}");
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
