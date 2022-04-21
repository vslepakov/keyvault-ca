variable "location" {
  type    = string
  default = "westeurope"
}

variable "resource_prefix" {
  type    = string
  default = ""
}

variable "vm_user_name" {
  type    = string
  default = "azureuser"
}

variable "vm_password" {
  type    = string
  default = ""
}

variable "edge_vm_sku" {
  type    = string
  default = "Standard_DS1_v2"
}