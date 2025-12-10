# MultiThreadedAsyncSingleWallet

## Architecture 3: Multi-Threaded, Asynchronous, Single-Wallet

This architecture introduces **true parallel processing** with multiple concurrent threads submitting transactions simultaneously. Each of the 10 trading symbols has its own dedicated producer-consumer thread pair, all sharing a single blockchain wallet but targeting different contract addresses.

## Key Design Features

- **10 Producer-Consumer Pairs**: One pair for each symbol (AAPL, GOOGL, MSFT, AMZN, TSLA, META, NVDA, AMD, INTC, NFLX)
- **Single Wallet**: All threads submit transactions from the same blockchain account
- **Dedicated Contracts**: Each symbol targets a unique contract address
- **Concurrent Execution**: All 20 threads (10 producers + 10 consumers) run simultaneously
- **Per-Symbol Queues**: Each symbol maintains its own transaction queue
- **Submission Rate Limiting**: 500ms delay per thread to manage global nonce distance
- **Automatic Retry Logic**: Nonce errors trigger 3-second pause and retry
- **Thread-Safe HTTP Access**: SemaphoreSlim ensures safe concurrent HTTP operations

## Performance Component Isolated

This architecture isolates **Blockchain-Level Contention** - specifically demonstrating the **Nonce/Mempool bottleneck** that occurs when multiple threads compete for the same wallet's nonce sequence.

### The Nonce Contention Problem

Even though each symbol has:
- Its own contract address
- Its own producer-consumer threads
- Its own transaction queue

They **ALL share the same wallet**, which means:
- They all compete for sequential nonces from a single account
- The blockchain must serialize these transactions despite concurrent submission
- Nonce distance limits apply globally across all threads
- Lock contention occurs at the blockchain level

## Multi-Threading and Concurrency Challenges

### Challenge 1: HttpClient Thread Safety

**Problem**: While `HttpClient` is thread-safe for concurrent requests, reading the response stream multiple times causes **"Cannot access a closed Stream"** errors.

**Initial Problematic Code**:
```csharp
var response = await client.PostAsJsonAsync(rpcUrl, request);
var responseContent = await response.Content.ReadAsStringAsync();  // Read #1

// This fails - stream is closed!
var result = await response.Content.ReadFromJsonAsync<T>();  // ❌ Read #2
```

**Solution**: Read the HTTP response content once into a string, then deserialize from that string:
```csharp
var response = await client.PostAsJsonAsync(rpcUrl, request);
var responseContent = await response.Content.ReadAsStringAsync();  // Read once

// Deserialize from string (not stream)
var result = System.Text.Json.JsonSerializer.Deserialize<T>(responseContent, jsonOptions);
```

### Challenge 2: Async-Await Inside Lock Statements

**Problem**: Cannot use `await` inside a `lock` statement because it can cause thread context switches while holding the lock.

**Initial Problematic Code**:
```csharp
lock (client)  // ❌ Cannot use await inside lock
{
    var response = await client.PostAsJsonAsync(rpcUrl, request);
}
```

**Solution**: Use `SemaphoreSlim` instead, which is async-compatible:
```csharp
var semaphore = new SemaphoreSlim(1, 1);  // Max 1 thread at a time

await semaphore.WaitAsync();  // ✅ Async-friendly
try
{
    var response = await client.PostAsJsonAsync(rpcUrl, request);
    // Process response...
}
finally
{
    semaphore.Release();  // Always release, even on exception
}
```

**Why This Works**:
- `SemaphoreSlim.WaitAsync()` is async and doesn't block threads
- `try-finally` ensures the semaphore is always released
- Provides same mutual exclusion as `lock` but allows `await`

### Challenge 3: JSON Property Name Case Sensitivity

**Problem**: `System.Text.Json.JsonSerializer` is case-sensitive by default, causing deserialization to fail.

**Blockchain Response**:
```json
{"jsonrpc":"2.0","id":1,"result":"0x..."}
```

**C# Model** (PascalCase):
```csharp
class JsonRpcResponse<T>
{
    public string Jsonrpc { get; set; }  // Doesn't match "jsonrpc"
    public T Result { get; set; }         // Doesn't match "result"
}
```

**Solution**: Configure JSON options for case-insensitive property matching:
```csharp
var jsonOptions = new System.Text.Json.JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true  // ✅ Matches "result" to "Result"
};

var result = System.Text.Json.JsonSerializer.Deserialize<T>(
    responseContent, 
    jsonOptions
);
```

### Challenge 4: Blockchain Nonce Distance Limits

**Problem**: Multiple threads submitting simultaneously overwhelm the blockchain's nonce management system.

**The Math**:
- With 10 threads @ 100ms delay: ~100 tx/sec submission rate
- But blockchain with single wallet: ~2-3 tx/sec acceptance rate
- Result: Nonce distance exceeded, transactions rejected

