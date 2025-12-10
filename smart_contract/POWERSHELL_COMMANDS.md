# ATS Smart Contract - PowerShell JSON-RPC Commands

## Contract Information
**Contract Address:** `0xffDE7cF853F7a62c8E90c0252A56266bc28C6039`  
**Blockchain URL:** `http://10.41.33.100:8545`

## Function Signatures
- **getValue()** - Function signature: `0x20965255`
- **bid()** - Function signature: `0x1998aeef`

---

## 1. Read the Current Value (getValue)

```powershell
# Read the current counter value
$body = @{
    jsonrpc = "2.0"
    method = "eth_call"
    params = @(
        @{
            to = "0xffDE7cF853F7a62c8E90c0252A56266bc28C6039"
            data = "0x20965255"
        },
        "latest"
    )
    id = 1
} | ConvertTo-Json -Depth 10

$response = Invoke-RestMethod -Uri "http://10.41.33.100:8545" -Method Post -Body $body -ContentType "application/json"
$hexValue = $response.result
$decimalValue = [Convert]::ToInt64($hexValue, 16)
Write-Host "Current counter value: $decimalValue"
```

---

## 2. Place a Bid (Consumes ~157,437 gas)

```powershell
# Get the list of accounts
$accountsBody = @{
    jsonrpc = "2.0"
    method = "eth_accounts"
    params = @()
    id = 1
} | ConvertTo-Json

$accountsResponse = Invoke-RestMethod -Uri "http://10.41.33.100:8545" -Method Post -Body $accountsBody -ContentType "application/json"
$fromAccount = $accountsResponse.result[0]

# Send transaction to place a bid
$body = @{
    jsonrpc = "2.0"
    method = "eth_sendTransaction"
    params = @(
        @{
            from = $fromAccount
            to = "0xffDE7cF853F7a62c8E90c0252A56266bc28C6039"
            data = "0x1998aeef"
            gas = "0x80000"
        }
    )
    id = 1
} | ConvertTo-Json -Depth 10

$response = Invoke-RestMethod -Uri "http://10.41.33.100:8545" -Method Post -Body $body -ContentType "application/json"
$txHash = $response.result
Write-Host "Transaction hash: $txHash"
Write-Host "Bid placed!"
```

---

## 3. Complete Workflow Example

```powershell
# Complete workflow: Read -> Bid -> Read

$rpcUrl = "http://10.41.33.100:8545"
$contractAddress = "0xffDE7cF853F7a62c8E90c0252A56266bc28C6039"

# Function to read value
function Get-CounterValue {
    $body = @{
        jsonrpc = "2.0"
        method = "eth_call"
        params = @(
            @{
                to = $contractAddress
                data = "0x20965255"
            },
            "latest"
        )
        id = 1
    } | ConvertTo-Json -Depth 10
    
    $response = Invoke-RestMethod -Uri $rpcUrl -Method Post -Body $body -ContentType "application/json"
    return [Convert]::ToInt64($response.result, 16)
}

# Function to place a bid
function Invoke-PlaceBid {
    # Get first account
    $accountsBody = @{
        jsonrpc = "2.0"
        method = "eth_accounts"
        params = @()
        id = 1
    } | ConvertTo-Json
    
    $accountsResponse = Invoke-RestMethod -Uri $rpcUrl -Method Post -Body $accountsBody -ContentType "application/json"
    $fromAccount = $accountsResponse.result[0]
    
    # Send transaction
    $body = @{
        jsonrpc = "2.0"
        method = "eth_sendTransaction"
        params = @(
            @{
                from = $fromAccount
                to = $contractAddress
                data = "0x1998aeef"
                gas = "0x80000"
            }
        )
        id = 1
    } | ConvertTo-Json -Depth 10
    
    $response = Invoke-RestMethod -Uri $rpcUrl -Method Post -Body $body -ContentType "application/json"
    return $response.result
}

# Execute workflow
Write-Host "=== ATS Smart Contract Interaction ==="
Write-Host ""

Write-Host "Initial value:"
$initialValue = Get-CounterValue
Write-Host "  $initialValue"
Write-Host ""

Write-Host "Placing bid..."
$txHash = Invoke-PlaceBid
Write-Host "  Transaction: $txHash"
Write-Host ""

# Wait a moment for transaction to be mined
Start-Sleep -Seconds 2

Write-Host "New value:"
$newValue = Get-CounterValue
Write-Host "  $newValue"
Write-Host ""
Write-Host "Value increased by: $($newValue - $initialValue)"
```

---

## 4. Check Gas Used in Transaction

```powershell
# After placing a bid, check the actual gas used
$txHash = "0x..." # Replace with your transaction hash from bid()

$body = @{
    jsonrpc = "2.0"
    method = "eth_getTransactionReceipt"
    params = @($txHash)
    id = 1
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://10.41.33.100:8545" -Method Post -Body $body -ContentType "application/json"
$gasUsedHex = $response.result.gasUsed
$gasUsed = [Convert]::ToInt64($gasUsedHex, 16)
Write-Host "Gas used: $gasUsed"
```

---

## Additional Useful Commands

### Get Transaction Receipt
```powershell
$txHash = "0x..." # Replace with your transaction hash
$body = @{
    jsonrpc = "2.0"
    method = "eth_getTransactionReceipt"
    params = @($txHash)
    id = 1
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://10.41.33.100:8545" -Method Post -Body $body -ContentType "application/json"
$response.result | ConvertTo-Json -Depth 10
```

### Get Block Number
```powershell
$body = @{
    jsonrpc = "2.0"
    method = "eth_blockNumber"
    params = @()
    id = 1
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://10.41.33.100:8545" -Method Post -Body $body -ContentType "application/json"
$blockNumber = [Convert]::ToInt64($response.result, 16)
Write-Host "Current block number: $blockNumber"
```

### Get Account Balance
```powershell
$account = "0x04d7404c04f075a91b6e06d98c53a0c198216d40" # Replace with your account
$body = @{
    jsonrpc = "2.0"
    method = "eth_getBalance"
    params = @($account, "latest")
    id = 1
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://10.41.33.100:8545" -Method Post -Body $body -ContentType "application/json"
$balanceWei = [Convert]::ToInt64($response.result, 16)
$balanceEth = $balanceWei / 1000000000000000000
Write-Host "Balance: $balanceEth ETH"
```

---

## Notes
- The contract is deployed at address: **0xffDE7cF853F7a62c8E90c0252A56266bc28C6039**
- `getValue()` is a view function (read-only, no gas cost)
- `bid()` requires a transaction and consumes approximately **150,059 gas** on warm storage (subsequent calls) due to multiple storage operations
- First call (cold storage) will use more gas (~474,959 gas) due to Ethereum's storage cost model
- The counter starts at 0 and increments by 1 each time
- Each transaction needs to be mined before the new value is visible
- The `bid()` function includes gas-intensive operations like storage writes, mapping updates, and string operations
