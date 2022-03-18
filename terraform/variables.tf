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
  default = "Standard_DS1_v2" # The default value provisions a standard VM without a GPU. For a GPU VM, use Standard_NC4as_T4_v3. Note that this will be much more expensive than a standard VM.
}

variable "root_ca_certificate_path" {
  type        = string
  default     = ""
  description = "path to the root ca certificate .pem file"
}

variable "dps_root_ca_name" {
  type        = string
  default     = "root-ca"
  description = "Name of DPS root CA"
}