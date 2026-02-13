# Frends.Mllp.Receive

Frends task that spins up a lightweight MLLP server, listens for HL7 messages for a configurable time window, and returns every received message in the result.

[![Receive_build](https://github.com/FrendsPlatform/Frends.Mllp/actions/workflows/Receive_build_and_test_on_main.yml/badge.svg)](https://github.com/FrendsPlatform/Frends.Mllp/actions/workflows/Receive_build_and_test_on_main.yml)
![Coverage](https://app-github-custom-badges.azurewebsites.net/Badge?key=FrendsPlatform/Frends.Mllp/Frends.Mllp.Receive|main)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)

## Installing

You can install the Task via Frends UI Task View.

## Building

### Clone a copy of the repository

`git clone https://github.com/FrendsPlatform/Frends.Mllp.git`

### Build the project

`dotnet build`

### Run tests

Run the tests

`dotnet test`

### Create a NuGet package

`dotnet pack --configuration Release`

### Third-party licenses

SuperSocket used for the MLLP server implementation is licensed under the Apache-2.0 license. 
The full license text and source code can be found at https://github.com/kerryjiang/SuperSocket.

### StyleCop.Analyzers Version

This project uses StyleCop.Analyzers 1.2.0-beta.556, as recommended by the author, to get the latest fixes and improvements not available in the last stable release.