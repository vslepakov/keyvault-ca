# See here for image contents: https://github.com/microsoft/vscode-dev-containers/blob/main/containers/dotnet/.devcontainer/base.Dockerfile

# [Choice] .NET version: 6.0, 5.0, 3.1, 2.1
ARG DOTNET_VARIANT="6.0"
FROM mcr.microsoft.com/vscode/devcontainers/dotnet:${DOTNET_VARIANT}

COPY library-scripts/azcli-debian.sh /tmp/library-scripts/
RUN bash /tmp/library-scripts/azcli-debian.sh \
    && curl -fsSL https://apt.releases.hashicorp.com/gpg | apt-key add - \
    && apt-get install -y software-properties-common \
    && apt-add-repository "deb [arch=amd64] https://apt.releases.hashicorp.com $(lsb_release -cs) main" \
    && apt-get update -y && apt-get install terraform \
    && apt-get clean -y && rm -rf /var/lib/apt/lists/* /tmp/library-scripts
