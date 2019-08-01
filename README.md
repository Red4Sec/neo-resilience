# Neo-Resilience
Perform resistance and performance tests for specific NEO builds. It comes with four predefined tests: consensus, faulty-nodes, normal, bad-network.

# NEO 3.0 dev TODO
- NeoStats Plugin.
- Save stats and consensus logs.
- Generate a 3.0 chain.
- Wallet txgen.

## Features
- Build NEO from the Github code or from the custom compilation.
- Allows selection of specific pull request.
- Run custom tests that simulate different network behaviors.
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
curl -L "https://github.com/docker/compose/releases/download/1.24.0/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
chmod +x /usr/local/bin/docker-compose
```

## Installation
```
apt-get install git python3 python3-pip
pip3 install docker
git clone https://github.com/belane/neo-resilience
```

## Usage
```
usage: neo-resilience.py [-h] [-t TESTS_FILE] [-c CUSTOM_BUILD]
                         [--pr-neo PR_NEO] [--pr-cli PR_CLI] [--pr-vm PR_VM]
                         [--pr-plg PR_PLG] [--code-neo] [--code-vm]
                         [--show-output]

optional arguments:
  -h, --help            show this help message and exit
  -t TESTS_FILE, --tests-file TESTS_FILE    JSON tests file
  -c CUSTOM_BUILD, --custom-build CUSTOM_BUILD    ZIP neo-cli
  --pr-neo PR_NEO       Select a specific neo pull request
  --pr-cli PR_CLI       Select a specific neo-cli pull request
  --pr-vm PR_VM         Select a specific neo-vm pull request
  --pr-plg PR_PLG       Select a specific neo plugins pull request
  --code-neo            Build using github neo code as reference
  --code-vm             Build using github neo-vm code as reference
  --show-output         Show output from nodes

```

## License
GPLv3
