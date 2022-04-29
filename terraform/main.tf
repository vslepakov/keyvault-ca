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

resource "random_id" "resource_uid" {
  byte_length = 4
  prefix      = "a"
}

locals {
  resource_uid     = var.resource_uid == "" ? lower(random_id.resource_uid.hex) : var.resource_uid
  issuing_ca       = "ContosoRootCA"
  edge_device_name = "${local.resource_uid}-edge-device"
}

resource "azurerm_resource_group" "rg" {
  name     = "rg-${local.resource_uid}-keyvault-ca"
  location = var.location
}

module "acr" {
  source                             = "./modules/acr"
  resource_group_name                = azurerm_resource_group.rg.name
  location                           = var.location
  resource_uid                       = local.resource_uid
  app_princ_id                       = module.appservice.app_princ_id
  dps_rootca_enroll_null_resource_id = module.iot_hub_dps.dps_rootca_enroll_null_resource_id
}

module "appservice" {
  source              = "./modules/appservice"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  resource_uid        = local.resource_uid
  issuing_ca          = local.issuing_ca
  keyvault_url        = module.keyvault.keyvault_url
  keyvault_name       = module.keyvault.keyvault_name
  acr_login_server    = module.acr.acr_login_server
  auth_mode           = var.auth_mode
}

module "iot_hub_dps" {
  source                          = "./modules/iot-hub"
  resource_group_name             = azurerm_resource_group.rg.name
  location                        = var.location
  resource_uid                    = local.resource_uid
  edge_device_name                = local.edge_device_name
  issuing_ca                      = local.issuing_ca
  keyvault_name                   = module.keyvault.keyvault_name
  run_api_facade_null_resource_id = module.keyvault.run_api_facade_null_resource_id
}

module "iot_edge" {
  source                          = "./modules/iot-edge"
  resource_uid                    = local.resource_uid
  resource_group_name             = azurerm_resource_group.rg.name
  location                        = var.location
  vm_sku                          = var.edge_vm_sku
  dps_scope_id                    = module.iot_hub_dps.iot_dps_scope_id
  edge_device_name                = local.edge_device_name
  app_hostname                    = module.appservice.app_hostname
  est_username                    = module.appservice.est_username
  est_password                    = module.appservice.est_password
  iot_dps_name                    = module.iot_hub_dps.iot_dps_name
  acr_admin_username              = module.acr.acr_admin_username
  acr_admin_password              = module.acr.acr_admin_password
  acr_name                        = module.acr.acr_name
  auth_mode                       = var.auth_mode
  run_api_facade_null_resource_id = module.keyvault.run_api_facade_null_resource_id
}

module "keyvault" {
  source              = "./modules/keyvault"
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  resource_uid        = local.resource_uid
  app_princ_id        = module.appservice.app_princ_id
  issuing_ca          = local.issuing_ca
}

## If preferred to deploy the infrastructure without private endpoints then the below sections can be removed
module "private-endpoint-acr" {
  source                        = "./modules/private-endpoints/acr"
  resource_uid                  = local.resource_uid
  resource_group_name           = azurerm_resource_group.rg.name
  location                      = var.location
  vnet_name                     = module.iot_edge.vnet_name
  vnet_id                       = module.iot_edge.vnet_id
  acr_id                        = module.acr.acr_id
  push_docker_null_resource_id  = module.acr.push_docker_null_resource_id
  push_iotedge_null_resource_id = module.acr.push_iotedge_null_resource_id
}

module "private-endpoint-appservice" {
  source              = "./modules/private-endpoints/appservice"
  resource_uid        = local.resource_uid
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  vnet_name           = module.iot_edge.vnet_name
  vnet_id             = module.iot_edge.vnet_id
  app_id              = module.appservice.app_id
}

module "private-endpoint-bastion" {
  source              = "./modules/private-endpoints/bastion"
  resource_uid        = local.resource_uid
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  vnet_name           = module.iot_edge.vnet_name
  vnet_id             = module.iot_edge.vnet_id
}

module "private-endpoint-iot-hub-dps" {
  source                             = "./modules/private-endpoints/iot-hub-dps"
  resource_uid                       = local.resource_uid
  resource_group_name                = azurerm_resource_group.rg.name
  location                           = var.location
  vnet_name                          = module.iot_edge.vnet_name
  vnet_id                            = module.iot_edge.vnet_id
  iot_hub_id                         = module.iot_hub_dps.iot_hub_id
  iot_dps_id                         = module.iot_hub_dps.iot_dps_id
  dps_rootca_enroll_null_resource_id = module.iot_hub_dps.dps_rootca_enroll_null_resource_id
  dps_shared_access_policy_id        = module.iot_hub_dps.dps_shared_access_policy_id
}

module "private-endpoint-keyvault" {
  source              = "./modules/private-endpoints/keyvault"
  resource_uid        = local.resource_uid
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  vnet_name           = module.iot_edge.vnet_name
  vnet_id             = module.iot_edge.vnet_id
  keyvault_id         = module.keyvault.keyvault_id
}

resource "null_resource" "disable_public_network" {
  provisioner "local-exec" {
    command = "az acr update --name ${module.acr.acr_name} --public-network-enabled false"
  }

  provisioner "local-exec" {
    command = "az iot dps update  --name ${module.iot_hub_dps.iot_dps_name} --resource-group ${azurerm_resource_group.rg.name} --set properties.publicNetworkAccess=Disabled"
  }

  provisioner "local-exec" {
    command = "az keyvault update --name ${module.keyvault.keyvault_name} --public-network-access Disabled"
  }

  depends_on = [module.acr, module.iot_hub_dps, module.keyvault, module.private-endpoint-acr, module.private-endpoint-iot-hub-dps, module.private-endpoint-keyvault]

  triggers = {
    timestamp = timestamp()
  }
}