output "edge_device_name" {
  value = azurerm_linux_virtual_machine.iot_edge.name
}

output "vnet_name" {
  value = azurerm_virtual_network.iot_edge.name
}

output "vnet_id" {
  value = azurerm_virtual_network.iot_edge.id
}

output "iotedge_subnet_id" {
  value = azurerm_subnet.iotedge_subnet.id
}

output "vm_username" {
  value = var.vm_username
}

output "vm_password" {
  value = local.vm_password
}
