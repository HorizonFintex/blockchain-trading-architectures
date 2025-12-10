# ATS Smart Contract - PowerShell JSON-RPC Commands (Updated with Parameters)

## Contract Information
**Contract Address:** `0xf4d4FF384b81B1a6041537C14D37A35a698ecAEc`  
**Blockchain URL:** `http://10.41.33.100:8545`

## Function Signatures
- **getValue()** - Function signature: `0x20965255`
- **bid(string,uint256)** - Function signature: `0x5f483313`

---

## 1. Read the Current Value (getValue)

```powershell
# Read the current counter value
$body = @{
    jsonrpc = "2.0"
    method = "eth_call"
    params = @(
        @{
            to = "0xf4d4FF384b81B1a6041537C14D37A35a698ecAEc"
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

## 2. Place a Bid with Parameters

### Helper Function to Encode String and UInt256

```powershell
function Get-EncodedBidData {
    param(
        [string]$symbol,
        [uint64]$orderId
    )
    
    # Function selector for bid(string,uint256)
    $selector = "5f483313"
    
    # Encode offset to string data (0x40 = 64 bytes, after offset and orderId)
    $stringOffset = "0000000000000000000000000000000000000000000000000000000000000040"
    
    # Encode orderId as uint256 (pad to 32 bytes)
    $orderIdHex = $orderId.ToString("X64")
    
    # Encode string length
    $symbolBytes = [System.Text.Encoding]::UTF8.GetBytes($symbol)
    $stringLength = $symbolBytes.Length.ToString("X64")
    
    # Encode string data (pad to multiple of 32 bytes)
    $symbolHex = [System.BitConverter]::ToString($symbolBytes).Replace("-", "").ToLower()
    $paddingNeeded = (32 - ($symbolBytes.Length % 32)) % 32
    $symbolHex += "0" * ($paddingNeeded * 2)
    
    # Combine all parts
    $encodedData = "0x" + $selector + $stringOffset + $orderIdHex + $stringLength + $symbolHex
    
    return $encodedData
}
```

### Example: Place a Bid

```powershell
$rpcUrl = "http://10.41.33.100:8545"
$contractAddress = "0xf4d4FF384b81B1a6041537C14D37A35a698ecAEc"

# Get the list of accounts
$accountsBody = @{
    jsonrpc = "2.0"
    method = "eth_accounts"
    params = @()
    id = 1
} | ConvertTo-Json

$accountsResponse = Invoke-RestMethod -Uri $rpcUrl -Method Post -Body $accountsBody -ContentType "application/json"
$fromAccount = $accountsResponse.result[0]

# Encode the bid data with parameters
$symbol = "AAPL"
$orderId = [uint64](Get-Random -Minimum 1000000000 -Maximum 9999999999)
$encodedData = Get-EncodedBidData -symbol $symbol -orderId $orderId

Write-Host "Placing bid with symbol: $symbol, orderId: $orderId"

# Send transaction to place a bid
$body = @{
    jsonrpc = "2.0"
    method = "eth_sendTransaction"
    params = @(
        @{
            from = $fromAccount
            to = $contractAddress
            data = $encodedData
            gas = "0x80000"
        }
    )
    id = 1
} | ConvertTo-Json -Depth 10

$response = Invoke-RestMethod -Uri $rpcUrl -Method Post -Body $body -ContentType "application/json"
$txHash = $response.result
Write-Host "Transaction hash: $txHash"
Write-Host "Bid placed!"
```

---

## 3. Complete Workflow Example

```powershell
# Complete workflow: Read -> Bid -> Read

$rpcUrl = "http://10.41.33.100:8545"
$contractAddress = "0xf4d4FF384b81B1a6041537C14D37A35a698ecAEc"

# Helper function to encode bid data
function Get-EncodedBidData {
    param(
        [string]$symbol,
        [uint64]$orderId
    )
    
    $selector = "5f483313"
    $stringOffset = "0000000000000000000000000000000000000000000000000000000000000040"
    $orderIdHex = $orderId.ToString("X64")
    $symbolBytes = [System.Text.Encoding]::UTF8.GetBytes($symbol)
    $stringLength = $symbolBytes.Length.ToString("X64")
    $symbolHex = [System.BitConverter]::ToString($symbolBytes).Replace("-", "").ToLower()
    $paddingNeeded = (32 - ($symbolBytes.Length % 32)) % 32
    $symbolHex += "0" * ($paddingNeeded * 2)
    $encodedData = "0x" + $selector + $stringOffset + $orderIdHex + $stringLength + $symbolHex
    
    return $encodedData
}

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
    param(
        [string]$symbol,
        [uint64]$orderId
    )
    
    # Get first account
    $accountsBody = @{
        jsonrpc = "2.0"
        method = "eth_accounts"
        params = @()
        id = 1
    } | ConvertTo-Json
    
    $accountsResponse = Invoke-RestMethod -Uri $rpcUrl -Method Post -Body $accountsBody -ContentType "application/json"
    $fromAccount = $accountsResponse.result[0]
    
    # Encode the bid data
    $encodedData = Get-EncodedBidData -symbol $symbol -orderId $orderId
    
    # Send transaction
    $body = @{
        jsonrpc = "2.0"
        method = "eth_sendTransaction"
        params = @(
            @{
                from = $fromAccount
                to = $contractAddress
                data = $encodedData
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

$symbol = "TSLA"
$orderId = [uint64](Get-Random -Minimum 1000000000 -Maximum 9999999999)

Write-Host "Placing bid with symbol: $symbol, orderId: $orderId"
$txHash = Invoke-PlaceBid -symbol $symbol -orderId $orderId
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
- The contract is deployed at address: **0xf4d4FF384b81B1a6041537C14D37A35a698ecAEc**
- `getValue()` is a view function (read-only, no gas cost)
- `bid(string,uint256)` now requires two parameters:
  - **symbol**: A string (max 10 characters) representing the trading symbol
  - **orderId**: A uint256 representing a random order ID
- `bid()` requires a transaction and consumes approximately **150,059+ gas** on warm storage (subsequent calls)
- First call (cold storage) will use more gas due to Ethereum's storage cost model
- The counter starts at 0 and increments by 1 each time
- Each transaction needs to be mined before the new value is visible
- The parameters are encoded using ABI encoding:
  - Function selector: 4 bytes
  - String offset: 32 bytes (points to where string data starts)
  - OrderId: 32 bytes (uint256)
  - String length: 32 bytes
  - String data: padded to multiple of 32 bytes
