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
        # TODO: fix this mess
        build_arguments = ''
        if (args.source_neo): build_arguments += ' -w {}'.format(args.source_neo)
        if (args.source_cli): build_arguments += ' -x {}'.format(args.source_cli)
        if (args.source_vm): build_arguments += ' -y {}'.format(args.source_vm)
        if (args.source_plg): build_arguments += ' -z {}'.format(args.source_plg)
        if (args.branch_neo): build_arguments += ' -n {}'.format(args.branch_neo)
        if (args.branch_cli): build_arguments += ' -c {}'.format(args.branch_cli)
        if (args.branch_vm): build_arguments += ' -v {}'.format(args.branch_vm)
        if (args.branch_plg): build_arguments += ' -p {}'.format(args.branch_plg)
        if (args.pr_neo): build_arguments += ' -o {}'.format(args.pr_neo)
        if (args.pr_cli): build_arguments += ' -i {}'.format(args.pr_cli)
        if (args.pr_vm): build_arguments += ' -m {}'.format(args.pr_vm)
        if (args.pr_plg): build_arguments += ' -g {}'.format(args.pr_plg)
        if (args.code_neo): build_arguments += ' -a'
        if (args.code_vm): build_arguments += ' -b'

        path = {os.path.join(os.getcwd(),'nodes/neo-cli'): {'bind': '/build/neo-cli', 'mode': 'rw'}}
        #args = '{} {} {} {} {} {}'.format(pr_neo, pr_cli, pr_vm, pr_plg, int(code_neo), int(code_vm))
        return self.client.containers.run('neo-build:latest', build_arguments, remove=True, volumes=path)


    def create_node_image(self):
        return self.client.images.build(path='./nodes', rm=True, nocache=True, tag='neo-node')


    def create_txgen_image(self):
        return self.client.images.build(path='./nodes', dockerfile='Dockerfile.txgen', rm=True, tag='neo-txgen')


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
        n = self.client.containers.get(node_name)
        return n.exec_run(cmd)


    def copyfile(self, node_name, org, dst):
        n = self.client.containers.get(node_name)
        bits, _ = n.get_archive(org)
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
        n = self.client.containers.get(node_name)
        bits, _ = n.get_archive(org)

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
