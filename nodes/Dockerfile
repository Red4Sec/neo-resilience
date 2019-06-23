FROM microsoft/dotnet:2.1.4-runtime-bionic

LABEL maintainer="https://github.com/Red4Sec"


ENV DEBIAN_FRONTEND noninteractive
ENV DOTNET_CLI_TELEMETRY_OPTOUT 1

RUN apt-get update && apt-get install -y \
    libleveldb-dev \
    libunwind8 \
    iproute2

RUN rm -rf /tmp/* &&\
    rm -rf /var/lib/{apt,dpkg,cache,log}/

ADD ./neo-cli /opt/neo-cli
ADD ./chain /opt/neo-cli/Chain_00AEBED3
COPY ./plugins/node/ /opt/neo-cli/Plugins/

WORKDIR /opt/neo-cli
CMD ["dotnet", "neo-cli.dll", "--rpc"]