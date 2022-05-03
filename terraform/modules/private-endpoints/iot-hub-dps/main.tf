resource "azurerm_subnet" "iot_subnet" {
  name                 = "snet-iot"
  resource_group_name  = var.resource_group_name
  virtual_network_name = var.vnet_name
  address_prefixes     = [cidrsubnet(var.cidr_prefix, 8, 3)]

  enforce_private_link_endpoint_network_policies = true
}

resource "azurerm_network_security_group" "iot_nsg" {
  name                = "nsg-iot-${var.resource_uid}"
  resource_group_name = var.resource_group_name
  location            = var.location
}

resource "azurerm_subnet_network_security_group_association" "iot_subnet_assoc" {
  subnet_id                 = azurerm_subnet.iot_subnet.id
  network_security_group_id = azurerm_network_security_group.iot_nsg.id
}

# IOT HUB
resource "azurerm_private_dns_zone" "iothub_dns_zone" {
  name                = "privatelink.azure-devices.net"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone_virtual_network_link" "iothub_dns_link" {
  name                  = "iothub-dns-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.iothub_dns_zone.name
  virtual_network_id    = var.vnet_id
}

resource "azurerm_private_dns_a_record" "iothub_dns_a_record" {
  name                = "iothub-private-dns-a-record"
  zone_name           = azurerm_private_dns_zone.iothub_dns_zone.name
  resource_group_name = var.resource_group_name
  ttl                 = 300
  records             = [cidrhost(azurerm_subnet.iot_subnet.address_prefixes[0], 1)]
}

resource "azurerm_private_endpoint" "iothub_private_endpoint" {
  name                = "pe-iothub-${var.resource_uid}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = azurerm_subnet.iot_subnet.id

  private_service_connection {
    name                           = "iothub_connection"
    private_connection_resource_id = var.iot_hub_id
    is_manual_connection           = false
    subresource_names              = ["iotHub"]
  }

  private_dns_zone_group {
    name                 = "pdnsz-iothub-${var.resource_uid}"
    private_dns_zone_ids = [azurerm_private_dns_zone.iothub_dns_zone.id]
  }

  depends_on = [var.dps_shared_access_policy_id]
}

# DEVICE PROVISIONING SERVICE
resource "azurerm_private_dns_zone" "dps_dns_zone" {
  name                = "privatelink.azure-devices-provisioning.net"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone_virtual_network_link" "dps_dns_link" {
  name                  = "dps-dns-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.dps_dns_zone.name
  virtual_network_id    = var.vnet_id
}

resource "azurerm_private_dns_a_record" "dps_dns_a_record" {
  name                = "dps-private-dns-a-record"
  zone_name           = azurerm_private_dns_zone.dps_dns_zone.name
  resource_group_name = var.resource_group_name
  ttl                 = 300
  records             = [cidrhost(azurerm_subnet.iot_subnet.address_prefixes[0], 2)]
}

resource "azurerm_private_endpoint" "dps_private_endpoint" {
  name                = "pe-dps-${var.resource_uid}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = azurerm_subnet.iot_subnet.id

  private_service_connection {
    name                           = "dps-connection"
    private_connection_resource_id = var.iot_dps_id
    is_manual_connection           = false
    subresource_names              = ["iotDps"]
  }

  private_dns_zone_group {
    name                 = "pdnsz-dps-${var.resource_uid}"
    private_dns_zone_ids = [azurerm_private_dns_zone.dps_dns_zone.id]
  }

  depends_on = [var.iot_dps_id, var.dps_rootca_enroll_null_resource_id]
}