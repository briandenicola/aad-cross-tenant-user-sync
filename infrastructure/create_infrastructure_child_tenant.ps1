param(
  [Parameter(Mandatory=$true)]
  [string] $AppName,

  [Parameter(Mandatory=$true)]
  [string] $SubscriptionName,

  [Parameter(Mandatory=$true)]
  [string] $ServiceBusConnectionString,

  [Parameter(Mandatory=$true)]
  [string] $region
)

$today = (Get-Date).ToString("yyyyMMdd")

az login --use-device-code
az account set -s $SubscriptionName

$ResourceGroup = "{0}_core_rg" -f $AppName
$functionAppName = "func-{0}01" -f $AppName
$funcStorageName = "{0}sa01" -f $AppName
$keyVaultName = "kv-{0}01" -f $AppName
$eventHubNameSpace = "eh-{0}01" -f $AppName

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

#Create Event Hub
$hub="userevents"
az eventhubs namespace create -g $ResourceGroup -n $eventHubNameSpace -l $region --sku Basic 
az eventhubs eventhub create -g $ResourceGroup --namespace-name $eventHubNameSpace -n $hub --message-retention 1
az eventhubs eventhub authorization-rule create --name "aadaccess-write" --eventhub-name $hub --namespace-name $eventHubNameSpace --resource-group $ResourceGroup --rights Send
az eventhubs eventhub authorization-rule create --name "aadaccess-read" --eventhub-name $hub --namespace-name $eventHubNameSpace --resource-group $ResourceGroup --rights Listen
$EventHubWriteCnnectionString = $(az eventhubs eventhub authorization-rule keys list --name "aadaccess-write" --eventhub-name $hub --namespace-name $eventHubNameSpace --resource-group $ResourceGroup --query "primaryConnectionString" --output tsv)
$EventHubReadCnnectionString = $(az eventhubs eventhub authorization-rule keys list --name "aadaccess-read" --eventhub-name $hub --namespace-name $eventHubNameSpace --resource-group $ResourceGroup --query "primaryConnectionString" --output tsv)

# Create Key Vault
$graphChangeTrackerSpn = $(az ad sp list --display-name 'Microsoft Graph Change Tracking' --query "[].appId" --output tsv)

az keyvault create --name $keyVaultName --resource-group $ResourceGroup --location $region
az keyvault set-policy --name $keyVaultName --object-id $functionAppId --secret-permissions get  --output none
az keyvault set-policy --name $keyvaultname --resource-group $ResourceGroup --secret-permissions get --spn $graphChangeTrackerSpn --output none
$keyVaultUri = $(az keyvault show --name $keyVaultName --resource-group $ResourceGroup --query "properties.vaultUri" --output tsv)

#Set secrets
$ehWriterConnectionString = "eventhub"
$ehReaderConnectionString = "eventhub-reader"
$sbConnectionString = "servicebus"
$ehWriterKeyVaultUri = $(az keyvault secret set --name $ehWriterConnectionString --value $EventHubWriteCnnectionString --vault-name $keyVaultName --query 'id' --output tsv)
$ehReaderKeyVaultUri = $(az keyvault secret set --name $ehReaderConnectionString --value $EventHubReadCnnectionString --vault-name $keyVaultName --query 'id' --output tsv)
$sbKeyVaultUri = $(az keyvault secret set --name $sbConnectionString --value $ServiceBusConnectionString --vault-name $keyVaultName --query 'id' --output tsv)

#Set Function Secrets
az functionapp config appsettings set -g $ResourceGroup -n $functionAppName --settings eventhub="@Microsoft.KeyVault(SecretUri=$ehReaderKeyVaultUri)"
az functionapp config appsettings set -g $ResourceGroup -n $functionAppName --settings servicebus="@Microsoft.KeyVault(SecretUri=$sbKeyVaultUri)"

#Subscribe to Graph API Change Feed
$notificationWebhook = "EventHub:{0}secrets/{1}?tenantId={2}" -f $keyVaultUri, $ehWriterConnectionString, $accountInfo.name
$notificationSubscription  = "https://graph.microsoft.com/beta/subscriptions"
$notificationExpiration = (Get-Date $(Get-Date).AddDays(3).ToUniversalTime() -Format o).ToString()

$subscriptionBody = @"
{ 
  \"changeType\": \"updated,deleted\", 
  \"notificationUrl\": \"$notificationWebhook\", 
  \"resource\": \"Users\", 
  \"expirationDateTime\": \"$notificationExpiration\", 
  \"clientState\": \"secretClientValue\", 
  \"latestSupportedTlsVersion\": \"v1_2\" 
}
"@ 
az rest --method post --uri $notificationSubscription --headers "Content-Type=application/json" --body $subscriptionBody

#Grant Azure Function access to Graph API
$graphGlobalId = "00000003-0000-0000-c000-000000000000"
$appRoleId =$(az ad sp show --id $graphGlobalId --query "appRoles[?value=='User.Read.All'].id" -o tsv)
$graphSpnId = $(az ad sp show --id $graphGlobalId -o tsv --query "objectId")

$aadAppRoleBody = @"
{
  \"principalId\": \"$functionAppId\",
  \"resourceId\": \"$graphSpnId\",
  \"appRoleId\": \"$appRoleId\"
}
"@

$graphUri = "https://graph.microsoft.com/v1.0/servicePrincipals/{0}/appRoleAssignments" -f $functionAppId
az rest --method POST --uri $graphUri --headers 'Content-Type=application/json' --body $aadAppRoleBody

# echo Application name
if($?){
  Write-Host "------------------------------------"
  Write-Host ("Infrastructure built successfully. Application Name: {0}" -f $AppName)
  Write-Host "------------------------------------"
}
else {
  Write-Host "------------------------------------"
  Write-Host ("Errors encountered while building infrastructure. Please review. Application Name: {0}" -f $AppName )
  Write-Host "------------------------------------"
}
