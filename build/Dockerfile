FROM mcr.microsoft.com/dotnet/core/sdk:3.0

LABEL maintainer="https://github.com/Red4Sec"


ENV DEBIAN_FRONTEND noninteractive
ENV DOTNET_CLI_TELEMETRY_OPTOUT 1
ENV PATH="${PATH}:/root/.dotnet/tools"

RUN apt-get update &&\
    apt-get install -y zip git openjdk-11-jre graphviz doxygen

RUN rm -rf /tmp/* &&\
    rm -rf /var/lib/{apt,dpkg,cache,log}/

COPY build.sh doc.config analysis?xml /
RUN chmod +x /build.sh

ENTRYPOINT ["/build.sh"]