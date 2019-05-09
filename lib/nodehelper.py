import json
from random import randint
from lib import dockercontrol


def config_node(dc, node, values, new=True):
    rule = 'add'
    tc_params = []

    if not new:
        rule = 'change'

    tc_cmd = 'tc qdisc {} dev eth0 root netem '.format(rule)

    if 'delay' in values:
        tc_params.append(__cmd_delay(values['delay']))
    if 'loss' in values:
        tc_params.append(__cmd_loss(values['loss']))
    if 'corrupt' in values:
        tc_params.append(__cmd_corrupt(values['corrupt']))

    dc.node_exec(node, tc_cmd + ' '.join(tc_params))


def config_txgen(n):
    with open('node/txgen.env', 'w') as f:
        f.write('NEO_TX_RUN={}\n'.format(n))


def get_node_height(dc, node):
    block = 0
    id = randint(10, 900)

    query = '\'{{ "jsonrpc": "2.0", "id": {}, "method": "getblockcount", "params": [] }}\''.format(id)
    cmd = 'curl -s -S -X POST http://localhost:10332 -H \'Content-Type: application/json\' -d {}'.format(query)
    r = dc.node_exec(node, cmd)

    if r.exit_code == 0:
        result = json.loads(r.output)
        if 'result' in result:
            block = result['result']

    return block


def __cmd_delay(value):
    if type(value) is not list:
        return 'delay {}ms'.format(value)
    else:
        return 'delay {}ms {}ms distribution normal'.format(value[0], value[1])


def __cmd_loss(value):
    if type(value) is not list:
        return 'loss {}%'.format(value)
    else:
        return 'loss {}% {}%'.format(value[0], value[1])


def __cmd_corrupt(value):
    if type(value) is not list:
        return 'corrupt {}%'.format(value)
    else:
        return 'corrupt {}% {}%'.format(value[0], value[1])
