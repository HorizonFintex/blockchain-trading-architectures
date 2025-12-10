// SPDX-License-Identifier: MIT
pragma solidity ^0.8.27;

contract Counter {
    uint256 private value;

    event ValueIncremented(uint256 newValue);

    constructor() {
        value = 0;
    }

    function increment() public {
        value += 1;
        emit ValueIncremented(value);
    }

    function getValue() public view returns (uint256) {
        return value;
    }
}
