# provider "azurerm" {
#   features {
#     key_vault {
#       purge_soft_delete_on_destroy = true
#     }
#   }
# }

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "keyvault-ca" {
  name                        = "${var.resource_prefix}-keyvault-ca"
  location                    = var.location
  resource_group_name         = var.resource_group_name
  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  purge_protection_enabled    = false
  soft_delete_retention_days  = 7
  sku_name                    = "standard"
}