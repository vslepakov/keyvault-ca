# See here for image contents: https://github.com/microsoft/vscode-dev-containers/blob/main/containers/dotnet/.devcontainer/base.Dockerfile

# [Choice] .NET version: 6.0, 5.0, 3.1, 2.1
ARG DOTNET_VARIANT="6.0"
FROM mcr.microsoft.com/vscode/devcontainers/dotnet:${DOTNET_VARIANT}

RUN apt-get update -y && apt-get install -y software-properties-common \
    # Install the Azure CLI and IoT extension
    && curl -sL https://aka.ms/InstallAzureCLIDeb | bash \
    && az extension add --name azure-iot \
    # Install Terraform
    && curl -fsSL https://apt.releases.hashicorp.com/gpg | apt-key add - \
    && apt-add-repository "deb [arch=amd64] https://apt.releases.hashicorp.com $(lsb_release -cs) main" \
    && apt-get update -y && apt-get install terraform \
    && apt-get clean -y && rm -rf /var/lib/apt/lists/*
