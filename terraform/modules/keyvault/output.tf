output "app_id" {
  value = azuread_application.azure-ad-app.application_id
}

# output "app_secret" {
#   value = azuread_service_principal_password.sp-app-password
# }

# output "keyvault_url" {
#   value = azurerm_key_vault.keyvault-ca.vault_uri
# }