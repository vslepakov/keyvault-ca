terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "=2.98.0"
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

resource "random_id" "prefix" {
  byte_length = 4
  prefix      = "f"
}

resource "random_string" "vm_user_name" {
  length  = 10
  special = false
}

resource "random_string" "vm_password" {
  length  = 10
  number = true
  special = true
}

locals {
  resource_prefix          = var.resource_prefix == "" ? lower(random_id.prefix.hex) : var.resource_prefix
  issuing_ca               = "${local.resource_prefix}-ca"
  edge_device_name         = "${local.resource_prefix}-edge-device"
  certs_path               = "../Certs/${local.resource_prefix}"
  vm_user_name             = var.vm_user_name != "" ? var.vm_user_name : random_string.vm_user_name.result
  vm_password              = var.vm_password != "" ? var.vm_password : random_string.vm_password.result
}

resource "azurerm_resource_group" "rg" {
  name     = "${local.resource_prefix}-keyvault-ca-rg"
  location = var.location
}

module "keyvault" {
  source                        = "./modules/keyvault"
  resource_group_name           = azurerm_resource_group.rg.name
  location                      = var.location
  resource_prefix               = local.resource_prefix
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
  issuing_ca          = local.issuing_ca
  keyvault_id         = module.keyvault.keyvault_id 
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
  edge_device_name    = local.edge_device_name
  issuing_ca          = local.issuing_ca
  keyvault_name       = module.keyvault.keyvault_name
  vnet_name           = module.iot_edge.vnet_name
}

module "iot_edge" {
  source                   = "./modules/iot-edge"
  resource_prefix          = local.resource_prefix
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = var.location
  vm_user_name             = local.vm_user_name
  vm_password              = local.vm_password
  vm_sku                   = var.edge_vm_sku
  dps_scope_id             = module.iot_hub.iot_dps_scope_id
  edge_vm_name             = local.edge_device_name
  app_hostname             = module.appservice.app_hostname
  est_user                 = module.appservice.est_user
  est_password             = module.appservice.est_password
}

resource "null_resource" "run-api-facade" {
  provisioner "local-exec" {
          working_dir = "../KeyvaultCA"
          command = "dotnet run --Csr:IsRootCA true --Csr:Subject ${"C=US, ST=WA, L=Redmond, O=Contoso, OU=Contoso HR, CN=Contoso Inc"} --Keyvault:IssuingCA ${local.issuing_ca} --Keyvault:KeyVaultUrl ${module.keyvault.keyvault_url}"
    }
  provisioner "local-exec" {
          working_dir = "../KeyVaultCA"
          command = "openssl genrsa -out ${local.certs_path}.key 2048"
  }

  provisioner "local-exec" {
          working_dir = "../KeyVaultCA"
          command = "openssl req -new -key ${local.certs_path}.key -subj \"/C=US/ST=WA/L=Redmond/O=Contoso/CN=Contoso Inc\" -out ${local.certs_path}.csr"
          interpreter = ["PowerShell", "-Command"]
  }

  provisioner "local-exec" {
          working_dir = "../KeyVaultCA"
          command = "openssl req -in ${local.certs_path}.csr -out ${local.certs_path}.csr.der -outform DER"
  }

  provisioner "local-exec" {
          working_dir = "../KeyVaultCA"
          command = "dotnet run --Csr:IsRootCA false --Csr:PathToCsr ${local.certs_path}.csr.der --Csr:OutputFileName ${local.certs_path}-cert --Keyvault:IssuingCA ${local.issuing_ca} --Keyvault:KeyVaultUrl ${module.keyvault.keyvault_url}"
  }
}