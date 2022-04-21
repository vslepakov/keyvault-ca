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
  }
}

resource "random_id" "prefix" {
  byte_length = 4
  prefix      = "a"
}

resource "random_string" "vm_user_name" {
  length  = 10
  special = false
}

resource "random_string" "vm_password" {
  length  = 10
  number  = true
  special = true
}

locals {
  resource_prefix  = var.resource_prefix == "" ? lower(random_id.prefix.hex) : var.resource_prefix
  issuing_ca       = "${local.resource_prefix}-ca"
  edge_device_name = "${local.resource_prefix}-edge-device"
  certs_path       = "../Certs/${local.resource_prefix}"
  vm_user_name     = var.vm_user_name != "" ? var.vm_user_name : random_string.vm_user_name.result
  vm_password      = var.vm_password != "" ? var.vm_password : random_string.vm_password.result
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
  acr_id              = module.acr.acr_id
  acr_login_server    = module.acr.acr_login_server
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
  source              = "./modules/iot-edge"
  resource_prefix     = local.resource_prefix
  resource_group_name = azurerm_resource_group.rg.name
  location            = var.location
  vm_user_name        = local.vm_user_name
  vm_password         = local.vm_password
  vm_sku              = var.edge_vm_sku
  dps_scope_id        = module.iot_hub.iot_dps_scope_id
  edge_device_name    = local.edge_device_name
  app_hostname        = module.appservice.app_hostname
  est_user            = module.appservice.est_user
  est_password        = module.appservice.est_password
}

resource "null_resource" "run-api-facade" {
  triggers = {
    key     = "${local.certs_path}.key"
    csr     = "${local.certs_path}.csr"
    csr_der = "${local.certs_path}.csr.der"
    cert    = "${local.certs_path}-cert"
  }

  provisioner "local-exec" {
    interpreter = ["/bin/bash", "-c"]
    working_dir = "../KeyvaultCA"
    when        = create
    command     = <<EOF
      set -Eeuo pipefail

      dotnet run --Csr:IsRootCA true --Csr:Subject "C=US, ST=WA, L=Redmond, O=Contoso, OU=Contoso HR, CN=Contoso Inc" --Keyvault:IssuingCA ${local.issuing_ca} --Keyvault:KeyVaultUrl ${module.keyvault.keyvault_url}
      openssl genrsa -out ${self.triggers.key} 2048
      openssl req -new -key ${self.triggers.key} -subj "/C=US/ST=WA/L=Redmond/O=Contoso/CN=Contoso Inc" -out ${self.triggers.csr}
      openssl req -in ${self.triggers.csr} -out ${self.triggers.csr_der} -outform DER
      dotnet run --Csr:IsRootCA false --Csr:PathToCsr ${self.triggers.csr_der} --Csr:OutputFileName ${self.triggers.cert} --Keyvault:IssuingCA ${local.issuing_ca} --Keyvault:KeyVaultUrl ${module.keyvault.keyvault_url}
    EOF
  }

  provisioner "local-exec" {
    interpreter = ["/bin/bash", "-c"]
    working_dir = "../KeyvaultCA"
    when        = destroy
    command     = "rm -f ${self.triggers.key} ${self.triggers.csr} ${self.triggers.csr_der} ${self.triggers.cert}"
  }
}