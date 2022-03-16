variable "location" {
  type    = string
  default = "westeurope"
}

variable "resource_prefix" {
  type    = string
  default = ""
}

variable "EstUser" {
  type    = string
  default = "foo"
}

variable "EstPassword" {
  type    = string
  default = "bar"
}

variable "AuthMode" {
  type    = string
  default = "Basic"
}