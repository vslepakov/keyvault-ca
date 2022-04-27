variable "location" {
  type    = string
  default = "westeurope"
}

variable "resource_prefix" {
  type    = string
  default = ""
}

variable "edge_vm_sku" {
  type    = string
  default = "Standard_DS1_v2"
}

variable "auth_mode" {
  type    = string
  default = "x509"
}