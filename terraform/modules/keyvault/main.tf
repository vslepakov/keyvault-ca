provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }
}

data "azurerm_client_config" "current" {}

resource "azuread_application" "azure-ad-app" {
  display_name = "${var.resource_prefix}-azure-ad-app"
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
  name                        = "${var.resource_prefix}-keyvault-ca"
  location                    = var.location
  resource_group_name         = var.resource_group_name
  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  purge_protection_enabled    = false
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