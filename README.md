# Neo-Resilience
Perform resistance and performance tests for specific NEO builds. It comes with a few batchs of tests, by default runs four predefined tests: consensus, faulty-nodes, normal and bad-day.


## NEO 3.0 dev
In continuous development to adapt to changes in NEO 3.0.

## Features
- Build NEO from the Github code or from the custom compilation.
- Allows selection of specific pull request, source or branch.
- Run custom tests that simulate different network behaviors.
- Generate class diagrams and code documentation.
- Generate detailed reports with test results.
- ...

## Requirements

#### Docker
```
apt-get install apt-transport-https ca-certificates curl software-properties-common
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo apt-key add -
add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"
apt-get install docker-ce
```

#### Docker Compose
```
curl -L "https://github.com/docker/compose/releases/download/1.24.1/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
chmod +x /usr/local/bin/docker-compose
```

## Installation
```
apt-get install git python3 python3-pip
pip3 install docker
git clone https://github.com/Red4Sec/neo-resilience
```

## Usage
```
usage: neo-resilience.py [-h] [-t TESTS_FILE] [-c CUSTOM_BUILD]
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

  --source-neo SOURCE_NEO     Use a specific neo repo
  --source-cli SOURCE_CLI     Use a specific neo-cli repo
  --source-vm SOURCE_VM       Use a specific neo-vm repo
  --source-plg SOURCE_PLG     Use a specific plugins repo

  --branch-neo BRANCH_NEO     Use a specific neo branch
  --branch-cli BRANCH_CLI     Use a specific neo-cli branch
  --branch-vm BRANCH_VM       Use a specific neo-vm branch
  --branch-plg BRANCH_PLG     Use a specific plugins branch

  --pr-neo PR_NEO             Use a specific neo pull request
  --pr-cli PR_CLI             Use a specific neo-cli pull request
  --pr-vm PR_VM               Use a specific neo-vm pull request
  --pr-plg PR_PLG             Use a specific neo plugins pull request

  --code-neo                  Build using github neo code as reference
  --code-vm                   Build using github neo-vm code as reference

  --doc                       Generate code documentation
  --analysis                  Run code analysis
  --show-output               Show output from nodes

```
