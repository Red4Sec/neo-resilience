#!/usr/bin/env python3
# -*- coding: utf-8 -*-
#
# Neo Resilience
# Version: 0.2
# https://github.com/red4sec/neo-resilience


import argparse
import json
import zipfile
from time import sleep
from os import path
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

show_banner()

parser = argparse.ArgumentParser(description='Neo Resilience Test')
parser.add_argument('-t', '--tests-file', type=str, default='tests.json', help='JSON tests file')
parser.add_argument('-c', '--custom-build', type=str, help='ZIP neo-cli')
parser.add_argument('--pr-neo', type=int, default=0, help='Select a specific neo pull request')
parser.add_argument('--pr-cli', type=int, default=0, help='Select a specific neo-cli pull request')
parser.add_argument('--pr-vm', type=int, default=0, help='Select a specific neo-vm pull request')
parser.add_argument('--pr-plg', type=int, default=0, help='Select a specific neo plugins pull request')
parser.add_argument('--code-neo', action='store_true', help='Build using github neo code as reference')
parser.add_argument('--code-vm', action='store_true', help='Build using github neo-vm code as reference')
parser.add_argument('--show-output', action='store_true', help='Show output from nodes')
args = parser.parse_args()


dc = dockercontrol.DockerControl()
batch = batch.Batch(dc, args)

print('[i] Batch ID: {}'.format(batch.id))

if(args.custom_build):
    print('[+] Using custom neo-cli build')
    with zipfile.ZipFile(args.custom_build, 'r') as z:
        z.extractall('node/neo-cli')

else:
    if not any(['neo-build:latest' in i.tags for i in dc.client.images.list()]):
        print('[i] Build image not found. Creating image ...')
        dc.create_builder()

    print('[+] Building neo-cli')
    print('     Pull Request: neo {}, neo-cli {}, neo-vm {}, plugins {}'.format(args.pr_neo, args.pr_cli, args.pr_vm, args.pr_plg))
    print('     Code Reference: neo {}, neo-vm {}'.format(args.code_neo, args.code_vm))
    buildlog = dc.run_builder(args.pr_neo, args.pr_cli, args.pr_vm, args.pr_plg, args.code_neo, args.code_vm)
    batch.savelog(buildlog)

    if not path.exists('node/neo-cli/neo-cli.dll'):
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
    print('     Duration: {}s'.format(sum(p['duration'] for p in test['phases'])))
    print('     Transactions: {}'.format(test['tx']))
    print('[+] Starting net ...')

    nodehelper.config_txgen(test['tx'])
    dc.neo_net_up(args.show_output)

    print('[+] Network warm up {}s'.format(test['start-delay']))
    sleep(test['start-delay'])

    first = True
    for phase in test['phases']:
        print('     Phase {} - {}s'.format(test['phases'].index(phase)+1, phase['duration']))
        for p, v in phase['config'].items():
            nodehelper.config_node(dc, p, v, first)

        sleep(phase['duration'])
        first = False

    batch.save_test_result(test)
    dc.neo_net_down()
    print('[+] Generated blocks\n  {}'.format(batch.report.tests[test['name']]['blocks']))

print('[i] Save report {}'.format(batch.reportdir + '/report.json'))
batch.save_report()

print('[ ] Done')
