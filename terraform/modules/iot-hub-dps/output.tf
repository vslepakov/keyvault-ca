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