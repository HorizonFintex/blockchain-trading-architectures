# Artifact Reproduction Guide

**Artifact Title:** A Lock Free, Non-blocking, In Line Processing Architecture for High Throughput and Regulatory Compliant Blockchain Trading Applications

**Authors:** Andrew Le Gear, Jim Buckley, Tawny Whatmore, Ashish Sai

**Venue:** ECSA 2026 Industrial Track + Open Science Track

**Repository:** https://github.com/HorizonFintex/blockchain-trading-architectures

---

## Overview

This artifact provides reference implementations of five blockchain transaction processing architectures, ranging from sequential synchronous processing to a novel lock-free, symbol-sharded design. All implementations achieve regulatory-compliant, deterministic transaction ordering while exploring throughput trade-offs.

## Contents

```
blockchain-trading-architectures/
├── BlockchainArchitecturePerformanceTesting/
│   ├── SequentialBlockingSingleThreaded/          (Architecture 1)
│   ├── SequentialAsyncSingleWallet/               (Architecture 2)
│   ├── MultiThreadedAsyncSingleWallet/            (Architecture 3)
│   ├── UnsynchronizedMultiThreadedMultiWallet/    (Architecture 4)
│   ├── SymbolShardedLockFreeMultiWallet/          (Architecture 5 - Proposed)
│   └── BlockchainArchitecturePerformanceTesting.slnx
├── smart_contract/                                 (Solidity + Hardhat)
├── docker/
│   └── README.md                                  (Docker quick-start)
├── docs/
│   ├── ARTIFACT_README.md                         (This file)
│   ├── RESULTS_SUMMARY.md                         (Data validation)
│   └── ARCHITECTURE_COMPARISON.md                 (Detailed analysis)
├── images/                                         (Performance graphs)
├── results/                                        (Output directory for benchmarks)
├── Dockerfile
├── docker-compose.yml
├── LICENSE                                         (MIT)
└── README.md
```

## Requirements

### Option A: Docker (Recommended - Reproducible)
- **Docker Desktop:** https://www.docker.com/products/docker-desktop
- **Disk space:** ~2 GB
- **OS:** Windows, Linux, macOS (Docker-agnostic)

### Option B: Native .NET
- **.NET 10 SDK:** https://dotnet.microsoft.com/download
- **PowerShell 7+** (Windows) or bash (Linux/macOS)
- **Disk space:** ~500 MB
- **OS:** Windows, Linux, macOS

---

## Blockchain Setup (Handled by Docker)

The benchmark architectures submit transactions to a blockchain RPC endpoint. The **recommended approach** is to use Docker Compose, which automatically starts a Hardhat node and deploys contracts for you.

### ✅ Recommended: Docker Compose (Batteries Included)

Everything is pre-configured:

```bash
# Terminal 1: Start Hardhat node (stays running)
docker-compose up hardhat-node

# Terminal 2: Run any architecture
docker-compose run arch5-symbol-sharded
```

**That's it!** No manual blockchain setup required. Hardhat automatically:
- Deploys ERC-20 token contracts
- Creates 20 pre-funded accounts (10,000 ETH each)
- Listens on http://localhost:8545
- Persists data until you run `docker-compose down`

See [docker/README.md](../docker/README.md) for full Docker usage guide.

---

### Alternative: Manual Blockchain Setup

If you prefer to build and run locally (not in Docker), you have two options below.

#### Option A: Local Private Blockchain (Hardhat - Manual)

**For reproducible, zero-cost testing without Docker.**

Prerequisites:
- Node.js 16+ and npm
- Hardhat already in `smart_contract/` directory

Setup:

```bash
cd smart_contract
npm install

# Terminal 1: Start Hardhat node
npx hardhat node

# Terminal 2: Deploy contracts
npx hardhat run scripts/deploy-multiple.ps1
```

Update RPC endpoint in C# code:
- Default: `http://10.41.33.100:8545`
- Local: `http://localhost:8545`

Edit each architecture's `Program.cs` or use environment variable:
```powershell
$env:RPC_URL="http://localhost:8545"
```

