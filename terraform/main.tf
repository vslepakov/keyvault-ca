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
  prefix      = "f"
}

locals {
  resource_prefix          = var.resource_prefix == "" ? lower(random_id.prefix.hex) : var.resource_prefix
  edge_device_name         = "${local.resource_prefix}-edge-device"
}

resource "azurerm_resource_group" "rg" {
  name     = "${local.resource_prefix}-keyvault-ca-rg"
  location = var.location
}

module "keyvault" {
  source              = "./modules/keyvault"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  resource_prefix     = local.resource_prefix
}

module "acr" {
  source              = "./modules/acr"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  resource_prefix     = local.resource_prefix
}

module "appservice" {
  source              = "./modules/appservice"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  resource_prefix     = local.resource_prefix
  app_id              = module.keyvault.app_id
  app_secret          = module.keyvault.app_secret
  keyvault_url        = module.keyvault.keyvault_url
  acr_name            = module.acr.acr_name
  acr_login_server    = module.acr.acr_login_server
  acr_admin_username  = module.acr.acr_admin_username
  acr_admin_password  = module.acr.acr_admin_password
}

module "iot_hub" {
  source              = "./modules/iot-hub"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  resource_prefix     = local.resource_prefix
  dps_root_ca_name    = var.dps_root_ca_name
  edge_device_name    = local.edge_device_name
}

module "iot_edge" {
  source                   = "./modules/iot-edge"
  resource_prefix          = local.resource_prefix
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = var.location
  vm_user_name             = var.edge_vm_user_name
  vm_sku                   = var.edge_vm_sku
  dps_scope_id             = module.iot_hub.iot_dps_scope_id
  #root_ca_certificate_path = local.root_ca_certificate_path
  edge_vm_name             = local.edge_device_name
}

