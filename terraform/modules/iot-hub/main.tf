
terraform {
  required_providers {
    shell = {
      source  = "scottwinkler/shell"
      version = "1.7.7"
    }
  }
}

data "azurerm_subscription" "current" {
}

resource "azurerm_iothub" "iothub" {
  name                          = "${var.resource_prefix}-iot-hub"
  resource_group_name           = var.resource_group_name
  location                      = var.location
  public_network_access_enabled = true

  sku {
    name     = "S1"
    capacity = "1"
  }

  fallback_route {
    source         = "DeviceMessages"
    endpoint_names = ["events"]
    enabled        = true
  }

  tags = {
    purpose = "testing"
  }
}

resource "azurerm_iothub_shared_access_policy" "iothub_accesspolicy" {
  name                = "iot_hub_sas"
  resource_group_name = var.resource_group_name
  iothub_name         = azurerm_iothub.iothub.name
  registry_read       = true # allow reading from device registry
  registry_write      = true # allow writing to device registry
  service_connect     = true # allows c2d communication and access to service endpoints
  device_connect      = true # allows sending and receiving on the device-side endpoints
}

# resource "shell_script" "upload_verify_root_ca_certificate" {
#   lifecycle_commands {
#     create = "/bin/bash ../scripts/dps/enrollment/upload_and_verify_root_ca.sh"
#     delete = "echo noop"
#   }

#   environment = {
#     RESOURCE_GROUP_NAME     = var.resource_group_name
#     DPS_NAME                = azurerm_iothub_dps.iot_dps.name
#     DPS_ROOT_CA_NAME        = var.dps_root_ca_name
#     RESOURCE_GROUP_LOCATION = var.location
#     TERM                    = "xterm"
#   }

#   triggers = {
#     iot_hub_dps = azurerm_iothub_dps.iot_dps.id
#   }
# }


# resource "shell_script" "create_enrollment_group" {
#   lifecycle_commands {
#     create = "/bin/bash ../scripts/dps/enrollment/create_enrollment_group.sh"
#     delete = "echo noop"
#   }

#   environment = {
#     RESOURCE_GROUP_NAME     = var.resource_group_name
#     DPS_NAME                = azurerm_iothub_dps.iot_dps.name
#     RESOURCE_GROUP_LOCATION = var.location
#     IOT_HUB_NAME_ARR        = azurerm_iothub.iothub.name # This could contain an array of IoT Hub names
#     TERM                    = "xterm"
#   }

#   depends_on = [
#     shell_script.upload_verify_root_ca_certificate
#   ]

#   triggers = {
#     iot_hub_dps = azurerm_iothub_dps.iot_dps.id
#   }
# }

resource "azurerm_iothub_shared_access_policy" "iot_hub_dps_shared_access_policy" {
  name                = "iot-hub-dps-access"
  resource_group_name = var.resource_group_name
  iothub_name         = azurerm_iothub.iothub.name

  registry_read   = true
  registry_write  = true
  service_connect = true
  device_connect  = true

  # Explicit dependency statement needed to prevent shared_access_policy
  # creation to start prematurely.
  depends_on = [azurerm_iothub.iothub]
}


resource "azurerm_iothub_dps" "iot_dps" {
  name                = "${var.resource_prefix}-iotdps"
  resource_group_name = var.resource_group_name
  location            = var.location

  sku {
    name     = "S1"
    capacity = "1"
  }

  linked_hub {
    connection_string       = azurerm_iothub_shared_access_policy.iot_hub_dps_shared_access_policy.primary_connection_string
    location                = var.location
    apply_allocation_policy = true # must be set to true, or else device deployment will not happen
  }

  depends_on = [azurerm_iothub_shared_access_policy.iot_hub_dps_shared_access_policy]
}