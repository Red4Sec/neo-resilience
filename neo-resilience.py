#!/usr/bin/env python3
# -*- coding: utf-8 -*-
#
# Neo Resilience
# Version: 0.4.4
# https://github.com/red4sec/neo-resilience


import argparse
import json
import zipfile
import signal
import sys
from time import time, sleep
from os import path
from shutil import copyfile, make_archive
from lib import dockercontrol
from lib import nodehelper
from lib import batch


def show_banner():
    banner = '''\033[1;32m
                                  _
     ___ ___ ___    ___ ___ ___ _| |_ ___ ___ ___ ___
    |   | -_| . |  |  _| -_|_ -| | | | -_|   |  _| -_|
    |_|_|___|___|  |_| |___|___|_|_|_|___|_|_|___|___|
    \033[0;0m
    '''
    print(banner)


def force_exit(signum, frame):
    print('\n[!] Aborting...\n')
    dc.neo_net_down()
    dc.stop_interactive()
    sys.exit(0)


show_banner()
signal.signal(signal.SIGINT, force_exit)

parser = argparse.ArgumentParser(description='neo-resilience - operational testing platform')

parser.add_argument('-t', '--tests-file', type=str, default='tests/default-tests.json', help='JSON tests file')
parser.add_argument('-c', '--custom-build', type=str, help='ZIP neo-cli')
parser.add_argument('-i', '--id', type=int, default=int(time()), help='Job ID')

parser.add_argument('--source-neo', type=str, help='Use a specific neo repo')
parser.add_argument('--source-cli', type=str, help='Use a specific neo-cli repo')
parser.add_argument('--source-vm', type=str, help='Use a specific neo-vm repo')
parser.add_argument('--source-mods', type=str, help='Use a specific neo modules repo')

parser.add_argument('--branch-neo', type=str, default='master', help='Use a specific neo branch')
parser.add_argument('--branch-cli', type=str, default='master', help='Use a specific neo-cli branch')
parser.add_argument('--branch-vm', type=str, default='master', help='Use a specific neo-vm branch')
parser.add_argument('--branch-mods', type=str, default='master', help='Use a specific neo modules branch')

parser.add_argument('--pr-neo', type=int, default=0, help='Use a specific neo pull request')
parser.add_argument('--pr-cli', type=int, default=0, help='Use a specific neo-cli pull request')
parser.add_argument('--pr-vm', type=int, default=0, help='Use a specific neo-vm pull request')
parser.add_argument('--pr-mods', type=int, default=0, help='Use a specific neo modules pull request')

parser.add_argument('--code-neo', action='store_true', help='Build using github neo code as reference')
parser.add_argument('--code-vm', action='store_true', help='Build using github neo-vm code as reference')

parser.add_argument('--debug', action='store_true', help='Debug binaries compilation')
parser.add_argument('--target', type=str, default='ubuntu.16.04-x64', help='Target binaries compilation')

parser.add_argument('--doc', action='store_true', help='Generate code documentation')
parser.add_argument('--analysis', action='store_true', help='Run code analysis')

parser.add_argument('--show-output', action='store_true', help='Show output from nodes')
parser.add_argument('--skip-build', action='store_true', help='Skip neo-cli compilation')
parser.add_argument('--interactive-node', action='store_true', help='Run an interactive node')

args = parser.parse_args()

if args.interactive_node:
    args.show_output = False

dc = dockercontrol.DockerControl()
batch = batch.Batch(dc, args)

print('[i] Batch ID: {}'.format(batch.id))

if(args.custom_build):
    print('[+] Using custom neo-cli build')
    with zipfile.ZipFile(args.custom_build, 'r') as z:
        z.extractall('nodes/neo-cli')

    copyfile(args.custom_build, path.join(batch.reportdir, path.basename(args.custom_build)))

elif not args.skip_build:
    if not any(['neo-build:latest' in i.tags for i in dc.client.images.list()]):
        print('[i] Build image not found. Creating image ...')
        dc.create_builder()

    print('[+] Building neo-cli')
    print('     Branch: neo {}, neo-cli {}, neo-vm {}, modules {}'.format(args.branch_neo, args.branch_cli, args.branch_vm, args.branch_mods))
    print('     Pull Request: neo {}, neo-cli {}, neo-vm {}, modules {}'.format(args.pr_neo, args.pr_cli, args.pr_vm, args.pr_mods))
    print('     Code Reference: neo {}, neo-vm {}'.format(args.code_neo, args.code_vm))
    
    buildlog = dc.run_builder(args)
    batch.savelog(buildlog)
    print('[+] Build done {}'.format(batch.buildlog))

if not path.exists('nodes/neo-cli/neo-cli.dll'):
    print('[!] Binaries missing. check logs')
    exit(1)

make_archive(path.join(batch.reportdir, 'build'), 'zip', 'nodes/neo-cli')

print('[+] Creating Node image ...')
dc.create_node_image()

if not any(['neo-txgen:latest' in i.tags for i in dc.client.images.list()]):
    print('[i] Tx generator image not found. Creating image ...')
    dc.create_txgen_image()

print('[+] Launch Tests')
with open(args.tests_file) as f:
    test_batch = json.load(f)

for test in test_batch:
    print('[+] Test: {}'.format(test['name']))
    print('     Desc: {}'.format(test['desc']))
    print('     Phases: {}'.format(len(test['phases'])))
    print('     Duration: {}s'.format(sum(p['duration'] for p in test['phases']) + test['start-delay']))
    print('     Transactions: {}'.format(test['tx']))

    nodehelper.config_txgen(test['tx'], test['start-delay'], test['tx_round'], test['tx_sleep'])
    dc.neo_net_up(args.show_output)
    if(args.interactive_node):
        sleep(5)
        print('     Interactive node -> docker exec -it node-interactive bash')
        dc.start_interactive()

    print('     Phases')
    print('        Network warm up - {}s'.format(test['start-delay']))
    sleep(test['start-delay'])

    first = True
    for phase in test['phases']:
        print('        Phase {} - {}s'.format(test['phases'].index(phase)+1, phase['duration']))
        for p, v in phase['config'].items():
            nodehelper.config_node(dc, p, v, first)

        sleep(phase['duration'])
        first = False

    batch.save_test_result(test)
    dc.neo_net_down()
    if(args.interactive_node):
        dc.stop_interactive()
    print('     Generated blocks:\n        {}'.format(batch.report.tests[test['name']]['blocks']))
    print('     Pass: {}'.format(batch.report.tests[test['name']]['result']))

print('[i] Saved report {}'.format(batch.reportdir + '/report.json'))
batch.save_report()

print('[ ] Done')
