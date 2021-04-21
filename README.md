# Azure AD User Account Sync Across Tenants

Words go here..maybe

# Design 
![Dapr](./assets/design.png)

# Prerequisite
* [PowerShell 7](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.1)
* [The Azure Function commandline tool](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=linux%2Ccsharp%2Cbash#v2)
* [The Azure cli](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli-linux?pivots=apt)

# Infrastructure Setup
## Parent Tenant 
```
$AppName = (New-Uuid).Substring(0,8)
./create_infrastructure_parent_tenant.ps1 -AppName $AppName -SubscriptionName "{{Subscription_in_Parent_Tenant}} -region centralus
```
## Child Tenant 
```
$sb = {{The Service Bus connection string output of script above}}
./create_infrastructure_child_tenant.ps1 -AppName $AppName -SubscriptionName "{{Subscription_in_Child_Tenant}} -ServiceBusConnectionString $sb -region centralus
```

<hr>

# Code Deploy
## Parent Tenant 
```
cd source\parent_tenant
func azure functionapp publish ("func-{0}02" -f $AppName)
```

## Child Tenant 
```
cd source\child_tenant
func azure functionapp publish ("func-{0}01" -f $AppName)
```

<hr>

# Validate 
* Create User in Child Tenant. Confirm that the user is created as a Guest Account in Parent Tenant
* Delete User in Child Tenant. Confirm that the user is removed from Parent Tenant