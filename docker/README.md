# Docker Quick-Start Guide

## Overview

This guide uses Docker Compose to run a complete, self-contained benchmarking environment:
- **Hardhat Node** — Local blockchain running on port 8545 with pre-funded accounts
- **All 5 Architecture Services** — Ready to benchmark transaction throughput

## Prerequisites

- **Docker Desktop** installed (https://www.docker.com/products/docker-desktop)
- **Docker Compose** (included with Docker Desktop)
- ~3GB disk space for build + Hardhat node

## Quick Start (One Command)

```bash
cd c:\dev\blockchain-trading-architectures
docker-compose up hardhat-node
```

Then in another terminal:
```bash
docker-compose run arch5-symbol-sharded
```

**That's it!** Hardhat starts automatically, contracts deploy, and Architecture 5 runs.

---

## Build All Architectures

```bash
cd c:\dev\blockchain-trading-architectures
docker-compose build
```

This builds a single Docker image containing all 5 C# architecture executables + a Hardhat service.

---

## Run Individual Architectures

Each architecture automatically waits for Hardhat to be healthy before running.

### Architecture 1: Sequential, Synchronous, Single-Wallet (Baseline)
```bash
docker-compose run arch1-sequential-blocking
```
Expected output: ~4000 seconds, 0.25 tx/sec throughput

**Note:** This takes 60+ minutes. Consider skipping or testing with reduced TRANSACTIONS.

### Architecture 2: Sequential, Asynchronous, Single-Wallet
```bash
docker-compose run arch2-sequential-async
```
Expected output: ~75 seconds, 13.22 tx/sec throughput

### Architecture 3: Multi-Threaded, Asynchronous, Single-Wallet
```bash
docker-compose run arch3-multithreaded-async
```
Expected output: ~55 seconds, 18.04 tx/sec throughput

### Architecture 4: Unsynchronized Multi-Threaded, Multi-Wallet
```bash
docker-compose run arch4-unsync-multithread
```
Expected output: ~41 seconds, 24.16 tx/sec throughput

### Architecture 5: Symbol-Sharded, Lock-Free, Multi-Wallet (Proposed)
```bash
docker-compose run arch5-symbol-sharded
```
Expected output: ~32 seconds, 31.19 tx/sec throughput

---

## Run All Architectures (Comparative Benchmark)

```bash
# Start Hardhat in the background
docker-compose up -d hardhat-node

# Wait for it to be healthy (check: docker-compose ps)
# Then run each architecture sequentially

docker-compose run arch2-sequential-async
docker-compose run arch3-multithreaded-async
docker-compose run arch4-unsync-multithread
docker-compose run arch5-symbol-sharded

# Optionally: skip Arch 1 (baseline) as it takes 60+ minutes
```

---

## Hardhat Node (Development Blockchain)

### Start Node Only

```bash
docker-compose up hardhat-node
```

Hardhat will:
- Create 20 pre-funded accounts (10,000 ETH each)
- Deploy ERC-20 token and order book contracts
- Listen on `http://localhost:8545` (exposed outside Docker)

### Inspect Hardhat Console

```bash
docker logs blockchain-hardhat-node
```

View deployed contracts, accounts, and initialization output.

### Stop Hardhat

```bash
docker-compose down hardhat-node
```

---

## Output

Results are written to `./results/` directory on the host:

- `arch5-results.txt` — Transaction log (one per run)
- `arch5-summary.json` — Performance statistics (throughput, latency, block count)

Compare outputs against Table 1 (published paper) in `RESULTS_SUMMARY.md`.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "hardhat-node is unhealthy" | Wait 20 seconds (Hardhat takes time to initialize). Check logs: `docker logs blockchain-hardhat-node` |
| "Cannot connect to Docker daemon" | Restart Docker Desktop |
| Port 8545 already in use | Change in docker-compose.yml: `ports: - "8546:8545"` |
| "npm: not found" | Hardhat image (Node 20) is downloading. Wait 1 minute on first run. |
| Results don't match paper | Check `ARTIFACT_README.md` for expected variance (±5-10%). Run 3 times and average. |
| High memory usage | Reduce TRANSACTIONS in docker-compose.yml: `- TRANSACTIONS=100` |

---

## Advanced Usage

### Reduce Transaction Count (Faster Testing)

Edit `docker-compose.yml`:
```yaml
environment:
  - TRANSACTIONS=100    # Default: 1000
```

Or set via command line:
```bash
docker-compose run -e TRANSACTIONS=50 arch5-symbol-sharded
```

### Custom RPC Endpoint (Not Recommended)

If you want to use Sepolia testnet instead of local Hardhat:

```bash
docker-compose run -e RPC_URL=https://sepolia.infura.io/v3/YOUR_KEY arch5-symbol-sharded
```

But note: You'll need to pre-fund accounts and deploy contracts manually on testnet.

---

## Architecture of the Docker Setup

```
docker-compose up
│
├─ hardhat-node (Node.js + Hardhat)
│  ├─ Runs: npx hardhat node
│  ├─ Deploys: smart_contract/*
│  ├─ Port: 8545 (http://localhost:8545)
│  └─ Provides: 20 pre-funded accounts, ERC-20 token, order book
│
└─ architecture services (arch1, arch2, ..., arch5)
   ├─ Wait for hardhat-node to be healthy
   ├─ Connect to http://hardhat-node:8545 (Docker network)
   ├─ Submit 1000 transactions
   └─ Write results to ./results/
```

All services communicate via a Docker bridge network. No external dependencies required.

---

## Notes

- Hardhat node resets when `docker-compose down` is called
- All 20 accounts are pre-funded; pick any wallet address from logs
- Contracts are deployed fresh on each Hardhat startup
- To make results persistent, save `./results/` files outside Docker

---

## Performance Baseline

On a typical laptop:
- Hardhat startup: 10-20 seconds
- Architecture 5 (1000 tx): 30-40 seconds
- Total time: ~1 minute per benchmark

---

For detailed reproduction instructions and expected output validation, see `ARTIFACT_README.md`.
