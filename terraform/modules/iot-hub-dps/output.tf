output "iot_dps_scope_id" {
  value = azurerm_iothub_dps.iot_dps.id_scope
}

output "iot_dps_name" {
  value = azurerm_iothub_dps.iot_dps.name
}

output "iot_dps_id" {
  value = azurerm_iothub_dps.iot_dps.id
}

output "iot_hub_id" {
  value = azurerm_iothub.iothub.id
}

output "dps_rootca_enroll_null_resource_id" {
  value = null_resource.dps_rootca_enroll.id
}

output "dps_shared_access_policy_id" {
  value = azurerm_iothub_shared_access_policy.iot_hub_dps_shared_access_policy.id
}

output "iot_hub_name" {
  value = azurerm_iothub.iothub.name
}

output "iot_hub_host_name" {
  value = azurerm_iothub.iothub.hostname
}

output "iot_hub_key_name" {
  value = azurerm_iothub.iothub.shared_access_policy.0.key_name
}

output "iot_hub_primary_key" {
  value     = azurerm_iothub.iothub.shared_access_policy.0.primary_key
  sensitive = true
}