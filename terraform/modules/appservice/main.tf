resource "azurerm_app_service_plan" "appserviceplan" {
  name                = "${var.resource_prefix}-appserviceplan"
  location            = var.location
  resource_group_name = var.resource_group_name
  kind                = "Linux"
  reserved            = true

  sku {
    tier = "Standard"
    size = "S1"
  }
}

resource "azurerm_app_service" "appservice" {
  name                = "${var.resource_prefix}-appservice"
  location            = var.location
  resource_group_name = var.resource_group_name
  app_service_plan_id = azurerm_app_service_plan.appserviceplan.id

  site_config {
    dotnet_framework_version = "v6.0"
    linux_fx_version         = "DOCKER|${var.acr_name}.azurecr.io/sample/estserver:v2"
  }

  app_settings = {
    WEBSITES_ENABLE_APP_SERVICE_STORAGE = false
    "AppId" = var.app_id
    "Secret"= var.app_secret
    "KeyVaultUrl"= var.keyvault_url
    "AuthMode"=var.authmode,
    "EstUser"=var.est_user,
    "EstPassword"=var.est_password,
    "IssuingCA"= var.issuing_ca,
    "CertValidityInDays"= var.cert_validity_in_days
    "DOCKER_REGISTRY_SERVER_URL"= "https://${var.acr_login_server}"
    "DOCKER_REGISTRY_SERVER_USERNAME"= var.acr_admin_username
    "DOCKER_REGISTRY_SERVER_PASSWORD"= var.acr_admin_password #Add to keyvault
  }
}