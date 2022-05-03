resource "azurerm_subnet" "app_subnet" {
  name                 = "snet-app"
  resource_group_name  = var.resource_group_name
  virtual_network_name = var.vnet_name
  address_prefixes     = [cidrsubnet(var.cidr_prefix, 8, 5)]

  enforce_private_link_endpoint_network_policies = true
}

resource "azurerm_network_security_group" "app_nsg" {
  name                = "nsg-app-${var.resource_uid}"
  resource_group_name = var.resource_group_name
  location            = var.location
}

resource "azurerm_subnet_network_security_group_association" "app_subnet_assoc" {
  subnet_id                 = azurerm_subnet.app_subnet.id
  network_security_group_id = azurerm_network_security_group.app_nsg.id
}

resource "azurerm_subnet" "app_vnet_integration_subnet" {
  name                 = "snet-app-vnet-integration"
  resource_group_name  = var.resource_group_name
  virtual_network_name = var.vnet_name
  address_prefixes     = [cidrsubnet(var.cidr_prefix, 8, 6)]

  delegation {
    name = "delegation"

    service_delegation {
      name    = "Microsoft.Web/serverFarms"
      actions = ["Microsoft.Network/virtualNetworks/subnets/action"]
    }
  }
}

resource "azurerm_private_dns_zone" "app_dns_zone" {
  name                = "privatelink.azurewebsites.net"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone_virtual_network_link" "app_dns_link" {
  name                  = "app-dns-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.app_dns_zone.name
  virtual_network_id    = var.vnet_id
}

resource "azurerm_private_dns_a_record" "app_dns_a_record" {
  name                = "app-private-dns-a-record"
  zone_name           = azurerm_private_dns_zone.app_dns_zone.name
  resource_group_name = var.resource_group_name
  ttl                 = 300
  records             = [cidrhost(azurerm_subnet.app_subnet.address_prefixes[0], 1)]
}

resource "azurerm_private_endpoint" "app_private_endpoint" {
  name                = "pe-app-${var.resource_uid}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = azurerm_subnet.app_subnet.id

  private_service_connection {
    name                           = "app-connection"
    private_connection_resource_id = var.app_id
    is_manual_connection           = false
    subresource_names              = ["sites"]
  }

  private_dns_zone_group {
    name                 = "pdnsz-${var.resource_uid}"
    private_dns_zone_ids = [azurerm_private_dns_zone.app_dns_zone.id]
  }

  depends_on = [var.app_id]
}

resource "azurerm_app_service_virtual_network_swift_connection" "app_vnet_connection" {
  app_service_id = var.app_id
  subnet_id      = azurerm_subnet.app_vnet_integration_subnet.id
}