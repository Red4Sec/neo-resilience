import subprocess
import docker
import tarfile
import io
import os


class DockerControl(object):

    def __init__(self):
        self.client = docker.from_env()


    def create_builder(self):
        return self.client.images.build(path='./build', rm=True, tag='neo-build')


    def run_builder(self, args):
        volumes = {os.path.join(os.getcwd(),'nodes/neo-cli'): {'bind': '/build/neo-cli', 'mode': 'rw'}}

        # TODO: fix this mess
        build_arguments = ''
        if (args.source_neo): build_arguments += ' -w {}'.format(args.source_neo)
        if (args.source_cli): build_arguments += ' -x {}'.format(args.source_cli)
        if (args.source_vm): build_arguments += ' -y {}'.format(args.source_vm)
        if (args.source_mods): build_arguments += ' -z {}'.format(args.source_mods)
        if (args.branch_neo): build_arguments += ' -n {}'.format(args.branch_neo)
        if (args.branch_cli): build_arguments += ' -c {}'.format(args.branch_cli)
        if (args.branch_vm): build_arguments += ' -v {}'.format(args.branch_vm)
        if (args.branch_mods): build_arguments += ' -p {}'.format(args.branch_mods)
        if (args.pr_neo): build_arguments += ' -o {}'.format(args.pr_neo)
        if (args.pr_cli): build_arguments += ' -i {}'.format(args.pr_cli)
        if (args.pr_vm): build_arguments += ' -m {}'.format(args.pr_vm)
        if (args.pr_mods): build_arguments += ' -g {}'.format(args.pr_mods)
        if (args.code_neo): build_arguments += ' -a'
        if (args.code_vm): build_arguments += ' -b'
        if (args.analysis): build_arguments += ' -q'
        if (args.doc):
            build_arguments += ' -d'
            volumes[os.path.join(os.getcwd(),'output/doc-neo-master-3.x')] = {'bind': '/doc/html', 'mode': 'rw'}

        return self.client.containers.run('neo-build:latest', build_arguments, remove=True, volumes=volumes)


    def create_node_image(self):
        try:
            return self.client.images.build(path='./nodes', rm=True, nocache=True, tag='neo-node')
        except docker.errors.BuildError as e:
            print('[!] Node image build failed:\n{}'.format(e.msg))
            quit()


    def create_txgen_image(self):
        try:
            return self.client.images.build(path='./nodes', dockerfile='Dockerfile.txgen', rm=True, tag='neo-txgen')
        except docker.errors.BuildError as e:
            print('[!] Tx image build failed:\n{}'.format(e.msg))
            quit()


    def start_interactive(self):
        path = os.getcwd()
        volumes = {
            os.path.join(path,'nodes/tmp'): {'bind': '/node-tmp', 'mode': 'rw'},
            os.path.join(path,'nodes/configs/protocol.json'): {'bind': '/opt/neo-cli/protocol.json', 'mode': 'ro'},
            os.path.join(path,'nodes/configs/config.txgen.json'): {'bind': '/opt/neo-cli/config.json', 'mode': 'ro'},
            os.path.join(path,'nodes/wallets/wallet0.json'): {'bind': '/opt/neo-cli/wallet.json', 'mode': 'ro'}
            }
        node = self.client.containers.run('neo-node',['sleep 1d'], entrypoint=['/bin/sh','-c'], remove=True, detach=True, name='node-interactive', auto_remove=True, network='neo-resilience_neo-net', privileged=True, volumes=volumes)
        bridge = [net for net in self.client.networks.list() if net.name == 'bridge'][0]
        bridge.connect(node)
        return True


    def stop_interactive(self):
        try:
            node = self.client.containers.get('node-interactive')
            return node.kill()
        except:
            return False


    def neo_net_down(self):
        return subprocess.Popen(['docker-compose', 'down'], stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT).wait()


    def neo_net_up(self, output):
        self.neo_net_down()
        if output:
            p = subprocess.Popen(['docker-compose', 'up'], stderr=subprocess.STDOUT)
        else:
            p = subprocess.Popen(['docker-compose', 'up', '-d'], stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
        return p.pid != -1


    def node_exec(self, node_name, cmd):
        node = self.client.containers.get(node_name)
        return node.exec_run(cmd) if node.status == 'running' else None


    def copyfile(self, node_name, org, dst):
        node = self.client.containers.get(node_name)
        bits, _ = node.get_archive(org)
        stream = self.__generator_to_stream(bits)
        tar_file = tarfile.open(fileobj=stream, mode='r|*')

        for tarinfo in tar_file:
            if tarinfo.name == os.path.basename(org):
                tfile = tar_file.extractfile(tarinfo)
                with open(dst, 'wb') as f:
                    f.writelines(tfile.readlines())
                tfile.close()

        tar_file.close()
        stream.close()


    def copy2tar(self, node_name, org, dst):
        node = self.client.containers.get(node_name)
        bits, _ = node.get_archive(org)

        with open(dst, 'wb') as f:
            for chunk in bits:
                f.write(chunk)


    def __generator_to_stream(self, generator, buffer_size=io.DEFAULT_BUFFER_SIZE):
        class GeneratorStream(io.RawIOBase):
            def __init__(self):
                self.leftover = None

            def readable(self):
                return True

            def readinto(self, b):
                try:
                    l = len(b)
                    chunk = self.leftover or next(generator)
                    output, self.leftover = chunk[:l], chunk[l:]
                    b[:len(output)] = output
                    return len(output)
                except StopIteration:
                    return 0
        return io.BufferedReader(GeneratorStream())
