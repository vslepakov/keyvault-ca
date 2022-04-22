output "app_hostname" {
  value = azurerm_app_service.appservice.default_site_hostname
}

output "est_username" {
  value = var.est_username
}

output "est_password" {
  value = local.est_password
}