#!/bin/bash

# Params
SOURCE_NEO=https://github.com/neo-project/neo
SOURCE_CLI=https://github.com/neo-project/neo-node
SOURCE_MODS=https://github.com/neo-project/neo-modules
SOURCE_VM=https://github.com/neo-project/neo-vm
BRANCH_NEO=master
BRANCH_CLI=master
BRANCH_VM=master
BRANCH_PLG=master
PR_NEO=0
PR_CLI=0
PR_VM=0
PR_MODS=0
BUILD=Release
TARGET=ubuntu.16.04-x64

# TODO: getopt long arguments
while getopts w:x:y:z:n:c:p:v:o:i:g:m:t:a,b,e,d,q option; do
    case "${option}" in
        w) SOURCE_NEO=${OPTARG};;
        x) SOURCE_CLI=${OPTARG};;
        y) SOURCE_VM=${OPTARG};;
        z) SOURCE_MODS=${OPTARG};;
        n) BRANCH_NEO=${OPTARG};;
        c) BRANCH_CLI=${OPTARG};;
        v) BRANCH_VM=${OPTARG};;
        p) BRANCH_PLG=${OPTARG};;
        o) PR_NEO=${OPTARG};;
        i) PR_CLI=${OPTARG};;
        m) PR_VM=${OPTARG};;
        g) PR_MODS=${OPTARG};;
        a) CODE_NEO=1;;
        b) CODE_VM=1;;
        e) BUILD=Debug;;
        t) TARGET=${OPTARG};;
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
echo "SOURCE_MODS=$SOURCE_MODS"
echo "BRANCH_NEO=$BRANCH_NEO"
echo "BRANCH_CLI=$BRANCH_CLI"
echo "BRANCH_VM=$BRANCH_VM"
echo "BRANCH_PLG=$BRANCH_PLG"
echo "PR_NEO=$PR_NEO"
echo "PR_CLI=$PR_CLI"
echo "PR_VM=$PR_VM"
echo "PR_MODS=$PR_MODS"
echo "CODE_NEO=$CODE_NEO"
echo "CODE_VM=$CODE_VM"
echo "BUILD=$BUILD"
echo "TARGET=$TARGET"
echo "DOC_GEN=$DOC_GEN"
echo "SC_ANA=$SC_ANA"
echo "--------------------------------------------------------"
echo "   CODE CLONE "
echo "--------------------------------------------------------"

# neo-cli
git clone --verbose --single-branch --branch $BRANCH_CLI $SOURCE_CLI /src/neo-node
if [[ $PR_CLI -ne 0 ]]; then
    cd /src/neo-node
    git fetch origin --verbose refs/pull/$PR_CLI/head:pr_$PR_CLI
    git checkout pr_$PR_CLI
fi

# neo-modules
git clone --verbose --single-branch --branch $BRANCH_PLG $SOURCE_MODS /src/neo-modules
if [[ $PR_MODS -ne 0 ]]; then
    cd /src/neo-modules
    git fetch origin --verbose refs/pull/$PR_MODS/head:pr_$PR_MODS
    git checkout pr_$PR_MODS
fi

# neo-core
if [[ $PR_NEO -ne 0 || $CODE_NEO -eq 1  || $CODE_VM -eq 1 || $BRANCH_NEO != "master" || $PR_VM -ne 0 ]]; then
    git clone --verbose --single-branch --branch $BRANCH_NEO $SOURCE_NEO /src/neo
    if [[ $PR_NEO -ne 0 ]]; then
        cd /src/neo
        git fetch origin --verbose refs/pull/$PR_NEO/head:pr_$PR_NEO
        git checkout pr_$PR_NEO
    fi
    dotnet remove /src/neo-node/neo-cli/neo-cli.csproj package neo
    dotnet sln /src/neo-node/neo-node.sln add /src/neo/src/neo/neo.csproj
    dotnet add /src/neo-node/neo-cli/neo-cli.csproj reference /src/neo/src/neo/neo.csproj
fi

# neo-vm
if [[ $PR_VM -ne 0 || $CODE_VM -eq 1 || $BRANCH_VM != "master" ]]; then
    git clone --verbose --single-branch --branch $BRANCH_VM $SOURCE_VM /src/neo-vm
    if [[ $PR_VM -ne 0 ]]; then
        cd /src/neo-vm
        git fetch origin --verbose refs/pull/$PR_VM/head:pr_$PR_VM
        git checkout pr_$PR_VM
    fi
    dotnet remove /src/neo/src/neo/neo.csproj package neo.vm
    dotnet sln /src/neo-node/neo-node.sln add /src/neo-vm/src/neo-vm/neo-vm.csproj
    dotnet add /src/neo/src/neo/neo.csproj reference /src/neo-vm/src/neo-vm/neo-vm.csproj
fi

# Documentation
if [[ $DOC_GEN -eq 1 ]]; then
    echo "--------------------------------------------------------"
    echo "   DOCUMENTATION "
    echo "--------------------------------------------------------"
    doxygen /doc.config
fi

# Analysis
if [[ -f "/analysis.xml" && $SC_ANA -eq 1 ]]; then
    echo "--------------------------------------------------------"
    echo "   ANALYSIS "
    echo "--------------------------------------------------------"
    cd /
    dotnet sln /src/neo-node/neo-node.sln remove /src/neo-node/neo-gui/neo-gui.csproj
    dotnet tool install --global dotnet-sonarscanner
    dotnet-sonarscanner begin /k:"NEO" /s:"/analysis.xml"
    dotnet build /src/neo-node/neo-node.sln -r $TARGET
    dotnet-sonarscanner end
fi

# Build
echo "--------------------------------------------------------"
echo "   BUILD "
echo "--------------------------------------------------------"
dotnet publish /src/neo-node/neo-cli/neo-cli.csproj --verbosity normal -o neo-cli -c $BUILD -r $TARGET
dotnet publish /src/neo-modules/src/LevelDBStore/LevelDBStore.csproj -o LevelDBStore -c $BUILD -r $TARGET -f netstandard2.1
#dotnet publish /src/neo-modules/src/RpcServer/RpcServer.csproj -o RpcServer -c $BUILD -r $TARGET -f netstandard2.1
dotnet publish /src/neo-modules/src/SystemLog/SystemLog.csproj -o SystemLog -c $BUILD -r $TARGET -f netstandard2.1

# Output binaries
if [[ -d "/src/neo-node/neo-cli/bin/$BUILD/netcoreapp3.0/$TARGET/" ]]; then
    mv /src/neo-node/neo-cli/bin/$BUILD/netcoreapp3.0/$TARGET/* /build/neo-cli
fi
if [[ -d "/src/neo-modules/src/LevelDBStore/bin/$BUILD/netstandard2.1/$TARGET/" ]]; then
    mkdir -p /build/neo-cli/Plugins
    mv /src/neo-modules/src/LevelDBStore/bin/$BUILD/netstandard2.1/$TARGET/* /build/neo-cli/Plugins
fi
if [[ -d "/src/neo-modules/src/RpcServer/bin/$BUILD/netstandard2.1/$TARGET/" ]]; then
    mkdir -p /build/neo-cli/Plugins
    mv /src/neo-modules/src/RpcServer/bin/$BUILD/netstandard2.1/$TARGET/* /build/neo-cli/Plugins
fi
if [[ -d "/src/neo-modules/src/SystemLog/bin/$BUILD/netstandard2.1/$TARGET/" ]]; then
    mkdir -p /build/neo-cli/Plugins
    mv /src/neo-modules/src/SystemLog/bin/$BUILD/netstandard2.1/$TARGET/* /build/neo-cli/Plugins
fi
