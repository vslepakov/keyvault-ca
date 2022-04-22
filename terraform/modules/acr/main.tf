resource "azurerm_container_registry" "acr" {
  name                = "${var.resource_prefix}acr"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "Basic"
  admin_enabled       = true
}

resource "null_resource" "push-docker" {
  provisioner "local-exec" {
    command = "az acr build --image sample/estserver:v2 --registry ${azurerm_container_registry.acr.name} https://github.com/vslepakov/keyvault-ca.git --file ./././KeyVaultCA.Web/Dockerfile"
  }
}