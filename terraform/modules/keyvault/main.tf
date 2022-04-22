data "azurerm_client_config" "current" {}

locals {
  certs_path = "${path.root}/../Certs/${var.resource_prefix}"
}

resource "azurerm_key_vault" "keyvault-ca" {
  name                        = "kv-${var.resource_prefix}"
  location                    = var.location
  resource_group_name         = var.resource_group_name
  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  purge_protection_enabled    = false
  soft_delete_retention_days  = 7
  sku_name                    = "standard"
}

resource "azurerm_key_vault_access_policy" "user_accesspolicy" {
  key_vault_id = azurerm_key_vault.keyvault-ca.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = data.azurerm_client_config.current.object_id

  key_permissions = [
    "Sign"
  ]

  certificate_permissions = [
    "Get", "List", "Update", "Create", "Import", "Delete", "Recover", "Backup", "Restore", "ManageIssuers", "GetIssuers", "ListIssuers", "SetIssuers", "DeleteIssuers"
  ]
}

resource "null_resource" "run-api-facade" {
  triggers = {
    key     = "${local.certs_path}.key"
    csr     = "${local.certs_path}.csr"
    csr_der = "${local.certs_path}.csr.der"
    cert    = "${local.certs_path}-cert"
  }

  provisioner "local-exec" {
    interpreter = ["/bin/bash", "-c"]
    working_dir = "${path.root}/../KeyvaultCA"
    when        = create
    command     = <<EOF
      set -Eeuo pipefail

      dotnet run --Csr:IsRootCA true --Csr:Subject "C=US, ST=WA, L=Redmond, O=Contoso, OU=Contoso HR, CN=Contoso Inc" --Keyvault:IssuingCA ${var.issuing_ca} --Keyvault:KeyVaultUrl ${azurerm_key_vault.keyvault-ca.vault_uri}
      openssl genrsa -out ${self.triggers.key} 2048
      openssl req -new -key ${self.triggers.key} -subj "/C=US/ST=WA/L=Redmond/O=Contoso/CN=Contoso Inc" -out ${self.triggers.csr}
      openssl req -in ${self.triggers.csr} -out ${self.triggers.csr_der} -outform DER
      dotnet run --Csr:IsRootCA false --Csr:PathToCsr ${self.triggers.csr_der} --Csr:OutputFileName ${self.triggers.cert} --Keyvault:IssuingCA ${var.issuing_ca} --Keyvault:KeyVaultUrl ${azurerm_key_vault.keyvault-ca.vault_uri}
    EOF
  }

  provisioner "local-exec" {
    interpreter = ["/bin/bash", "-c"]
    working_dir = "${path.root}/../KeyvaultCA"
    when        = destroy
    command     = "rm -f ${self.triggers.key} ${self.triggers.csr} ${self.triggers.csr_der} ${self.triggers.cert}"
  }
}