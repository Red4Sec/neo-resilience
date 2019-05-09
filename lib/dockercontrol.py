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


    def run_builder(self, pr_neo=0, pr_cli=0, pr_vm=0, pr_plg=0, code_neo=False, code_vm=False):
        path = {os.path.join(os.getcwd(),'node/neo-cli'): {'bind': '/build/neo-cli', 'mode': 'rw'}}
        args = '{} {} {} {} {} {}'.format(pr_neo, pr_cli, pr_vm, pr_plg, int(code_neo), int(code_vm))
        return self.client.containers.run('neo-build:latest', args, remove=True, volumes=path)


    def create_node_image(self):
        return self.client.images.build(path='./node', rm=True, nocache=True, tag='neo-node')


    def create_txgen_image(self):
        return self.client.images.build(path='./node', dockerfile='Dockerfile.txgen', rm=True, tag='neo-txgen')


    def neo_net_down(self):
        return subprocess.Popen(['docker-compose', 'down'], stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT).wait()


    def neo_net_up(self, output):
        self.neo_net_down()
        if output:
            p = subprocess.Popen(['docker-compose', 'up'], stderr=subprocess.STDOUT)
            return p.pid != -1
        return subprocess.call(['docker-compose', 'up', '-d']) != -1


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
