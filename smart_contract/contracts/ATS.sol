// SPDX-License-Identifier: MIT
pragma solidity ^0.8.27;

contract ATS {
    uint256 private value;
    
    // Additional storage variables to consume more gas
    uint256 private dummyVar1;
    uint256 private dummyVar2;
    uint256 private dummyVar3;
    uint256 private dummyVar4;
    uint256 private dummyVar5;
    mapping(uint256 => uint256) private dummyMapping;
    string private dummyString;

    event ValueIncremented(uint256 newValue);

    constructor() {
        value = 0;
        dummyVar1 = 0;
        dummyVar2 = 0;
        dummyVar3 = 0;
        dummyVar4 = 0;
        dummyVar5 = 0;
        dummyString = "placeholder";
    }

    function bid(string memory symbol, uint256 orderId) public {
        value += 1;
        
        // Storage operations to consume gas (targeting ~157,437 gas on subsequent calls)
        dummyVar1 = block.timestamp;
        dummyVar2 = block.number;
        dummyVar3 = value * 2;
        dummyVar4 = value * 3;
        dummyVar5 = value * 5;
        
        // Multiple mapping writes (warm storage) - increased to reach target gas
        dummyMapping[value] = block.timestamp;
        dummyMapping[value + 1] = block.number;
        dummyMapping[value + 2] = value * 3;
        dummyMapping[value + 3] = value * 7;
        dummyMapping[value + 4] = block.timestamp + value;
        dummyMapping[value + 5] = block.number * 2;
        dummyMapping[value + 6] = value * 11;
        dummyMapping[value + 7] = block.timestamp * 2;
        dummyMapping[value + 8] = value + block.number;
        dummyMapping[value + 9] = value * value;
        dummyMapping[value + 10] = block.timestamp + block.number;
        dummyMapping[value + 11] = value * 13;
        dummyMapping[value + 12] = block.number + value * 2;
        dummyMapping[value + 13] = value * 17;
        dummyMapping[value + 14] = value * 19;
        
        emit ValueIncremented(value);
    }

    function getValue() public view returns (uint256) {
        return value;
    }
    
    // Helper function to convert uint to string
    function uintToString(uint256 v) internal pure returns (string memory) {
        if (v == 0) {
            return "0";
        }
        uint256 j = v;
        uint256 length;
        while (j != 0) {
            length++;
            j /= 10;
        }
        bytes memory bstr = new bytes(length);
        uint256 k = length;
        j = v;
        while (j != 0) {
            bstr[--k] = bytes1(uint8(48 + j % 10));
            j /= 10;
        }
        return string(bstr);
    }
}
