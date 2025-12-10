const hre = require("hardhat");

async function main() {
    const deployerAddress = process.env.DEPLOYER_ADDRESS;
    
    if (!deployerAddress) {
        throw new Error("DEPLOYER_ADDRESS environment variable not set");
    }
    
    // Get the signer for the specific address
    await hre.network.provider.request({
        method: "hardhat_impersonateAccount",
        params: [deployerAddress],
    });
    
    const signer = await hre.ethers.getSigner(deployerAddress);
    
    console.log(`Deploying ATS contract with account: ${deployerAddress}`);
    
    const ATS = await hre.ethers.getContractFactory("ATS", signer);
    const ats = await ATS.deploy();
    
    await ats.waitForDeployment();
    
    const address = await ats.getAddress();
    console.log(`ATS deployed to: ${address}`);
}

main().catch((error) => {
    console.error(error);
    process.exitCode = 1;
});
