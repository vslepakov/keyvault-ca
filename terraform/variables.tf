variable "location" {
  type    = string
  default = "westeurope"
}

variable "cidr_prefix" {
  type    = string
  default = "10.0.0.0/16"
}

variable "resource_uid" {
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