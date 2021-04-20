# Azure AD User Account Sync Across Tenants

Words go here..maybe

# Design 
![Dapr](./assets/design.png)


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
## Child Tenant 

<hr>

# Validate 