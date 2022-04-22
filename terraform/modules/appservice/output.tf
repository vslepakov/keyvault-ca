output "app_hostname" {
  value = azurerm_linux_web_app.appservice.default_hostname
}

output "est_username" {
  value = var.est_username
}

output "est_password" {
  value = local.est_password
}