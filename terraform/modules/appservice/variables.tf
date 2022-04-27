variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_prefix" {
  type = string
}

variable "keyvault_url" {
  type = string
}

variable "auth_mode" {
  type = string
}

variable "est_username" {
  type    = string
  default = "azureuser"
}

variable "est_password" {
  type    = string
  default = ""
}

variable "issuing_ca" {
  type = string
}

variable "cert_validity_in_days" {
  type    = string
  default = "365"
}

variable "acr_id" {
  type = string
}

variable "acr_login_server" {
  type = string
}

variable "keyvault_id" {
  type = string
}