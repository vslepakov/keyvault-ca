locals {
  dns_label_prefix = "${var.resource_uid}-iot-edge"
}

locals {
  vm_password = var.vm_password == "" ? random_string.vm_password.result : var.vm_password
}

data "local_file" "est_auth_cert" {
  filename   = "${path.root}/../Certs/${var.resource_uid}-cert.pem"
  depends_on = [var.run_api_facade_null_resource_id]
}

data "local_file" "est_auth_key" {
  filename   = "${path.root}/../Certs/${var.resource_uid}.key.pem"
  depends_on = [var.run_api_facade_null_resource_id]
}

resource "random_string" "vm_password" {
  length  = 10
  number  = true
  special = true
}

resource "azurerm_public_ip" "iot_edge" {
  name                = "pip-${local.dns_label_prefix}"
  resource_group_name = var.resource_group_name
  location            = var.location
  allocation_method   = "Dynamic"
  domain_name_label   = local.dns_label_prefix
}

resource "azurerm_network_security_group" "iot_edge" {
  name                = "nsg-iotedge-${var.resource_uid}"
  resource_group_name = var.resource_group_name
  location            = var.location

  security_rule {
    name                       = "AllowBastionInbound"
    priority                   = 900
    access                     = "Allow"
    direction                  = "Inbound"
    protocol                   = "*"
    source_address_prefix      = "10.0.2.0/26"
    source_port_range          = "*"
    destination_address_prefix = "*"
    destination_port_ranges    = ["3389", "22"]
  }

  security_rule {
    name                       = "Default-Deny-22"
    priority                   = 1000
    access                     = "Deny"
    direction                  = "Inbound"
    protocol                   = "Tcp"
    destination_port_range     = "22"
    source_address_prefix      = "*"
    source_port_range          = "*"
    destination_address_prefix = "*"
  }
}

resource "azurerm_virtual_network" "iot_edge" {
  name                = "vnet-${var.resource_uid}"
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = ["10.0.0.0/16"]
}

resource "azurerm_subnet" "iotedge_subnet" {
  name                 = "edge-device-subnet"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.iot_edge.name
  address_prefixes     = ["10.0.1.0/24"]
}

resource "azurerm_network_interface" "iot_edge" {
  name                = "nic-${local.dns_label_prefix}"
  location            = var.location
  resource_group_name = var.resource_group_name

  ip_configuration {
    name                          = "ipconf-${local.dns_label_prefix}"
    private_ip_address_allocation = "Dynamic"
    public_ip_address_id          = azurerm_public_ip.iot_edge.id
    subnet_id                     = azurerm_subnet.iotedge_subnet.id
  }
}

resource "azurerm_subnet_network_security_group_association" "iotedge_subnet_assoc" {
  subnet_id                 = azurerm_subnet.iotedge_subnet.id
  network_security_group_id = azurerm_network_security_group.iot_edge.id
}

resource "azurerm_linux_virtual_machine" "iot_edge" {
  name                            = "vm-${var.edge_device_name}"
  location                        = var.location
  resource_group_name             = var.resource_group_name
  admin_username                  = var.vm_username
  disable_password_authentication = false
  admin_password                  = local.vm_password

  provision_vm_agent         = false
  allow_extension_operations = false
  size                       = var.vm_sku
  network_interface_ids      = [azurerm_network_interface.iot_edge.id]

  custom_data = base64encode(templatefile("modules/iot-edge/cloud-init.yaml", {
    "SCOPE_ID"         = var.dps_scope_id
    "DEVICE_ID"        = var.edge_device_name
    "EST_HOSTNAME"     = var.app_hostname
    "EST_USERNAME"     = var.est_username
    "EST_PASSWORD"     = var.est_password
    "VM_USER_NAME"     = var.vm_username
    "DPS_NAME"         = var.iot_dps_name
    "ACR_USERNAME"     = var.acr_admin_username
    "ACR_PASSWORD"     = var.acr_admin_password
    "ACR_NAME"         = var.acr_name
    "AUTH_CERTIFICATE" = var.auth_mode == "x509" ? indent(6, data.local_file.est_auth_cert.content) : ""
    "AUTH_KEY"         = var.auth_mode == "x509" ? indent(6, data.local_file.est_auth_key.content) : ""
  }))

  source_image_reference {
    offer     = "0001-com-ubuntu-server-focal" #UbuntuServer does not seem to have 20.04 LTS image available (also through az vm image list)
    publisher = "Canonical"
    sku       = "20_04-lts-gen2"
    version   = "latest"
  }

  os_disk {
    caching              = "ReadWrite"
    storage_account_type = "Premium_LRS"
  }
}