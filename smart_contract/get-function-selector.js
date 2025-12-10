const { ethers } = require("hardhat");

async function main() {
    const iface = new ethers.Interface([
        "function bid(string symbol, uint256 orderId)"
    ]);
    
    const selector = iface.getFunction("bid").selector;
    console.log("Function selector for bid(string,uint256):", selector);
}

main().catch((error) => {
    console.error(error);
    process.exitCode = 1;
});
