output "keyvault_url" {
  value = azurerm_key_vault.keyvault-ca.vault_uri
}

output "keyvault_name" {
  value = azurerm_key_vault.keyvault-ca.name
}

output "keyvault_id" {
  value = azurerm_key_vault.keyvault-ca.id
}

output "run_api_facade_null_resource_id" {
  value = null_resource.run_api_facade.id
}