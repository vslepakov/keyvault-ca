output "vm_username" {
  value = module.iot_edge.vm_username
}

output "vm_password" {
  value = module.iot_edge.vm_password
}

output "est_username" {
  value = var.auth_mode == "Basic" ? module.appservice.est_username : "Not set since user selected certificate authentication"
}

output "est_password" {
  value = var.auth_mode == "Basic" ? module.appservice.est_password : "Not set since user selected certificate authentication"
}

output "iot_hub_connection_string" {
  value     = join(";", ["HostName=${module.iot_hub_dps.iot_hub_host_name}", "SharedAccessKeyName=${module.iot_hub_dps.iot_hub_key_name}", "SharedAccessKey=${module.iot_hub_dps.iot_hub_primary_key}"])
  sensitive = true
}

output "edge_device_name" {
  value = local.edge_device_name
}

output "iot_hub_name" {
  value = module.iot_hub_dps.iot_hub_name
}