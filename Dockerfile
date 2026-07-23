# Multi-stage Dockerfile for Blockchain Trading Architecture Benchmark
# Build Stage: Compile all 5 architectures + smart contracts
# Runtime Stage: Minimal .NET runtime image for execution

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src

# Copy entire project structure
COPY BlockchainArchitecturePerformanceTesting/ ./BlockchainArchitecturePerformanceTesting/
COPY smart_contract/ ./smart_contract/
COPY . .

# Build all 5 architecture projects
RUN dotnet build BlockchainArchitecturePerformanceTesting/BlockchainArchitecturePerformanceTesting.slnx -c Release

# Runtime Stage
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

# Copy compiled binaries from builder
COPY --from=builder /src/BlockchainArchitecturePerformanceTesting/bin/Release/ ./

# Default: Run Architecture 5 (Symbol-Sharded, Lock-Free, Multi-Wallet)
ENTRYPOINT ["dotnet", "SymbolShardedLockFreeMultiWallet/bin/Release/net10.0/SymbolShardedLockFreeMultiWallet.dll"]

# Usage:
# docker build -t blockchain-architectures .
# docker run blockchain-architectures
