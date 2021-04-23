# Some Context

Please refer to [this blog post](https://vslepakov.medium.com/build-a-lightweight-pki-for-iot-using-azure-keyvault-acc46bce26ed) for details.  

![Overview](assets/arch.png "High Level Architecture")

# Getting Started

Clone this repo and then ```cd KeyVaultCA``` 

## Prerequisites

1. Create Azure KeyVault:  
```az keyvault create --name <KEYVAULT_NAME> \```  
```--resource-group keyvault-ca \```  
```--enable-soft-delete=true \```  
```--enable-purge-protection=true```  

## Setup KeyVault access for the API façade

1. Create a Service Principal in AAD  
```az ad sp create-for-rbac --name api-facade-for-keyvault-ca --skip-assignment=true```  
You will get an output containing ```appId``` and ```password```, please note them for later.

2. Give the Service Principal accesss to KeyVault keys and certificates:  
```az keyvault set-policy --name <KEYVAULT_NAME> \```  
```--spn <your appId> \```  
```--key-permissions sign \```  
``` --certificate-permissions get list update create```  

## Generate a new Root CA in KeyVault

1. Run the API Facade like this (feel free to use your own values for the subject):  
```dotnet run --appId <YOUR_APPID> --secret <YOUR_APP_SECRET> \```  
```--ca --subject="C=US, ST=WA, L=Redmond, O=Contoso, OU=Contoso HR, CN=Contoso Inc" \```  
```--issuercert ContosoRootCA --kvName <KEYVAULT_NAME>```  

## Request a new device certificate

1. First generate the private key:  
```openssl genrsa -out mydevice.key 2048```  

2. Create the CSR:  
```openssl req -new -key mydevice.key -out mydevice.csr```  
```openssl req -in mydevice.csr -out mydevice.csr.der -outform DER```

3. Run the API Facade and pass all required arguments:   
```dotnet run --appId <YOUR_APPID> --secret <YOUR_APP_SECRET> \```  
```--issuercert ContosoRootCA --csrPath <PATH_TO_CSR_IN_DER_FORMAT> \```  
```--output <OUTPUT_CERTIFICATE_FILENAME> --kvName <KEYVAULT_NAME>```

## Use the [EST](https://tools.ietf.org/html/rfc7030) Facade to request a certificate

DISCLAIMER: NOT all of the the EST methods are implemented. It is only:
- [/cacerts](https://tools.ietf.org/html/rfc7030#section-4.1)
- [/simpleenroll](https://tools.ietf.org/html/rfc7030#section-4.2)

The endpoints above are used by and work with Azure IoT Edge 1.2 which supports certificate enrollment via EST.  

Build and deploy (or run in container) the ```KeyVaultCA.Web``` project.  
You need to provide following environment variables:  
  
```Secret``` - This you service principal secret to access the KeyVault (see ```YOUR_APP_SECRET``` above)  
```AppId``` - This is the app id of the service principal to access the KeyVault (see ```YOUR_APPID``` above)  
```KeyVaultName``` - Name of your KeyVault  
```IssuingCA``` - Name of the certificate in the KeyVault to issue your leaf certificate  
```CACerts``` - CA certs stored in the KeyVault to return through the ```/est/cacerts``` endpoint  
```EstUser``` - Username for the EST enpoint (using Basic Auth for now, will update to use client certs)  
```EstPassword``` - Password for the EST endpoint (using Basic Auth for now, will update to use client certs)  