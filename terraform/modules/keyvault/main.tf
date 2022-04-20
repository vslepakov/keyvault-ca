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

resource "azurerm_key_vault_access_policy" "user_accesspolicy" {
  key_vault_id = azurerm_key_vault.keyvault-ca.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = data.azurerm_client_config.current.object_id

  key_permissions = [
    "Sign"
  ]

  certificate_permissions = [
    "Get", "List", "Update", "Create", "Import", "Delete", "Recover", "Backup", "Restore", "ManageIssuers", "GetIssuers", "ListIssuers", "SetIssuers", "DeleteIssuers"
  ]
}