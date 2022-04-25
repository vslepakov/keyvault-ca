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