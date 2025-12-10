# SequentialAsyncSingleWallet

## Architecture 2: Sequential, Asynchronous, Single-Wallet

This architecture improves upon Architecture 1 by introducing asynchronous processing through a producer-consumer pattern. While transaction submission remains sequential (one at a time), the mining verification is decoupled into a separate thread, allowing the submission thread to continue submitting new transactions without blocking on mining confirmation.

## Key Design Features

- **Producer Thread**: Submits transactions sequentially to the blockchain, waiting only for the initial transaction hash before proceeding to the next submission
- **Consumer Thread**: Runs concurrently, polling for transaction receipts as they become available
- **Blocking Queue**: Thread-safe `BlockingCollection` manages communication between producer and consumer threads
- **Single Wallet**: All transactions use the same blockchain account
- **Completion Signal**: Special marker string ("COMPLETE") signals the consumer thread when all submissions are done
- **Submission Rate Limiting**: 50ms delay between submissions to prevent blockchain nonce distance errors

## Performance Benefit Isolated

This architecture isolates the **Asynchronous Submission Benefit** - specifically demonstrating server-side optimization. By decoupling submission from mining verification:

1. The producer thread can submit transactions as fast as the blockchain accepts them
2. The consumer thread handles the slower mining confirmation process in parallel
3. Overall throughput should improve compared to Architecture 1, as we're not blocking submission on mining

**Important Limitation**: While this architecture enables asynchronous processing, it still encounters blockchain-level constraints. The single wallet approach means all transactions must use sequential nonces, and the blockchain limits how far ahead pending nonces can be (nonce distance limit). This necessitates a submission rate limiter (50ms delay) to prevent "nonce is too distant" errors.

## Expected Performance Characteristics

- **Throughput**: Higher than Architecture 1 due to asynchronous processing
- **Latency**: Similar individual transaction times, but better overall wall-clock time
- **Concurrency**: Two threads working concurrently (producer + consumer)
- **Blockchain Contention**: Still minimal, as only one wallet is used sequentially

## Comparison to Architecture 1

| Metric | Architecture 1 | Architecture 2 |
|--------|---------------|----------------|
| Submission Pattern | Synchronous blocking | Asynchronous (fire and forget) |
| Mining Verification | Inline (blocks next submission) | Separate thread (parallel) |
| Threads | 1 | 2 (producer + consumer) |
| Expected Throughput | ~0.25 tx/sec | Should be higher |
| Queue Depth | N/A | Grows during submission, drains during mining |

## Running the Test

```bash
cd SequentialAsyncSingleWallet
dotnet run
```

## Statistics Tracked

The application tracks and reports the same statistics as Architecture 1:

- Total execution time
- Successful vs failed transactions
- Overall throughput (tx/sec)
- Transaction timing (average, median, min, max, P95, P99)
- Gas usage statistics
- Unique blocks used
- Average transactions per block

## Implementation Details

### Producer Thread
```csharp
for (int i = 0; i < totalTransactions; i++)
{
    var txHash = await SubmitBidTransactionAsync(...);
    submissionTimes[txHash] = DateTime.UtcNow;
    transactionQueue.Add(txHash);  // Non-blocking
}
transactionQueue.Add(completionSignal);
```

### Consumer Thread
```csharp
foreach (var txHash in transactionQueue.GetConsumingEnumerable())
{
    if (txHash == completionSignal) break;
    var receipt = await WaitForTransactionMiningAsync(...);
    statistics.RecordTransaction(...);
}
```

### Thread Safety
- `BlockingCollection<string>` provides thread-safe queue operations
- `ConcurrentDictionary` tracks submission times without locking
- `TransactionStatistics` uses locks to protect shared statistics

## Output Example

```
=== Sequential Asynchronous Single-Wallet Blockchain Test ===
Architecture 2: Sequential, Asynchronous, Single-Wallet
Single submission thread; separate thread polls for mined transactions.

Using account: 0x04d7404c04f075a91b6e06d98c53a0c198216d40

Initial counter value: 495

Starting asynchronous submission of 1000 transactions...
Producer thread: Submits transactions sequentially
Consumer thread: Polls for mined transactions asynchronously

[Producer] [1/1000] Submitting AAPL order 1234567890... Submitted (TxHash: 0x12345678...) [Queue size: 1]
[Producer] [2/1000] Submitting TSLA order 9876543210... Submitted (TxHash: 0xabcdef01...) [Queue size: 2]
[Consumer] ✓ Tx 1/1000 mined in block 12345 (polling: 1523ms, total: 1532ms, gas: 151,687)
[Producer] [3/1000] Submitting GOOGL order 5555555555... Submitted (TxHash: 0x99999999...) [Queue size: 2]
...
```

## Notes

- The blockchain node is at: `http://10.41.33.100:8545`
- Contract address: `0xf4d4FF384b81B1a6041537C14D37A35a698ecAEc`
- Default test size: 1000 transactions
- Queue size fluctuates based on submission vs mining rate
- All transactions use the same wallet account (single-wallet architecture)

## Blockchain Nonce Distance Limitation

This architecture reveals an important blockchain constraint: **nonce distance limits**.

### What is Nonce Distance?

- Each blockchain account has a transaction counter called a **nonce**
- Transactions must be processed in nonce order (0, 1, 2, 3, ...)
- The blockchain's transaction pool (mempool) has a limit on how many pending transactions (how far ahead in nonce sequence) it will accept from a single account
- This limit is typically 64-1000 transactions depending on the blockchain implementation

### Why This Matters for Architecture 2

When submitting transactions rapidly without waiting for mining:
```
Submit Tx (nonce 100) → mempool
Submit Tx (nonce 101) → mempool
Submit Tx (nonce 102) → mempool
...
Submit Tx (nonce 200) → mempool (might succeed)
Submit Tx (nonce 300) → REJECTED! "nonce is too distant"
```

### The Solution: Submission Rate Limiting

We add a 50ms delay between submissions to:
1. Keep the queue size manageable
2. Allow some transactions to mine while submitting
3. Prevent exceeding the nonce distance limit

This means **Architecture 2's performance is still constrained by blockchain limitations**, even though we've decoupled submission from mining verification.

### Performance Impact

Without rate limiting: 
- Submissions would be extremely fast (~10ms each)
- But would fail after ~200 transactions

With 50ms rate limiting:
- Submission rate: ~20 tx/sec
- All 1000 transactions can complete successfully
- Still much better than Architecture 1's ~0.25 tx/sec

This limitation will be addressed in later architectures by using **multiple wallets**, allowing parallel nonce sequences.



============================================================
=== Test Results ===
============================================================
Total Time: 75.67 seconds
Initial Counter Value: 7236
Final Counter Value: 8236
Counter Increased By: 1000

=== Performance Statistics ===
Total Attempted: 1000
Successful Transactions: 1000
Submission Failures: 0
Mining Failures: 0
Total Failures: 0
Success Rate: 100.00%

Overall Throughput: 13.22 tx/sec
Unique Blocks Used: 19
Average Transactions per Block: 52.63

=== Transaction Timing (Submission + Mining) ===
Average: 2.916 seconds
Median: 2.938 seconds
Min: 0.728 seconds
Max: 5.172 seconds
P95: 4.522 seconds
P99: 4.913 seconds

=== Gas Usage ===
Average Gas Used: 146194
Total Gas Used: 146,193,612
Min Gas: 146,068
Max Gas: 151,704

