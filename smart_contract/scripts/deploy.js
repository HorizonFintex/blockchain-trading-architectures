const hre = require("hardhat");

async function main() {
  console.log("Deploying ATS contract...");

  const ATS = await hre.ethers.getContractFactory("ATS");
  const ats = await ATS.deploy();

  await ats.waitForDeployment();

  const address = await ats.getAddress();
  console.log(`ATS deployed to: ${address}`);
}

main()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  });
