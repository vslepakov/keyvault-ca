resource "azurerm_container_registry" "acr" {
  name                = "cr${var.resource_prefix}"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "Basic"
  admin_enabled       = true
}

resource "null_resource" "push-docker" {
  provisioner "local-exec" {
    command = "az acr build -r ${azurerm_container_registry.acr.name} -t estserver:latest ../ -f ../KeyVaultCA.Web/Dockerfile"
  }
  depends_on = [var.null_resource_dps_id]
}