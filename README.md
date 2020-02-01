<p align="center">
  <a href="https://red4sec.com" target="_blank"><img src="https://red4sec.com/images/logo.png" width="200px"></a>
</p>
<h1 align="center">
Neo-Resilience
</h1>

<p align="center">
 Neo-Resilience platform tests resistance and performance for specific NEO builds.
</p>

## Table of Contents

- [Features](#features)
- [NEO 3](#neo-3)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)

## Features

- NEO blockchain from scratch.
- Build from Github projects, custom repos or from local binaries.
- Selection of specific pull request, source or branch.
- Run custom tests that simulate different network behaviors.
- Set of common tests.
- Generate class diagrams and code documentation.
- Generate detailed reports with test results.
- ...

## NEO 3

In continuous development to adapt to changes in __NEO 3.x__.

*NEO 2.x has been archived in branch [master-2.x](https://github.com/Red4Sec/neo-resilience/tree/master-2.x "master-2.x").*

## Requirements

#### Docker

```console
apt-get install apt-transport-https ca-certificates curl software-properties-common
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo apt-key add -
add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"
apt-get install docker-ce
```

#### Docker Compose

```console
curl -L "https://github.com/docker/compose/releases/download/1.24.1/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
chmod +x /usr/local/bin/docker-compose
```

## Installation

```console
apt-get install git python3 python3-pip
pip3 install docker
git clone https://github.com/Red4Sec/neo-resilience
```

## Usage

```
usage: neo-resilience.py [-h] [-t TESTS_FILE] [-c CUSTOM_BUILD] [-i ID]
                         [--source-neo SOURCE_NEO] [--source-cli SOURCE_CLI]
                         [--source-vm SOURCE_VM] [--source-plg SOURCE_PLG]
                         [--branch-neo BRANCH_NEO] [--branch-cli BRANCH_CLI]
                         [--branch-vm BRANCH_VM] [--branch-plg BRANCH_PLG]
                         [--pr-neo PR_NEO] [--pr-cli PR_CLI] [--pr-vm PR_VM]
                         [--pr-plg PR_PLG] [--code-neo] [--code-vm] [--doc]
                         [--analysis] [--show-output]

optional arguments:
  -h, --help                  show this help message and exit

  -t TESTS_FILE, --tests-file TESTS_FILE          JSON tests file
  -c CUSTOM_BUILD, --custom-build CUSTOM_BUILD    ZIP neo-cli
  -i ID, --id ID                                  Job ID

  --source-neo SOURCE_NEO     Use a specific neo repo
  --source-cli SOURCE_CLI     Use a specific neo-cli repo
  --source-vm SOURCE_VM       Use a specific neo-vm repo
  --source-mods SOURCE_MODS   Use a specific neo modules repo

  --branch-neo BRANCH_NEO     Use a specific neo branch
  --branch-cli BRANCH_CLI     Use a specific neo-cli branch
  --branch-vm BRANCH_VM       Use a specific neo-vm branch
  --branch-mods BRANCH_MODS   Use a specific neo modules branch

  --pr-neo PR_NEO             Use a specific neo pull request
  --pr-cli PR_CLI             Use a specific neo-cli pull request
  --pr-vm PR_VM               Use a specific neo-vm pull request
  --pr-mods PR_MODS           Use a specific neo modules pull request

  --code-neo                  Build using github neo code as reference
  --code-vm                   Build using github neo-vm code as reference

  --doc                       Generate code documentation
  --analysis                  Run code analysis
  --show-output               Show output from nodes

```
