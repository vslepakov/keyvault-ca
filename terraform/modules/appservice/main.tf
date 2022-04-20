data "azurerm_client_config" "current" {}

resource "azurerm_application_insights" "appinsights" {
  name                = "${var.resource_prefix}-appinsights"
  location            = var.location
  resource_group_name = var.resource_group_name
  application_type    = "web"
}

resource "azurerm_service_plan" "appserviceplan" {
  name                = "${var.resource_prefix}-appserviceplan"
  location            = var.location
  resource_group_name = var.resource_group_name
  os_type             = "Linux"
  sku_name            = "S1"
}

resource "azurerm_linux_web_app" "appservice" {
  name                       = "${var.resource_prefix}-appservice"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  service_plan_id            = azurerm_service_plan.appserviceplan.id
  client_certificate_enabled = var.authmode == "Basic" ? false : true
  client_certificate_mode    = var.authmode == "Basic" ? "Optional" : "Required"

  site_config {
    container_registry_use_managed_identity = true

    application_stack {
      docker_image     = "${var.acr_login_server}/sample/estserver"
      docker_image_tag = "v2"
    }
  }

  identity {
    type = "SystemAssigned"
  }

  app_settings = {
    WEBSITES_ENABLE_APP_SERVICE_STORAGE          = false
    "Keyvault__KeyVaultUrl"                      = var.keyvault_url
    "EstAuthentication__Auth"                    = var.authmode
    "EstAuthentication__EstUsername"             = var.est_user
    "EstAuthentication__EstPassword"             = var.est_password
    "KeyVault__IssuingCA"                        = var.issuing_ca
    "KeyVault__CertValidityInDays"               = var.cert_validity_in_days
    "APPINSIGHTS_INSTRUMENTATIONKEY"             = azurerm_application_insights.appinsights.instrumentation_key
    "ApplicationInsights__ConnectionString"      = azurerm_application_insights.appinsights.connection_string
    "ApplicationInsightsAgent_EXTENSION_VERSION" = "~2"
  }
}

resource "azurerm_role_assignment" "app_acr" {
  scope                = var.acr_id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.appservice.identity.0.principal_id
}

resource "azurerm_key_vault_access_policy" "app_accesspolicy" {
  key_vault_id = var.keyvault_id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_linux_web_app.appservice.identity.0.principal_id

  key_permissions = [
    "Sign"
  ]

  certificate_permissions = [
    "Get", "List", "Update", "Create"
  ]
}