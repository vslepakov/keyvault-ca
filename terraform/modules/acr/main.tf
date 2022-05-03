resource "azurerm_container_registry" "acr" {
  name                          = "cr${var.resource_uid}"
  resource_group_name           = var.resource_group_name
  location                      = var.location
  sku                           = "Premium" # Needs to be premium in order to disable public network access
  admin_enabled                 = true
  public_network_access_enabled = true
}

resource "azurerm_role_assignment" "acr_app_service" {
  principal_id         = var.app_princ_id
  role_definition_name = "AcrPull"
  scope                = azurerm_container_registry.acr.id
}

resource "null_resource" "push-docker" {
  triggers = {
    always_run = uuid()
  }

  provisioner "local-exec" {
    command = "az acr build -r ${azurerm_container_registry.acr.name} -t estserver:latest ../ -f ../KeyVaultCA.Web/Dockerfile"
  }
  depends_on = [azurerm_container_registry.acr, var.dps_rootca_enroll_null_resource_id]
}

resource "null_resource" "push-iotedge" {
  provisioner "local-exec" {
    command = "az acr import --name ${azurerm_container_registry.acr.name} --source mcr.microsoft.com/azureiotedge-agent:1.2 --image azureiotedge-agent:1.2"
  }

  depends_on = [azurerm_container_registry.acr]
}