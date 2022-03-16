terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "=2.90.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 2.15.0"
    }
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }
}

data "azurerm_client_config" "current" {}

provider "azuread" {
  tenant_id = data.azurerm_client_config.current.tenant_id
}

resource "random_id" "prefix" {
  byte_length = 4
  prefix      = "m"
}

locals {
  resource_prefix          = var.resource_prefix == "" ? lower(random_id.prefix.hex) : var.resource_prefix
  edge_device_name         = "${local.resource_prefix}-edge-device"
}

resource "azurerm_resource_group" "rg" {
  name     = "${local.resource_prefix}-keyvault-ca-rg"
  location = var.location
}

resource "azuread_application" "azure-ad-app" {
  display_name = "${local.resource_prefix}-azure-ad-app"
  owners       = [data.azurerm_client_config.current.object_id]
}

resource "azuread_service_principal" "sp-app" {
  application_id = azuread_application.azure-ad-app.application_id
}

# resource "azuread_service_principal_password" "sp-app-password" {
#   display_name = "terraform-created-secret"
#   service_principal_id = azuread_service_principal.sp-app.object_id
# }

resource "azurerm_key_vault" "keyvault-ca" {
  name                        = "${local.resource_prefix}-keyvault-ca"
  location                    = var.location
  resource_group_name         = azurerm_resource_group.rg.name
  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  purge_protection_enabled    = true
  soft_delete_retention_days  = 7 

  sku_name = "standard"

  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = data.azurerm_client_config.current.object_id
    application_id = azuread_application.azure-ad-app.application_id
    key_permissions = [
      "Sign"
    ]

    certificate_permissions = [
      "Get", "List", "Update", "Create"
    ]
  }
}

resource "azurerm_app_service_plan" "appserviceplan" {
  name                = "${local.resource_prefix}-appserviceplan"
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name

  sku {
    tier = "Standard"
    size = "S1"
  }
}

resource "azurerm_app_service" "appservice" {
  name                = "${local.resource_prefix}-appservice"
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  app_service_plan_id = azurerm_app_service_plan.appserviceplan.id

  site_config {
    dotnet_framework_version = "v4.0"
  }

  # source_control {
  #   repo_url           = "https://github.com/machteldbogels/keyvault-ca"
  #   branch             = "master"
  #   manual_integration = true
  # }

  app_settings = {
    "AppId" = azuread_application.azure-ad-app.application_id,
    "Secret"= #appsecret,
    "KeyVaultUrl"= azurerm_key_vault.keyvault-ca.vault_uri,
    "AuthMode"=var.AuthMode,
    "EstUser"=var.EstUser,
    "EstPassword"=var.EstPassword,
    "IssuingCA"=#rootca,
    "CertValidityInDays"="365"
  }

}

resource "azurerm_container_registry" "acr" {
  name                = "${local.resource_prefix}acr"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  sku                 = "Basic"

    provisioner "local-exec" {
        command = "az acr build --image sample/estserver:v1 --registry ${local.resource_prefix} https://github.com/machteldbogels/keyvault-ca.git --file ./KeyVaultCA.Web/Dockerfile"

  }
}