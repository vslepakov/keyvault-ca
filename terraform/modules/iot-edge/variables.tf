variable "resource_prefix" {
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

variable "edge_vm_name" {
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