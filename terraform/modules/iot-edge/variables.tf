variable "resource_uid" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "vm_username" {
  type    = string
  default = "azureuser"
}

variable "vm_password" {
  type    = string
  default = ""
}

variable "vm_sku" {
  type = string
}

variable "dps_scope_id" {
  type = string
}

variable "edge_device_name" {
  type = string
}

variable "app_hostname" {
  type = string
}

variable "est_username" {
  type = string
}

variable "est_password" {
  type = string
}

variable "iot_dps_name" {
  type = string
}

variable "acr_admin_password" {
  type = string
}

variable "acr_admin_username" {
  type = string
}

variable "acr_name" {
  type = string
}

variable "auth_mode" {
  type = string
}

variable "run_api_facade_null_resource_id" {
  type = string
}