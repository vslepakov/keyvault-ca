variable "location" {
  type    = string
  default = "westeurope"
}

variable "resource_prefix" {
  type    = string
  default = ""
}

variable "edge_vm_user_name" {
  type    = string
  default = "" # The default value is empty, but in the usages of this variable it will be overridden by a generated value if left empty.
}

variable "edge_vm_sku" {
  type    = string
  default = "Standard_DS1_v2"
}