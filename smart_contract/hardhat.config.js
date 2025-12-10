require("@nomicfoundation/hardhat-toolbox");

/** @type import('hardhat/config').HardhatUserConfig */
module.exports = {
  solidity: "0.8.27",
  networks: {
    custom: {
      url: "http://10.41.33.100:8545",
      chainId: 1640
    }
  }
};
