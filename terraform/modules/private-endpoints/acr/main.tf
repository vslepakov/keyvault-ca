resource "azurerm_subnet" "acr_subnet" {
  name                 = "acr-subnet"
  resource_group_name  = var.resource_group_name
  virtual_network_name = var.vnet_name
  address_prefixes     = ["10.0.4.0/24"]

  enforce_private_link_endpoint_network_policies = true
}

resource "azurerm_network_security_group" "acr_nsg" {
  name                = "nsg-acr-${var.resource_uid}"
  resource_group_name = var.resource_group_name
  location            = var.location
}

resource "azurerm_subnet_network_security_group_association" "acr_subnet_assoc" {
  subnet_id                 = azurerm_subnet.acr_subnet.id
  network_security_group_id = azurerm_network_security_group.acr_nsg.id
}

resource "azurerm_private_dns_zone" "acr_dns_zone" {
  name                = "privatelink.azurecr.io"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone_virtual_network_link" "acr_dns_link" {
  name                  = "acr_dns_link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.acr_dns_zone.name
  virtual_network_id    = var.vnet_id
}

resource "azurerm_private_dns_a_record" "acr_dns_a_record" {
  name                = "acr-private-dns-a-record"
  zone_name           = azurerm_private_dns_zone.acr_dns_zone.name
  resource_group_name = var.resource_group_name
  ttl                 = 300
  records             = ["10.0.4.1"]
}

resource "azurerm_private_endpoint" "acr_private_endpoint" {
  name                = "priv-endpoint-acr-${var.resource_uid}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = azurerm_subnet.acr_subnet.id

  private_service_connection {
    name                           = "acr_connection"
    private_connection_resource_id = var.acr_id
    is_manual_connection           = false
    subresource_names              = ["registry"]
  }

  private_dns_zone_group {
    name                 = "dns-zone-group-acr-${var.resource_uid}"
    private_dns_zone_ids = [azurerm_private_dns_zone.acr_dns_zone.id]
  }

  depends_on = [var.acr_id, var.push_docker_null_resource_id, var.push_iotedge_null_resource_id]
}