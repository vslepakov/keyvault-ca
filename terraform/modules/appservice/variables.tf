variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_prefix" {
  type = string
}

variable "app_id" {
  type = string
  sensitive = true
}

variable "app_secret" {
  type = string
  sensitive = true
}

variable "keyvault_url" {
  type = string
}

variable "authmode" {
  type    = string
  default = "Basic"
}

variable "est_user" {
  type    = string
  default = "foo"
}

variable "est_password" {
  type    = string
  default = "bar"
}

variable "issuing_ca" {
  type = string
}

variable "cert_validity_in_days" {
  type = string
  default = "365"
}

variable "acr_name" {
  type = string
}

variable "acr_login_server" {
  type = string
}

variable "acr_admin_username" {
  type = string
}

variable "acr_admin_password" {
  type = string
  sensitive = true
}