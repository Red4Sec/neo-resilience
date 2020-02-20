import json
from lib import dockercontrol
from lib import nodehelper
from time import time
from datetime import datetime
import os


class Batch(object):

    outdir = 'output'
    reportdir = ''
    buildlog = ''

    def __init__(self, dc, args):
        self.id = args.id
        self.dc = dc
        self.report = Report(self.id, args)

        if not os.path.isdir(self.outdir):
            os.mkdir(self.outdir)
        self.reportdir = os.path.join(self.outdir, str(self.id))
        os.mkdir(self.reportdir)


    def savelog(self, log):
        self.buildlog = os.path.join(self.reportdir, 'build.log')
        with open(self.buildlog, 'w') as file:
            file.write(log.decode())


    def save_test_result(self, test):
        nodes = ['node-1', 'node-2', 'node-3', 'node-4', 'node-5', 'node-6', 'node-7']
        initial_block_count = 27
        test_name = test['name']
        testdir = os.path.join(self.reportdir, test_name)

        if not os.path.isdir(testdir):
            os.mkdir(testdir)

        self.report.tests[test_name] = {
            'name': test_name,
            'desc': test['desc'],
            'phases': len(test['phases']),
            'duration': sum(p['duration'] for p in test['phases']),
            'tx': test['tx'],
            'blocks': {},
            'stats': {}
        }

        for node in nodes:
            node_stats = os.path.join(testdir, node + '_stats.json')
            node_logs = os.path.join(testdir, node + '_logs.tar')

            try:
                self.dc.copyfile(node, '/opt/neo-cli/stats.json', node_stats)
                with open(node_stats) as f:
                    self.report.tests[test_name]['stats'][node] = json.load(f)
            except FileNotFoundError:
                print('     Stats not found for {}'.format(node))
            except BaseException as e:
                print('     Error: {}'.format(e))

            try:
                self.dc.copy2tar(node, '/opt/neo-cli/SystemLogs_00AEBED3/ConsensusService', node_logs)
            except FileNotFoundError:
                print('     Logs not found for {}'.format(node))
            except BaseException as e:
                print('     Error: {}'.format(e))

            blocks = 0
            if node in self.report.tests[test_name]['stats']:
                if 'Index' in self.report.tests[test_name]['stats'][node][-1]:
                    blocks = self.report.tests[test_name]['stats'][node][-1]['Index']
            if blocks == 0:
                blocks = nodehelper.get_node_height(self.dc, node) - 1
            self.report.tests[test_name]['blocks'][node] = blocks - initial_block_count

        if('expected' in test and test['expected'] > 0):
            min_block = min(self.report.tests[test_name]['blocks'].values())
            if(min_block >= test['expected']):
                self.report.tests[test_name]['result'] = True
            else:
                self.report.tests[test_name]['result'] = False
        else:
            self.report.tests[test_name]['result'] = None


    def get_report(self):
        return json.dumps(self.report.__dict__)


    def save_report(self):
        report_file = os.path.join(self.reportdir, 'report.json')
        with open(report_file, 'w') as f:
            json.dump(self.report.__dict__, f)



class Report(object):

    def __init__(self, id, args):
        self.id = id
        self.date = str(datetime.now())
        self.tests = {}
        self.buildoptions = {
            'branch_neo': args.branch_neo,
            'branch_cli': args.branch_cli,
            'branch_vm': args.branch_vm,
            'branch_plg': args.branch_mods,
            'pr_neo': args.pr_neo,
            'pr_cli': args.pr_cli,
            'pr_vm': args.pr_vm,
            'pr_plg': args.pr_mods,
            'code_neo': args.code_neo,
            'code_vm': args.code_vm,
            'custom_build': args.custom_build
        }