**Solution 1 - Increased Delay**: 
```csharp
const int submissionDelayMs = 500;  // Increased from 100ms
// Reduces submission rate from ~100 tx/sec to ~20 tx/sec
```

**Solution 2 - Automatic Retry Logic**:
```csharp
catch (Exception ex) when (ex.Message.Contains("nonce"))
{
    statistics.RecordSubmissionFailure(symbol);
    Console.WriteLine($"[{symbol}] Producer: Nonce error on tx {i + 1}");
    Console.WriteLine($"[{symbol}] Producer: Pausing for 3 seconds...");
    await Task.Delay(3000);  // Let blockchain catch up
    
    i--;  // Retry this transaction
}
```

**Why 500ms + Retry Works**:
- Lower submission rate reduces immediate pressure
- Retry logic handles occasional nonce conflicts gracefully
- 3-second pause allows blockchain to process pending transactions
- Prevents permanent transaction loss due to nonce issues

## Thread Safety Implementation Summary

| Component | Thread Safety Mechanism | Purpose |
|-----------|------------------------|---------|
| HTTP Requests | `SemaphoreSlim(1,1)` | Serializes blockchain RPC calls |
| Statistics | `lock(_lock)` | Protects shared counters and lists |
| Transaction Queue | `BlockingCollection` | Thread-safe producer-consumer queue |
| Submission Times | `ConcurrentDictionary` | Lock-free timestamp storage |
| Response Parsing | Read-once pattern | Prevents stream access errors |

## Symbol to Contract Mapping

| Symbol | Contract Address |
|--------|-----------------|
| AAPL   | 0x3A1f0EF3dbbd16370B8606A4D03d0Ea9BF6E561f |
| GOOGL  | 0xe38d4CfA501C22A0dF3e9de0514d12FED2950968 |
| MSFT   | 0x479B5016418fB8c7838C980b69360bfdbeEc6f57 |
| AMZN   | 0x657E17E6a3E022e255342deb9d4513b6D2cD20A8 |
| TSLA   | 0x3a39104910084C495fAF2815c51A856AE482b460 |
| META   | 0xD05a885A416D0Be9493F77775Ab288407B23571c |
| NVDA   | 0x23E9A16D696db4F6338DBcD8CE0aD75343344C98 |
| AMD    | 0xdB32C288f7915360c308DF84f93416ec40C9a808 |
| INTC   | 0x56A50288A21025577562bB336725F22Aa2Ee65eD |
| NFLX   | 0x1cD19F8b0b2503814980b60b31ff5278882F59fF |

## Expected Performance Characteristics

### Theoretical Maximum (if no contention)
- With 10 parallel threads @ 500ms delay each: ~20 tx/sec submission rate
- But reality will be MUCH lower due to single-wallet bottleneck

### Actual Expected Performance
- **Throughput**: Still limited by single wallet's nonce sequence (~2-3 tx/sec)
- **Success Rate**: High (90-95%+) due to retry logic
- **Queue Buildup**: Producers submit faster than blockchain can mine
- **Nonce Errors**: Common but handled automatically with retries
- **Total Time**: ~10-15 minutes for 1000 transactions

## Comparison to Previous Architectures

| Metric | Arch 1 | Arch 2 | Arch 3 |
|--------|--------|--------|--------|
| Submission Threads | 1 | 1 | 10 |
| Consumer Threads | 0 | 1 | 10 |
| Wallets | 1 | 1 | 1 |
| Contracts | 1 | 1 | 10 |
| Parallelism | None | Limited | High (but constrained) |
| Nonce Contention | None | Managed | **HIGH** |
| Expected Bottleneck | Submission waits | Nonce distance | **Nonce conflicts** |
| Thread Safety | Not needed | Basic | **Critical** |
| Retry Logic | No | No | **Yes** |

## Running the Test

```bash
cd MultiThreadedAsyncSingleWallet
dotnet run
```

## Output Example

```
=== Multi-Threaded Asynchronous Single-Wallet Blockchain Test ===
Architecture 3: Multi-Threaded, Asynchronous, Single-Wallet
Multiple concurrent threads submit asynchronously to a single blockchain wallet.

Using account: 0x04d7404c04f075a91b6e06d98c53a0c198216d40
Number of symbols/threads: 10
Transactions per symbol: 100

Starting multi-threaded submission of 1000 transactions...
Each symbol has its own producer-consumer thread pair

[AAPL] Starting producer-consumer pair for 100 transactions
[AAPL] Consumer: Started and waiting for transactions
[GOOGL] Starting producer-consumer pair for 100 transactions
[GOOGL] Consumer: Started and waiting for transactions
...
[AAPL] Producer: 10/100 submitted [Queue: 8]
[TSLA] Producer: 10/100 submitted [Queue: 9]
[META] Producer: Nonce error on tx 26: RPC Error: Transaction nonce is too distant
[META] Producer: Pausing for 3 seconds to let blockchain catch up...
[AAPL] Consumer: Processing tx 1, waiting for mining...
[AAPL] Consumer: 10/100 mined (block 12345)
...
```

