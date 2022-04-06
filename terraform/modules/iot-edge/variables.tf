variable "resource_prefix" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "vm_user_name" {
  type = string
}

variable "vm_sku" {
  type    = string
  default = "Standard_DS2_v2"
}

variable "dps_scope_id" {
  type = string
}

variable "edge_vm_name" {
  type = string
}

variable "app_hostname" {
  type = string
}

variable "est_user" {
  type    = string
}

variable "est_password" {
  type    = string
}