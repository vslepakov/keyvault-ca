data "azurerm_client_config" "current" {}

resource "random_string" "est_password" {
  length  = 10
  number  = true
  special = true
}

locals {
  est_password = var.est_password == "" ? random_string.est_password.result : var.est_password
}

resource "azurerm_application_insights" "appinsights" {
  name                = "appi-${var.resource_uid}"
  location            = var.location
  resource_group_name = var.resource_group_name
  application_type    = "web"
}

resource "azurerm_service_plan" "appserviceplan" {
  name                = "plan-${var.resource_uid}"
  location            = var.location
  resource_group_name = var.resource_group_name
  os_type             = "Linux"
  sku_name            = "S1"
}

resource "azurerm_linux_web_app" "appservice" {
  name                       = "app-${var.resource_uid}"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  service_plan_id            = azurerm_service_plan.appserviceplan.id
  client_certificate_enabled = var.auth_mode == "Basic" ? false : true
  client_certificate_mode    = var.auth_mode == "Basic" ? "Optional" : "Required"

  site_config {
    container_registry_use_managed_identity = true

    application_stack {
      docker_image     = "${var.acr_login_server}/estserver"
      docker_image_tag = "latest"
    }
  }

  identity {
    type = "SystemAssigned"
  }

  app_settings = {
    WEBSITES_ENABLE_APP_SERVICE_STORAGE          = false
    "Keyvault__KeyVaultUrl"                      = var.keyvault_url
    "EstAuthentication__Auth"                    = var.auth_mode
    "EstAuthentication__EstUsername"             = var.est_username
    "EstAuthentication__EstPassword"             = local.est_password
    "KeyVault__IssuingCA"                        = var.issuing_ca
    "KeyVault__CertValidityInDays"               = var.cert_validity_in_days
    "APPINSIGHTS_INSTRUMENTATIONKEY"             = azurerm_application_insights.appinsights.instrumentation_key
    "ApplicationInsights__ConnectionString"      = azurerm_application_insights.appinsights.connection_string
    "ApplicationInsightsAgent_EXTENSION_VERSION" = "~2"
    "WEBSITE_PULL_IMAGE_OVER_VNET"               = true
  }
}