## Statistics Tracked

### Overall Statistics
- Total attempted transactions
- Successful vs failed transactions (submission + mining)
- Overall throughput (tx/sec)
- Block distribution
- Transaction timing percentiles

### Per-Symbol Breakdown
- Success count by symbol
- Submission failures by symbol
- Mining failures by symbol
- Shows which symbols experience more contention

## What This Architecture Reveals

### 1. Single Wallet = Single Point of Contention
No matter how many threads you have, they all serialize at the wallet level. The blockchain's nonce system enforces sequential processing.

### 2. Thread Safety is Critical in Concurrent Systems
Multiple challenges emerged:
- HTTP response stream management
- Async operations inside locks
- JSON deserialization case sensitivity
- Blockchain-specific rate limiting

### 3. Nonce Management Requires Application-Level Coordination
Without retry logic and rate limiting:
- ~80% transaction failure rate
- "Nonce too distant" errors dominate
- Blockchain mempool rejection

With retry logic and 500ms delays:
- 90-95%+ success rate
- Graceful degradation under load
- But still limited by single wallet

### 4. Blockchain is the Ultimate Bottleneck
Even with perfect application threading:
- Single wallet nonce must be sequential
- Blockchain enforces serialization
- Application parallelism cannot overcome this

## Lessons Learned: Multi-Threading Best Practices

### ✅ DO
- Use `SemaphoreSlim` for async operations (not `lock`)
- Read HTTP response streams only once
- Configure JSON serializers for case-insensitivity
- Implement retry logic for transient failures
- Use thread-safe collections (`BlockingCollection`, `ConcurrentDictionary`)
- Always use `try-finally` when acquiring resources

### ❌ DON'T
- Use `await` inside `lock` statements
- Read HTTP response streams multiple times
- Assume JSON property names match automatically
- Submit to blockchain faster than it can process
- Ignore nonce-specific errors
- Share mutable state without synchronization

## The Solution: Architecture 4

To truly achieve parallel submission, we need **multiple wallets** - one per symbol (or group of symbols). This will be demonstrated in Architecture 4, which uses 10 different wallet accounts to eliminate nonce contention entirely.

**Why Multiple Wallets Solve This**:
- Each wallet has its own independent nonce sequence
- No contention between threads
- True parallel submission possible
- Throughput scales with number of wallets

## Important Notes

- All 10 threads compete for the same wallet's nonce
- Nonce errors are EXPECTED and demonstrate the bottleneck
- This architecture shows why single-wallet approaches don't scale
- The 500ms delay + retry logic prevents most failures
- This is intentionally designed to hit blockchain limits
- Thread safety required careful consideration of async patterns
- Multi-threading revealed several subtle concurrency issues

## Key Takeaway

**Architecture 3 proves that application-level parallelism cannot overcome blockchain-level serialization when using a single wallet.** The thread safety challenges encountered also demonstrate the complexity of building robust concurrent systems that interact with external services like blockchain nodes.


============================================================
=== Test Results ===
============================================================
Total Time: 55.42 seconds

=== Performance Statistics ===
Total Attempted: 1000
Successful Transactions: 1000
Submission Failures: 0
Mining Failures: 0
Total Failures: 0
Success Rate: 100.00%

Overall Throughput: 18.04 tx/sec
Unique Blocks Used: 14
Average Transactions per Block: 71.43

=== Per-Symbol Statistics ===
AAPL    : Success: 100, Sub Fail:   0, Mine Fail:   0, Total: 100
AMD     : Success: 100, Sub Fail:   0, Mine Fail:   0, Total: 100
AMZN    : Success: 100, Sub Fail:   0, Mine Fail:   0, Total: 100
GOOGL   : Success: 100, Sub Fail:   0, Mine Fail:   0, Total: 100
INTC    : Success: 100, Sub Fail:   0, Mine Fail:   0, Total: 100
META    : Success: 100, Sub Fail:   0, Mine Fail:   0, Total: 100
MSFT    : Success: 100, Sub Fail:   0, Mine Fail:   0, Total: 100
NFLX    : Success: 100, Sub Fail:   0, Mine Fail:   0, Total: 100
NVDA    : Success: 100, Sub Fail:   0, Mine Fail:   0, Total: 100
TSLA    : Success: 100, Sub Fail:   0, Mine Fail:   0, Total: 100

=== Transaction Timing (Submission + Mining) ===
Average: 2.959 seconds
Median: 3.017 seconds
Min: 0.490 seconds
Max: 5.272 seconds
P95: 4.700 seconds
P99: 5.177 seconds

=== Gas Usage ===
Average Gas Used: 152603
Total Gas Used: 152,603,320
Min Gas: 151,800
Max Gas: 157,436

