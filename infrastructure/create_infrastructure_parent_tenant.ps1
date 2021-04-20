param(
  [Parameter(Mandatory=$true)]
  [string] $AppName,

  [Parameter(Mandatory=$true)]
  [string] $SubscriptionName,

  [Parameter(Mandatory=$true)]
  [string] $region
)

$today = (Get-Date).ToString("yyyyMMdd")

az login  --use-device-code
az account set -s $SubscriptionName

$ResourceGroup = "{0}_core_rg" -f $AppName
$functionAppName = "func-{0}02" -f $AppName
$funcStorageName = "{0}sa02" -f $AppName
$keyVaultName = "kv-{0}02" -f $AppName
$serviceBusNameSpace = "sb-{0}02" -f $AppName

$accountInfo = $(az account show -o json | ConvertFrom-Json)
$deployer = $accountInfo.user.name 
$resourceId = "/subscriptions/{0}/resourcegroups/{1}" -f $accountInfo.id, $ResourceGroup

az group create -n $ResourceGroup -l $region
az tag create --resource-id $resourceId --tags AppName=AAD-User-Account-Sync DeployDate=$today Deployer=$deployer

# Create an Azure Function with storage accouunt in the resource group.
az storage account create --name $funcStorageName --location $region --resource-group $ResourceGroup --sku Standard_LRS
az functionapp create --name $functionAppName --storage-account $funcStorageName --consumption-plan-location $region --resource-group $ResourceGroup  --functions-version 3 
az functionapp identity assign --name $functionAppName --resource-group $ResourceGroup
$functionAppId=$(az functionapp identity show --name $functionAppName --resource-group $ResourceGroup --query 'principalId' --output tsv)

# Create Event Hub
$userCreateQueue = "UserCreated"
$userDeletedQueue = "UserDeleted"

az servicebus namespace create -g $ResourceGroup -n $serviceBusNameSpace -l $region --sku Basic 
az servicebus namespace authorization-rule create --name "aadaccess-read" --namespace-name $serviceBusNameSpace --resource-group $ResourceGroup --rights Listen
az servicebus namespace authorization-rule create --name "aadaccess-write" --namespace-name $serviceBusNameSpace --resource-group $ResourceGroup --rights Send
az servicebus queue create -g $ResourceGroup --namespace-name $serviceBusNameSpace -n $userCreateQueue 
az servicebus queue create -g $ResourceGroup --namespace-name $serviceBusNameSpace -n $userDeletedQueue
$readServiceBusConnectionString = $(az servicebus namespace authorization-rule keys list --name "aadaccess-read" --namespace-name $serviceBusNameSpace --resource-group $ResourceGroup --query "primaryConnectionString" --output tsv)
$writeServiceBusConnectionString = $(az servicebus namespace authorization-rule keys list --name "aadaccess-write" --namespace-name $serviceBusNameSpace --resource-group $ResourceGroup --query "primaryConnectionString" --output tsv)

# Create Key Vault 
az keyvault create --name $keyVaultName --resource-group $ResourceGroup --location $region
az keyvault set-policy --name $keyVaultName --object-id $functionAppId --secret-permissions get  --output none

# Set Function Secrets
$sbConnectionString = "servicebus"
$sbKeyVaultUri = $(az keyvault secret set --name $sbConnectionString --value $readServiceBusConnectionString --vault-name $keyVaultName --query 'id' --output tsv)
az functionapp config appsettings set -g $ResourceGroup -n $functionAppName --settings servicebus="@Microsoft.KeyVault(SecretUri=$sbKeyVaultUri)"

#Grant Azure Function access to Graph API
az ad app permission grant --api '00000003-0000-0000-c000-000000000000' --id $functionAppId --scope "User.ReadWrite.All"

# Results Application name
if($?){
  Write-Host "------------------------------------"
  Write-Host ("Infrastructure built successfully. Application Name: {0}" -f $AppName)
  Write-Host ("Service Bus Connection String: {0}" -f $writeServiceBusConnectionString)
  Write-Host "------------------------------------"
}
else {
  Write-Host "------------------------------------"
  Write-Host ("Errors encountered while building infrastructure. Please review. Application Name: {0}" -f $AppName )
  Write-Host "------------------------------------"
}