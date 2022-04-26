terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "=3.2.0"
    }
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = true
    }
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }
}

resource "random_id" "prefix" {
  byte_length = 4
  prefix      = "a"
}

locals {
  resource_prefix  = var.resource_prefix == "" ? lower(random_id.prefix.hex) : var.resource_prefix
  issuing_ca       = "ContosoRootCA"
  edge_device_name = "${local.resource_prefix}-edge-device"
}

resource "azurerm_resource_group" "rg" {
  name     = "rg-${local.resource_prefix}-keyvault-ca"
  location = var.location
}

module "keyvault" {
  source              = "./modules/keyvault"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  resource_prefix     = local.resource_prefix
  issuing_ca          = local.issuing_ca
}

module "acr" {
  source                             = "./modules/acr"
  resource_group_name                = azurerm_resource_group.rg.name
  location                           = var.location
  resource_prefix                    = local.resource_prefix
  dps_rootca_enroll_null_resource_id = module.iot_hub.dps_rootca_enroll_null_resource_id
}

module "appservice" {
  source              = "./modules/appservice"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  resource_prefix     = local.resource_prefix
  issuing_ca          = local.issuing_ca
  keyvault_id         = module.keyvault.keyvault_id
  keyvault_url        = module.keyvault.keyvault_url
  acr_id              = module.acr.acr_id
  acr_login_server    = module.acr.acr_login_server
  auth_mode           = var.auth_mode
}

module "iot_hub" {
  source                          = "./modules/iot-hub"
  resource_group_name             = azurerm_resource_group.rg.name
  location                        = var.location
  resource_prefix                 = local.resource_prefix
  edge_device_name                = local.edge_device_name
  issuing_ca                      = local.issuing_ca
  keyvault_name                   = module.keyvault.keyvault_name
  vnet_name                       = module.iot_edge.vnet_name
  run_api_facade_null_resource_id = module.keyvault.run_api_facade_null_resource_id
}

module "iot_edge" {
  source                          = "./modules/iot-edge"
  resource_prefix                 = local.resource_prefix
  resource_group_name             = azurerm_resource_group.rg.name
  location                        = var.location
  vm_sku                          = var.edge_vm_sku
  dps_scope_id                    = module.iot_hub.iot_dps_scope_id
  edge_device_name                = local.edge_device_name
  app_hostname                    = module.appservice.app_hostname
  est_username                    = module.appservice.est_username
  est_password                    = module.appservice.est_password
  auth_mode                       = var.auth_mode
  run_api_facade_null_resource_id = module.keyvault.run_api_facade_null_resource_id
}
