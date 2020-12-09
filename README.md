# Some Context
Please refer to [this blog post](ADD LINK) for details.  

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

## Generate a Root CA and Intermediate CA in KeyVault

1. Run the API Facade like this (`maxPathLength` specifies the number of intermediate CAs allowed in a device certificate's chain):  
```dotnet run --appId <YOUR_APPID> --secret <YOUR_APP_SECRET> \```  
```--ca --subject="C=US, ST=WA, L=Redmond, O=Contoso, OU=Contoso HR, CN=Contoso Inc" \```  
```--maxPathLength 1 --issuercert ContosoRootCA --kvName <KEYVAULT_NAME>```

2. Generate the private key for the intermediate CA:  
```openssl genrsa -out myintermediateca.key 2048```  

3. Create the CSR:  
```openssl req -new -key myintermediateca.key -out myintermediateca.csr```  
```openssl req -in myintermediateca.csr -out myintermediateca.csr.der -outform DER```

4. Run the API Facade and pass all required arguments:   
```dotnet run --appId <YOUR_APPID> --secret <YOUR_APP_SECRET> \```  
```--intermediate --maxPathLength 0```
```--issuercert ContosoRootCA --csrPath <PATH_TO_CSR_IN_DER_FORMAT> \```  
```--output <OUTPUT_CERTIFICATE_FILENAME> --kvName <KEYVAULT_NAME> \```
