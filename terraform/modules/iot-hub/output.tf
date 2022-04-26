output "iot_dps_scope_id" {
  value = azurerm_iothub_dps.iot_dps.id_scope
}

output "null_resource_dps_id" {
  value = null_resource.dps_rootca_enroll.id
}