---

#### Option B: Public Testnet (Sepolia or Holesky)

**For testing against live, persistent blockchain infrastructure.**

| Network | RPC Endpoint | Faucet |
|---------|-------------|--------|
| **Sepolia** | `https://sepolia.infura.io/v3/YOUR_API_KEY` | https://www.alchemy.com/faucets/ethereum-sepolia |
| **Holesky** | `https://holesky.infura.io/v3/YOUR_API_KEY` | https://faucet.holesky.ethpandaops.io |

Setup:

1. **Get Infura API Key** (free at https://infura.io)
2. **Set RPC endpoint** in code:
   ```csharp
   const string RpcUrl = "https://sepolia.infura.io/v3/YOUR_API_KEY";
   ```
   Or environment variable:
   ```powershell
   $env:RPC_URL="https://sepolia.infura.io/v3/YOUR_API_KEY"
   ```
3. **Fund test accounts** (each needs ~0.1 ETH) via faucet
4. **Deploy contracts** (optional):
   ```bash
   cd smart_contract
   npx hardhat run scripts/deploy-multiple.ps1 --network sepolia
   ```

---

### Quick Comparison

| Approach | Setup Time | Block Time | Cost | Recommended For |
|----------|------------|-----------|------|-----------------|
| **Docker (Recommended)** | ~1 min | ~1 sec | Free | Evaluation, demos, CI/CD |
| Local Hardhat | ~2 min | ~1 sec | Free | Development, testing |
| Sepolia testnet | ~5 min | 12-15 sec | Free | Persistence, real-world testing |
| Holesky testnet | ~5 min | 12-15 sec | Free | Persistence, real-world testing |

**For ECSA 2026 artifact evaluation: Use Docker.** It's self-contained, reproducible, and requires no manual blockchain setup.

## Reproduction Steps

### Step 1: Clone or Extract the Artifact

```bash
git clone https://github.com/HorizonFintex/blockchain-trading-architectures.git
cd blockchain-trading-architectures
```

### Step 2: Start Hardhat Blockchain (Docker)

```bash
# Terminal 1: Start Hardhat node
docker-compose up hardhat-node

# Output will show:
# - Contract deployment: DEPLOY_CONTRACT_RESULT: success
# - Accounts: Wallet 0: 0x... Account 0: 0x...
# - RPC listening on 0.0.0.0:8545
```

The Hardhat node will:
- Deploy ERC-20 token and order book contracts
- Create 20 pre-funded test accounts
- Listen on http://localhost:8545
- Wait for architecture services to connect

### Step 3: Run Architecture 5 (Lock-Free, Proposed)

#### Using Docker (Recommended)

```bash
# Terminal 2: Run Architecture 5
docker-compose run arch5-symbol-sharded
```

**Expected output (Arch 5):**
- Throughput: ~31.19 tx/sec
- Execution time: ~32 seconds for 1000 transactions
- Per-symbol ordering preserved
- Output file: `results/arch5-results.txt`

#### Using Native .NET (If Not Using Docker)

```bash
# Build all projects
cd BlockchainArchitecturePerformanceTesting
dotnet build -c Release

# Set RPC endpoint and run Architecture 5
$env:RPC_URL="http://localhost:8545"
dotnet run --project SymbolShardedLockFreeMultiWallet --configuration Release
```

### Step 4: Verify Results

1. **Check output log:** `results/arch5-results.txt`
2. **Validate metrics:** Compare against Table 1 (see `RESULTS_SUMMARY.md`)
   - Throughput should be within ±5% of reported values
   - Execution time should be within ±10% (varies by hardware)
3. **Inspect transaction ordering:** Each symbol lane processes transactions in order (guaranteed by lock-free algorithm)

### Step 5: Run All Five Architectures (Comparative Benchmark)

```bash
# Docker option
docker-compose run arch1-sequential-blocking
docker-compose run arch2-sequential-async
docker-compose run arch3-multithreaded-async
docker-compose run arch4-unsync-multithread
docker-compose run arch5-symbol-sharded

# Or native .NET
for arch in Sequential Async MultiThreaded Unsync SymbolSharded; do
  dotnet run --project ${arch}* --configuration Release
done
```

---

## Data Dictionary

### Output Files

Each architecture run produces:

- **`results/arch-X-results.txt`** — Raw transaction log
  - Columns: `TransactionID | Timestamp | Symbol | Amount | Status | Latency(ms)`
  
- **`results/arch-X-summary.json`** — Statistical summary
  - Fields: `throughput_tx_per_sec`, `total_time_sec`, `avg_latency_ms`, `tx_per_block`, `block_count`

### Key Metrics

| Metric | Definition |
|--------|-----------|
| **Throughput** | Transactions processed per second (1000 tx / total_time) |
| **Execution Time** | Wall-clock duration to process 1000 transactions |
| **Avg Latency** | Mean time from transaction submission to block inclusion |
| **Tx/Block** | Average transactions per block (1000 / block_count) |
| **Determinism** | Per-symbol ordering guaranteed without global locks (Arch 5) |

---

## Validation Against Published Paper

See [RESULTS_SUMMARY.md](RESULTS_SUMMARY.md) for side-by-side comparison of artifact results vs. Table 1 (published paper).

**Expected fidelity:** Artifact results should match published values within:
- Throughput: ±5%
- Execution time: ±10% (hardware-dependent)
- Ordering: 100% deterministic (by design)

---

## Customization

### Adjust Transaction Volume

Edit `docker-compose.yml` environment variable:

```yaml
environment:
  - TRANSACTIONS=5000    # Default: 1000
```

Or set via command line:

```bash
docker-compose run -e TRANSACTIONS=5000 arch5-symbol-sharded
```

### Modify Concurrency Parameters

Open `BlockchainArchitecturePerformanceTesting/Program.cs`:

```csharp
const int ProducerCount = 10;      // Number of concurrent producers
const int ConsumerCount = 10;      // Number of concurrent consumers
const int TransactionCount = 1000; // Total transactions
```

Rebuild and rerun.

---

## Interpretation

### Why Lock-Free?

Lock-free algorithms guarantee non-blocking progress: even if one thread is preempted, others can continue. This is critical for regulatory compliance (no stalled transactions) and high throughput (no lock contention).

### Why Symbol-Sharding?

Financial regulations (e.g., MiFID II, SEC) require deterministic audit trails per asset. Symbol-based partitioning aligns with this requirement while enabling parallel processing of unrelated asset pairs.

### Expected Variance

Results may vary ±10% due to:
- OS scheduler jitter
- CPU frequency scaling
- Memory allocation patterns
- Background processes

All variance is captured in the `*_summary.json` files.

---

## Architecture Comparison

Refer to `ARCHITECTURE_COMPARISON.md` for detailed analysis of design trade-offs, code complexity, and correctness proofs.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Docker build fails | Run `docker-compose build --no-cache` |
| Dotnet command not found | Ensure .NET 10 SDK is installed and in PATH |
| Out of memory | Reduce `ProducerCount` and `ConsumerCount` or `TRANSACTIONS` |
| Results don't match paper | Check CPU thermal throttling, close background apps, rerun 3 times and average |
| Permission denied (Linux) | Run `sudo usermod -aG docker $USER` and restart terminal |

---

## Citation

If you use this artifact, please cite:

```bibtex
@inproceedings{LeGear2026,
  author = {Le Gear, Andrew and Buckley, Jim and Whatmore, Tawny and Sai, Ashish},
  title = {A Lock Free, Non-blocking, In Line Processing Architecture for High Throughput and Regulatory Compliant Blockchain Trading Applications},
  booktitle = {Proceedings of the European Software Architecture Conference (ECSA 2026)},
  address = {Bolzano, Italy},
  month = {September},
  year = {2026}
}
```

---

## Support

For questions, issues, or feedback:
- GitHub Issues: https://github.com/HorizonFintex/blockchain-trading-architectures/issues
- Email: andrew.legear@ul.ie

---

## License

MIT License. See `LICENSE` file in the repository root.
