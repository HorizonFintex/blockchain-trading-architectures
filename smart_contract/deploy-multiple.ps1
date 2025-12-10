# Deploy 10 ATS contracts and test each one

$rpcUrl = "http://10.41.33.100:8545"
$deployedContracts = @()

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
    return "0x" + $selector + $stringOffset + $orderIdHex + $stringLength + $symbolHex
}

Write-Host "=== Deploying 10 ATS Contracts ==="
Write-Host ""

# Deploy 10 contracts
for ($i = 1; $i -le 10; $i++) {
    Write-Host "Deploying contract $i/10..."
    
    $deployOutput = npx hardhat run scripts/deploy.js --network custom 2>&1 | Out-String
    
    # Extract contract address
    if ($deployOutput -match '0x[a-fA-F0-9]{40}') {
        $address = $Matches[0]
        $deployedContracts += $address
        Write-Host "  ✓ Deployed: $address"
    } else {
        Write-Host "  ✗ Failed to deploy contract $i"
    }
    
    Start-Sleep -Milliseconds 500
}

Write-Host ""
Write-Host "=== Testing Bids on All Contracts ==="
Write-Host ""

# Get account for transactions
$accountsBody = @{
    jsonrpc = "2.0"
    method = "eth_accounts"
    params = @()
    id = 1
} | ConvertTo-Json

$accountsResponse = Invoke-RestMethod -Uri $rpcUrl -Method Post -Body $accountsBody -ContentType "application/json"
$fromAccount = $accountsResponse.result[0]

$successfulContracts = @()

# Test each contract
foreach ($contractAddr in $deployedContracts) {
    Write-Host "Testing $contractAddr..."
    
    $symbol = "TEST"
    $orderId = [uint64](Get-Random -Minimum 1000000000 -Maximum 9999999999)
    $encodedData = Get-EncodedBidData -symbol $symbol -orderId $orderId
    
    try {
        # Send bid transaction
        $body = @{
            jsonrpc = "2.0"
            method = "eth_sendTransaction"
            params = @(
                @{
                    from = $fromAccount
                    to = $contractAddr
                    data = $encodedData
                    gas = "0x80000"
                }
            )
            id = 1
        } | ConvertTo-Json -Depth 10
        
        $response = Invoke-RestMethod -Uri $rpcUrl -Method Post -Body $body -ContentType "application/json"
        $txHash = $response.result
        
        # Wait for transaction
        Start-Sleep -Seconds 2
        
        # Check receipt
        $receiptBody = @{
            jsonrpc = "2.0"
            method = "eth_getTransactionReceipt"
            params = @($txHash)
            id = 1
        } | ConvertTo-Json
        
        $receipt = Invoke-RestMethod -Uri $rpcUrl -Method Post -Body $receiptBody -ContentType "application/json"
        
        if ($receipt.result.status -eq "0x1") {
            Write-Host "  ✓ Bid successful (TX: $txHash)"
            $successfulContracts += $contractAddr
        } else {
            Write-Host "  ✗ Bid failed - status: $($receipt.result.status)"
        }
    } catch {
        Write-Host "  ✗ Error: $_"
    }
}

Write-Host ""
Write-Host "=== VERIFIED CONTRACT ADDRESSES ==="
Write-Host ""

$successfulContracts | ForEach-Object {
    Write-Host $_
}

Write-Host ""
Write-Host "Total verified contracts: $($successfulContracts.Count)/10"
