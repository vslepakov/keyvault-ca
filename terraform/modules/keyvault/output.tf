output "app_id" {
  value = azuread_application.azure-ad-app.application_id
}

output "app_secret" {
  value = #<Insert manaully created app-password or use variable azuread_service_principal_password.sp-app-password.value instead>
}

output "keyvault_url" {
  value = azurerm_key_vault.keyvault-ca.vault_uri
}