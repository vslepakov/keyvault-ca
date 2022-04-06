data "azurerm_client_config" "current" {}

resource "azurerm_application_insights" "appinsights" {
  name                = "${var.resource_prefix}-appinsights"
  location            = var.location
  resource_group_name = var.resource_group_name
  application_type    = "web"
}


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

  identity {
    type = "SystemAssigned"
  }

  app_settings = {
    WEBSITES_ENABLE_APP_SERVICE_STORAGE = false
    "Keyvault__KeyVaultUrl"= var.keyvault_url
    "EstAuthentication__Auth"=var.authmode
    "EstAuthentication__EstUsername"=var.est_user
    "EstAuthentication__EstPassword"=var.est_password
    "KeyVault__IssuingCA"= var.issuing_ca
    "KeyVault__CertValidityInDays"= var.cert_validity_in_days
    "DOCKER_REGISTRY_SERVER_URL"= "https://${var.acr_login_server}"
    "DOCKER_REGISTRY_SERVER_USERNAME"= var.acr_admin_username
    "DOCKER_REGISTRY_SERVER_PASSWORD"= var.acr_admin_password
    "APPINSIGHTS_INSTRUMENTATIONKEY"= azurerm_application_insights.appinsights.instrumentation_key
    "ApplicationInsights__ConnectionString"= azurerm_application_insights.appinsights.connection_string
     "ApplicationInsightsAgent_EXTENSION_VERSION" = "~2"
  }
}

resource "azurerm_key_vault_access_policy" "app_accesspolicy" {
  key_vault_id = var.keyvault_id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_app_service.appservice.identity.0.principal_id

  key_permissions = [
    "Sign"
  ]

  certificate_permissions = [
    "Get", "List", "Update", "Create"
  ]
}

resource "azurerm_key_vault_access_policy" "user_accesspolicy" {
  key_vault_id = var.keyvault_id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = data.azurerm_client_config.current.object_id

  key_permissions = [
    "Sign"
  ]

  certificate_permissions = [
    "Get", "List", "Update", "Create", "Import", "Delete", "Recover", "Backup", "Restore", "ManageIssuers", "GetIssuers", "ListIssuers", "SetIssuers", "DeleteIssuers"
  ]
}