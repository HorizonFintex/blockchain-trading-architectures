using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;

Console.WriteLine("=== Unsynchronized Multi-Threaded Multi-Wallet Blockchain Test ===");
Console.WriteLine("Architecture 4: Unsynchronized Multi-Threaded, Multi-Wallet");
Console.WriteLine("Single producer-consumer pair with intelligent wallet-contract-symbol routing.");
Console.WriteLine();

const string rpcUrl = "http://10.41.33.100:8545";
const int totalTransactions = 1000;
const string completionSignal = "COMPLETE";

// Symbol to contract and wallet mapping
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

using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

// Shared queue for transaction hashes between producer and consumer threads
var transactionQueue = new BlockingCollection<string>(new ConcurrentQueue<string>());

// Dictionary to track submission times for each transaction
var submissionTimes = new ConcurrentDictionary<string, DateTime>();

// Statistics
var statistics = new TransactionStatistics();

try
{
    Console.WriteLine("Symbol to Contract and Wallet Mappings:");
    foreach (var kvp in symbolMappings)
    {
        Console.WriteLine($"  {kvp.Key,-8} ? Contract: {kvp.Value.contractAddress} | Wallet: {kvp.Value.walletAddress}");
    }
    Console.WriteLine();
    
    var overallStopwatch = Stopwatch.StartNew();

    Console.WriteLine($"Starting asynchronous submission of {totalTransactions} transactions...");
    Console.WriteLine("Producer thread: Submits transactions with intelligent wallet-contract routing");
    Console.WriteLine("Consumer thread: Polls for mined transactions asynchronously");
    Console.WriteLine("NO artificial delays - each symbol uses its own wallet!");
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
                    Console.WriteLine($"[Consumer] ? Tx {minedCount} mined in block {receipt.BlockNumber} (polling: {pollStopwatch.ElapsedMilliseconds}ms, total: {totalTime.TotalMilliseconds:F0}ms, gas: {receipt.GasUsed:N0})");
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
                Console.WriteLine($"[Consumer] ? Failed to mine {txHash}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"[Consumer] Finished processing. Total mined: {minedCount}");
    });

    // Producer thread (submits transactions)
    var producerTask = Task.Run(async () =>
    {
        int submittedCount = 0;
        
        for (int i = 0; i < totalTransactions; i++)
        {
            try
            {
                // Randomly select a symbol
                var symbol = GetRandomSymbol(symbolMappings.Keys.ToArray());
                var orderId = (ulong)Random.Shared.NextInt64(1000000000, 9999999999);
                
                // Get the correct wallet and contract for this symbol
                var (contractAddress, walletAddress) = symbolMappings[symbol];

                Console.Write($"[Producer] [{i + 1}/{totalTransactions}] Submitting {symbol} order {orderId} (Wallet: {walletAddress[..10]}..., Contract: {contractAddress[..10]}...)... ");

                var txHash = await SubmitBidTransactionAsync(httpClient, rpcUrl, contractAddress, walletAddress, symbol, orderId);
                
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
                
                // NO ARTIFICIAL DELAY - Each symbol uses its own wallet with independent nonce!
            }
            catch (Exception ex)
            {
                statistics.RecordSubmissionFailure();
                Console.WriteLine($"? Failed to submit: {ex.Message}");
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

// Helper Methods

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

static string GetRandomSymbol(string[] symbols)
{
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
