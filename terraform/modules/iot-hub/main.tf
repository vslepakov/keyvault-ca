resource "azurerm_iothub" "iothub" {
  name                          = "iot-${var.resource_prefix}"
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
  name                          = "provs-${var.resource_prefix}"
  resource_group_name           = var.resource_group_name
  location                      = var.location
  public_network_access_enabled = true

  sku {
    name     = "S1"
    capacity = "1"
  }

  linked_hub {
    connection_string       = azurerm_iothub_shared_access_policy.iot_hub_dps_shared_access_policy.primary_connection_string
    location                = var.location
    apply_allocation_policy = true
  }

  depends_on = [azurerm_iothub_shared_access_policy.iot_hub_dps_shared_access_policy]
}

# Currently using local exec instead of azurerm_iothub_dps_certificate due to missing option to verify CA during upload in Terraform, missing ability to create enrollment groups and to retrieve cert from Key Vault instead of manual download
resource "null_resource" "dps_rootca_enroll" {
  triggers = {
    cer = "${var.issuing_ca}.cer"
  }

  provisioner "local-exec" {
    interpreter = ["/bin/bash", "-c"]
    working_dir = "${path.root}/../KeyVaultCA.Web/TrustedCAs"
    when        = create
    command     = <<EOF
      set -Eeuo pipefail

      az config set extension.use_dynamic_install=yes_without_prompt

      CERT_NAME=$(az iot dps certificate list -g ${var.resource_group_name} --dps-name ${azurerm_iothub_dps.iot_dps.name} --query "value[?name=='${var.issuing_ca}'].name" -o tsv)

      if [ -z "$CERT_NAME" ]
      then
        az keyvault certificate download --vault-name ${var.keyvault_name} -n ${var.issuing_ca} -f ${self.triggers.cer} -e PEM
        az iot dps certificate create -g ${var.resource_group_name} -n ${var.issuing_ca} --dps-name ${azurerm_iothub_dps.iot_dps.name} -p ${self.triggers.cer} -v true
      else
        echo "Cert ${var.issuing_ca} already exists."
      fi

      az iot dps enrollment-group create -g ${var.resource_group_name} --dps-name ${azurerm_iothub_dps.iot_dps.name} --eid ${var.resource_prefix}-enrollmentgroup --ee true --cn ${var.issuing_ca}
    EOF
  }

  provisioner "local-exec" {
    interpreter = ["/bin/bash", "-c"]
    working_dir = "${path.root}/../KeyVaultCA.Web/TrustedCAs"
    when        = destroy
    command     = "rm -f ${self.triggers.cer}"
  }

  depends_on = [var.run_api_facade_null_resource_id]
}