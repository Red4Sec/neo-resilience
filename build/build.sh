#!/bin/bash

# Repos
NEO=https://github.com/neo-project/neo
NEO_CLI=https://github.com/neo-project/neo-cli
NEO_VM=https://github.com/neo-project/neo-vm
NEO_PLUGINS=https://github.com/neo-project/neo-plugins

# Source dirs
rm -rf /src /build/neo-cli/*
mkdir /src

# Clone repos
git clone $NEO /src/neo
git clone $NEO_CLI /src/neo-cli
git clone $NEO_VM /src/neo-vm
git clone $NEO_PLUGINS /src/neo-plugins

# Info header
echo NEO:$1 CLI:$2 VM:$3 PLG:$4 NEO-CODE:$5 VM-CODE:$6

# Pull Requests
if [[ $1 -ne 0 ]] ; then (cd /src/neo && git fetch origin refs/pull/$1/head:pr_$1 && git checkout pr_$1); fi
if [[ $2 -ne 0 ]] ; then (cd /src/neo-cli && git fetch origin refs/pull/$2/head:pr_$2 && git checkout pr_$2); fi
if [[ $3 -ne 0 ]] ; then (cd /src/neo-vm && git fetch origin refs/pull/$3/head:pr_$3 && git checkout pr_$3); fi
if [[ $4 -ne 0 ]] ; then (cd /src/neo-plugins && git fetch origin refs/pull/$4/head:pr_$4 && git checkout pr_$4); fi

# References
if [[ $5 -ne 0 ]]; then
    dotnet remove /src/neo-cli/neo-cli/neo-cli.csproj package neo
    dotnet sln /src/neo-cli/neo-cli.sln add /src/neo/neo/neo.csproj
    dotnet add /src/neo-cli/neo-cli/neo-cli.csproj reference /src/neo/neo/neo.csproj
fi

if [[ $6 -ne 0 ]]; then
    dotnet remove /src/neo/neo/neo.csproj package neo.vm
    dotnet sln /src/neo/neo.sln add /src/neo-vm/src/neo-vm/neo-vm.csproj
    dotnet add /src/neo/neo/neo.csproj reference /src/neo-vm/src/neo-vm/neo-vm.csproj
fi

# Analysis
if [[ -f "/analysis.xml" ]]; then
    dotnet tool install --global dotnet-sonarscanner
    dotnet-sonarscanner begin /k:"NEO" /s:"/analysis.xml"
    dotnet build /src/neo-cli/neo-cli.sln -r ubuntu.16.04-x64
    dotnet-sonarscanner end
fi

# Build
dotnet publish /src/neo-cli/neo-cli/neo-cli.csproj -o neo-cli -c Release -r ubuntu.16.04-x64
dotnet publish /src/neo-plugins/SimplePolicy/SimplePolicy.csproj -o SimplePolicy -c Release -r ubuntu.16.04-x64 -f netstandard2.0

# Output binaries
mv /src/neo-cli/neo-cli/neo-cli/* /build/neo-cli
mkdir /build/neo-cli/Plugins
mv /src/neo-plugins/SimplePolicy/bin/Release/netstandard2.0/ubuntu.16.04-x64/* /build/neo-cli/Plugins