FROM microsoft/dotnet:latest

LABEL maintainer="https://github.com/Red4Sec"


ENV DEBIAN_FRONTEND noninteractive
ENV DOTNET_CLI_TELEMETRY_OPTOUT 1
ENV PATH="${PATH}:/root/.dotnet/tools"

RUN apt-get update &&\
    apt-get install -y zip git openjdk-8-jre

RUN rm -rf /tmp/* &&\
    rm -rf /var/lib/{apt,dpkg,cache,log}/

COPY build.sh analysis?xml /
RUN chmod +x /build.sh

ENTRYPOINT ["/build.sh"]