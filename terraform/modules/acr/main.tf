resource "azurerm_container_registry" "acr" {
  name                = "${var.resource_prefix}acr"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "Basic"
  admin_enabled       = true
}

resource "null_resource" "push-docker" {
  provisioner "local-exec" {
    command = "az acr build -r ${azurerm_container_registry.acr.name} --image sample/estserver:v2 https://github.com/vslepakov/keyvault-ca.git -f ./././KeyVaultCA.Web/Dockerfile"
  }
}