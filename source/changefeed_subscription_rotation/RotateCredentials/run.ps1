param($Timer)

function Invoke-AzRestMethod {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateSet("GET","POST", "PUT")]
        [string] $Method,

        [Parameter(Mandatory=$true)]
        [string] $Uri,

        [Parameter(Mandatory=$true)]
        [string] $Body
    )

    $token = (Get-AzAccessToken -ResourceTypeName AadGraph).Token 
    $header = @{'Authorization' = ("Bearer {0}" -f $token)}
    $response = Invoke-RestMethod -Method $Method -Uri $Uri -ContentType "application/json" -Body $Body -Headers $header -Verbose
    return $response
}

$currentUTCtime = (Get-Date).ToUniversalTime()
Write-Host "PowerShell timer trigger function ran! TIME: $currentUTCtime"

$ehWriterConnectionString = $ENV:EVENTHUB_HUB_NAMNE
$keyVaultUri = $ENV:KEYVAULT_URI
$azureSubscription = $ENV:AZURE_SUBSCRIPTION

Select-AzSubscription -SubscriptionName $azureSubscription

$notificationWebhook = "EventHub:{0}secrets/{1}?tenantId={2}" -f $keyVaultUri, $ehWriterConnectionString, $(Get-AzContext).Tenant.Id
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
Invoke-AzRestMethod -Method Post -Uri $notificationSubscription -Body $subscriptionBody