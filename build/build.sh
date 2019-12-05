#!/bin/bash

# Params
SOURCE_NEO=https://github.com/neo-project/neo
SOURCE_CLI=https://github.com/neo-project/neo-cli
SOURCE_PLG=https://github.com/neo-project/neo-plugins
SOURCE_VM=https://github.com/neo-project/neo-vm
BRANCH_NEO=master
BRANCH_CLI=master
BRANCH_VM=master
BRANCH_PLG=master
PR_NEO=0
PR_CLI=0
PR_VM=0
PR_PLG=0

# TODO: getopt long arguments
while getopts w:x:y:z:n:c:p:v:o:i:g:m:a,b,d,q option; do
    case "${option}" in
        w) SOURCE_NEO=${OPTARG};;
        x) SOURCE_CLI=${OPTARG};;
        y) SOURCE_VM=${OPTARG};;
        z) SOURCE_PLG=${OPTARG};;
        n) BRANCH_NEO=${OPTARG};;
        c) BRANCH_CLI=${OPTARG};;
        v) BRANCH_VM=${OPTARG};;
        p) BRANCH_PLG=${OPTARG};;
        o) PR_NEO=${OPTARG};;
        i) PR_CLI=${OPTARG};;
        m) PR_VM=${OPTARG};;
        g) PR_PLG=${OPTARG};;
        a) CODE_NEO=1;;
        b) CODE_VM=1;;
        d) DOC_GEN=1;;
        q) SC_ANA=1;;
    esac
done

# Source dirs
rm -rf /src /build/neo-cli/*
mkdir /src

# Header
echo "--------------------------------------------------------"
echo "   SETTINGS "
echo "--------------------------------------------------------"
echo "SOURCE_NEO=$SOURCE_NEO"
echo "SOURCE_CLI=$SOURCE_CLI"
echo "SOURCE_VM=$SOURCE_VM"
echo "SOURCE_PLG=$SOURCE_PLG"
echo "BRANCH_NEO=$BRANCH_NEO"
echo "BRANCH_CLI=$BRANCH_CLI"
echo "BRANCH_VM=$BRANCH_VM"
echo "BRANCH_PLG=$BRANCH_PLG"
echo "PR_NEO=$PR_NEO"
echo "PR_CLI=$PR_CLI"
echo "PR_VM=$PR_VM"
echo "PR_PLG=$PR_PLG"
echo "CODE_NEO=$CODE_NEO"
echo "CODE_VM=$CODE_VM"
echo "DOC_GEN=$DOC_GEN"
echo "SC_ANA=$SC_ANA"
echo "--------------------------------------------------------"
echo "   BUILD LOG "
echo "--------------------------------------------------------"

# neo-cli
git clone --single-branch --branch $BRANCH_CLI $SOURCE_CLI /src/neo-cli
if [[ $PR_CLI -ne 0 ]]; then
    cd /src/neo-cli
    git fetch origin refs/pull/$PR_CLI/head:pr_$PR_CLI
    git checkout pr_$PR_CLI
fi

# neo-plugins
#git clone --single-branch --branch $BRANCH_PLG $SOURCE_PLG /src/neo-plugins
#if [[ $PR_PLG -ne 0 ]]; then
#    cd /src/neo-plugins
#    git fetch origin refs/pull/$PR_PLG/head:pr_$PR_PLG
#    git checkout pr_$PR_PLG
#fi

# neo-core
if [[ $PR_NEO -ne 0 || $CODE_NEO -eq 1  || $CODE_VM -eq 1 || $BRANCH_NEO != "master" || $PR_VM -ne 0 ]]; then
    git clone --single-branch --branch $BRANCH_NEO $SOURCE_NEO /src/neo
    if [[ $PR_NEO -ne 0 ]]; then
        cd /src/neo
        git fetch origin refs/pull/$PR_NEO/head:pr_$PR_NEO
        git checkout pr_$PR_NEO
    fi
    dotnet remove /src/neo-cli/neo-cli/neo-cli.csproj package neo
    dotnet sln /src/neo-cli/neo-cli.sln add /src/neo/src/neo/neo.csproj
    dotnet add /src/neo-cli/neo-cli/neo-cli.csproj reference /src/neo/src/neo/neo.csproj
fi

# neo-vm
if [[ $PR_VM -ne 0 || $CODE_VM -eq 1 || $BRANCH_VM != "master" ]]; then
    git clone --single-branch --branch $BRANCH_VM $SOURCE_VM /src/neo-vm
    if [[ $PR_VM -ne 0 ]]; then
        cd /src/neo-vm
        git fetch origin refs/pull/$PR_VM/head:pr_$PR_VM
        git checkout pr_$PR_VM
    fi
    dotnet remove /src/neo/src/neo/neo.csproj package neo.vm
    dotnet sln /src/neo-cli/neo-cli.sln add /src/neo-vm/src/neo-vm/neo-vm.csproj
    dotnet add /src/neo/src/neo/neo.csproj reference /src/neo-vm/src/neo-vm/neo-vm.csproj
fi

# Documentation
if [[ $DOC_GEN -eq 1 ]]; then
    doxygen /doc.config
fi

# Analysis
if [[ -f "/analysis.xml" && $SC_ANA -eq 1 ]]; then
    cd /
    dotnet tool install --global dotnet-sonarscanner
    dotnet-sonarscanner begin /k:"NEO" /s:"/analysis.xml"
    dotnet build /src/neo-cli/neo-cli.sln -r ubuntu.16.04-x64
    dotnet-sonarscanner end
fi

# Build
dotnet publish /src/neo-cli/neo-cli/neo-cli.csproj --verbosity normal -o neo-cli -c Release -r ubuntu.16.04-x64
#dotnet publish /src/neo-plugins/LevelDBStore/LevelDBStore.csproj -o LevelDBStore -c Release -r ubuntu.16.04-x64 -f netstandard2.1

# Output binaries
if [[ -d "/src/neo-cli/neo-cli/bin/Release/netcoreapp3.0/ubuntu.16.04-x64/" ]]; then
    mv /src/neo-cli/neo-cli/bin/Release/netcoreapp3.0/ubuntu.16.04-x64/* /build/neo-cli
fi
#if [[ -d "/src/neo-plugins/LevelDBStore/bin/Release/netstandard2.1/ubuntu.16.04-x64/" ]]; then
#    mkdir /build/neo-cli/Plugins
#    mv /src/neo-plugins/LevelDBStore/bin/Release/netstandard2.1/ubuntu.16.04-x64/* /build/neo-cli/Plugins
#fi

