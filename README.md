# Some Context

Please refer to [this blog post](https://vslepakov.medium.com/build-a-lightweight-pki-for-iot-using-azure-keyvault-acc46bce26ed) for details.  

![Overview](assets/arch.png "High Level Architecture")

# Prerequisites

1. Create Azure Key Vault:  
```az keyvault create --name <KEYVAULT_NAME> \```  
```--resource-group keyvault-ca \```  
```--enable-soft-delete=true \```  
```--enable-purge-protection=true```  

## Setup Key Vault access for the API façade

1. Create a Service Principal in AAD  
```az ad sp create-for-rbac --name <SP_NAME> --skip-assignment=true```  
You will get an output containing ```appId``` and ```password```, please note them for later.

2. Give the Service Principal accesss to KeyVault keys and certificates:  
```az keyvault set-policy --name <KEYVAULT_NAME> \```  
```--spn <YOUR_APPID> \```  
```--key-permissions sign \```  
``` --certificate-permissions get list update create```  

# Getting Started

Clone this repository. There are two projects (`KeyVaultCA` and `KeyVaultCA.Web`) containing `appsettings.json` files. The settings specified there can also be overridden with environment variables or command line arguments.
The following common block must be filled in, for all usages of the projects.
```
"KeyVault": {
    "AppId": "<AppId of the AAD service principal that can access KeyVault.>",
    "Secret": "<Password of the AAD service principal that can access KeyVault.>",
    "KeyVaultUrl": "<Key Vault URL>",
    "IssuingCA": "<Name of the issuing certificate in KeyVault.>"
  }
```
For overriding settings from command line arguments on Linux, use a syntax similar to `KeyVault__KeyVaultUrl` and for Windows, `KeyVault:KeyVaultUrl`.

# Use the Console application
## Generate a new Root CA in Key Vault

1. Run the API Facade like this (feel free to use your own values for the subject):  
```cd KeyVaultCA```  
```dotnet run --Csr:IsRootCA "true" --Subject "C=US, ST=WA, L=Redmond, O=Contoso, OU=Contoso HR, CN=Contoso Inc"``` 

## Request a new device certificate

1. First generate the private key:  
```openssl genrsa -out mydevice.key 2048```  

2. Create the CSR:  
```openssl req -new -key mydevice.key -out mydevice.csr```  
```openssl req -in mydevice.csr -out mydevice.csr.der -outform DER```

3. Run the API Facade and pass all required arguments:   
```cd KeyVaultCA```  
```dotnet run --Csr:IsRootCA "false" --Csr:PathToCsr <PATH_TO_CSR_IN_DER_FORMAT> --Csr:OutputFileName <OUTPUT_CERTIFICATE_FILENAME>```

If desired, values can also be set in the `Csr` block of the `appsettings.json`.
```
"Csr": {
    "IsRootCA": "<Boolean value. To register a Root CA, value should be true, otherwise false.>",
    "Subject": "<Subject in the format 'C=US, ST=WA, L=Redmond, O=Contoso, OU=Contoso HR, CN=Contoso Inc'.>",
    "PathToCsr": "<Path to the CSR file in .der format.>",
    "OutputFileName": "<Output file name for the certificate.>"
  }
  ```

# Use the [EST](https://tools.ietf.org/html/rfc7030) Façade Web API to request a certificate

DISCLAIMER: NOT all of the EST methods are implemented. It is only:
- [/cacerts](https://tools.ietf.org/html/rfc7030#section-4.1)
- [/simpleenroll](https://tools.ietf.org/html/rfc7030#section-4.2)

The endpoints above are used by and work with Azure IoT Edge 1.2 which supports certificate enrollment via EST.

When calling the EST endpoints for:
- generating the device identity certificate (needed for authenticating to the IoT Hub), use the URL like in the example - `https://example-est.azurewebsites.net/.well-known/est`
- generating the Edge CA certificate (needed for authenticating the IoT edge modules), use `ca` as part of the URL - e.g. `https://example-est.azurewebsites.net/ca/.well-known/est`.

Build and deploy (or run in container) the ```KeyVaultCA.Web``` project.  
You need to provide the following variables in the `appsettings.json`, which will also be used when publishing the app to Azure:  
  
1. ```Secret``` - the service principal secret to access the KeyVault (see ```YOUR_APP_SECRET``` above).  
2. ```AppId``` - the app id of the service principal to access the KeyVault (see ```YOUR_APPID``` above).  
3. ```KeyVaultUrl``` - url of your KeyVault the format depending on whether it is accessible via public or private endpoint:
    - for a public endpoint, the format is `https://<KEYVAULT_NAME>.vault.azure.net/`
    - for a private endpoint with Azure built-in DNS integration, the format is `https://<KEYVAULT_NAME>.privatelink.vaultcore.azure.net/`
    - for a private endpoint with custom DNS integration, the format is `https://<KEYVAULT_NAME>.vaultcore.azure.net/`
4. ```IssuingCA``` - name of the certificate in the KeyVault to issue your leaf certificate (same as ```NAME_OF_ROOT_CA``` above).  
5. ```CertValidityInDays``` - specifies validity period for issued certificates.  
6. ```Auth``` - Authentication mode for the EST API. Possible values are: 
- **Basic** - add the following environment variables: 
    - ```EstUsername``` - username for the EST endpoint
    - ```EstPassword``` - password for the EST endpoint 
- **x509** - via certificates
    - put your trusted CA certificates into the ```KeyVaultCA.Web\TrustedCAs``` folder. Make sure to specify CopyToOutput. Note that certificates downloaded from Azure Key Vault are by default encoded as a base64 string.  
   -  if you choose to publish the ```KeyVaultCA.Web``` app to an Azure App Service Plan, make sure to go to **Configuration** -> **General Setttings** -> **Incoming client certificates** -> set **Client certificate mode** to `Require`. 
The implementation returns the `IssuingCA` via the ```/cacerts``` endpoint.  
Refer to [this repo](https://github.com/arlotito/iot-edge-1.2-tpm) for details on IoT Edge configuration, including PKCS#11 and EST.

## Logging

The `KeyVaultCA` console app uses a Console logger, for which the severity can be changed in the `appsettings.json`.

The `KeyVaultCA.Web` writes logs to an Azure Application Insights instance, for which the connection string must be added in the `appsettings.json`. Additionally, the logging must be turned on from the Azure portal by going to the Web App and into the Application Insights settings.
