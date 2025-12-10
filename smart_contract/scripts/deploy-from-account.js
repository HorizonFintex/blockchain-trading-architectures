const hre = require("hardhat");

async function main() {
    const accountAddress = process.argv[2];
    
    if (!accountAddress) {
        console.error("Usage: node deploy-from-account.js <account-address>");
        process.exit(1);
    }
    
    console.log(`Deploying ATS with account: ${accountAddress}`);
    
    const ATS = await hre.ethers.getContractFactory("ATS");
    const deployTx = await ATS.getDeployTransaction();
    
    // Send deployment transaction from specific account
    const tx = await hre.network.provider.send("eth_sendTransaction", [{
        from: accountAddress,
        data: deployTx.data,
        gas: "0x500000"
    }]);
    
    console.log(`Deployment TX: ${tx}`);
    
    // Wait for receipt
    let receipt = null;
    for (let i = 0; i < 10; i++) {
        await new Promise(resolve => setTimeout(resolve, 2000));
        receipt = await hre.network.provider.send("eth_getTransactionReceipt", [tx]);
        if (receipt) break;
    }
    
    if (receipt && receipt.contractAddress) {
        console.log(`ATS deployed to: ${receipt.contractAddress}`);
    } else {
        console.log("Failed to get contract address");
    }
}

main().catch((error) => {
    console.error(error);
    process.exitCode = 1;
});
