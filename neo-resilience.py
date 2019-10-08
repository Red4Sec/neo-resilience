#!/usr/bin/env python3
# -*- coding: utf-8 -*-
#
# Neo Resilience
# Version: 0.4.0
# https://github.com/red4sec/neo-resilience


import argparse
import json
import zipfile
import signal
import sys
from time import time, sleep
from os import path
from shutil import copyfile
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
parser.add_argument('--source-plg', type=str, help='Use a specific plugins repo')

parser.add_argument('--branch-neo', type=str, default='master', help='Use a specific neo branch')
parser.add_argument('--branch-cli', type=str, default='master', help='Use a specific neo-cli branch')
parser.add_argument('--branch-vm', type=str, default='master', help='Use a specific neo-vm branch')
parser.add_argument('--branch-plg', type=str, default='master', help='Use a specific plugins branch')

parser.add_argument('--pr-neo', type=int, default=0, help='Use a specific neo pull request')
parser.add_argument('--pr-cli', type=int, default=0, help='Use a specific neo-cli pull request')
parser.add_argument('--pr-vm', type=int, default=0, help='Use a specific neo-vm pull request')
parser.add_argument('--pr-plg', type=int, default=0, help='Use a specific neo plugins pull request')

parser.add_argument('--code-neo', action='store_true', help='Build using github neo code as reference')
parser.add_argument('--code-vm', action='store_true', help='Build using github neo-vm code as reference')

parser.add_argument('--doc', action='store_true', help='Generate code documentation')
parser.add_argument('--analysis', action='store_true', help='Run code analysis')

parser.add_argument('--show-output', action='store_true', help='Show output from nodes')

args = parser.parse_args()


dc = dockercontrol.DockerControl()
batch = batch.Batch(dc, args)

print('[i] Batch ID: {}'.format(batch.id))

if(args.custom_build):
    print('[+] Using custom neo-cli build')
    with zipfile.ZipFile(args.custom_build, 'r') as z:
        z.extractall('nodes/neo-cli')

    copyfile(args.custom_build, path.join(batch.reportdir, path.basename(args.custom_build)))

else:
    if not any(['neo-build:latest' in i.tags for i in dc.client.images.list()]):
        print('[i] Build image not found. Creating image ...')
        dc.create_builder()

    print('[+] Building neo-cli')
    print('     Branch: neo {}, neo-cli {}, neo-vm {}, plugins {}'.format(args.branch_neo, args.branch_cli, args.branch_vm, args.branch_plg))
    print('     Pull Request: neo {}, neo-cli {}, neo-vm {}, plugins {}'.format(args.pr_neo, args.pr_cli, args.pr_vm, args.pr_plg))
    print('     Code Reference: neo {}, neo-vm {}'.format(args.code_neo, args.code_vm))
    buildlog = dc.run_builder(args)
    batch.savelog(buildlog)

    if not path.exists('nodes/neo-cli/neo-cli.dll'):
        print('[!] Build failed. check {}'.format(batch.buildlog))
        exit(1)

    print('[+] Build done {}'.format(batch.buildlog))


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
    print('     Phases:')

    nodehelper.config_txgen(test['tx'], test['start-delay'], test['tx_round'], test['tx_sleep'])
    dc.neo_net_up(args.show_output)

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
    print('     Generated blocks:\n        {}'.format(batch.report.tests[test['name']]['blocks']))

print('[i] Save report {}'.format(batch.reportdir + '/report.json'))
batch.save_report()

print('[ ] Done')
