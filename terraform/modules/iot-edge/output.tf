output "public_ssh" {
  value = "ssh -i ../.ssh/id_rsa ${local.vm_user_name}@${azurerm_public_ip.iot_edge.fqdn}"
}

output "private_ssh" {
  value     = tls_private_key.vm_ssh.private_key_pem
  sensitive = true
}

output "edge_device_name" {
  value = azurerm_linux_virtual_machine.iot_edge.name
}