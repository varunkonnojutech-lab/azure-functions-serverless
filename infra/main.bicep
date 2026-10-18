// Deploys the full backend for the OrderFunctions app:
// Storage account, Service Bus namespace + "orders" queue, Log Analytics,
// Application Insights, Key Vault (holding the Service Bus connection string
// as a secret), and a Linux Consumption Function App (.NET 8 isolated) whose
// ServiceBusConnection app setting is a Key Vault reference resolved at
// runtime through the app's system-assigned managed identity.

@description('Short project name used as a prefix for all resources (lowercase, letters and numbers).')
@minLength(3)
@maxLength(10)
param projectName string = 'orders'

@description('Azure region for all resources.')
param location string = resourceGroup().location

var suffix = uniqueString(resourceGroup().id)

// ---------- Storage (required by the Functions host) ----------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: toLower('${projectName}st${suffix}')
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

// ---------- Service Bus + orders queue ----------
resource serviceBus 'Microsoft.ServiceBus/namespaces@2023-01-01-preview' = {
  name: '${projectName}-sb-${suffix}'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

resource ordersQueue 'Microsoft.ServiceBus/namespaces/queues@2023-01-01-preview' = {
  parent: serviceBus
  name: 'orders'
  properties: {}
}

// ---------- Monitoring ----------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${projectName}-law-${suffix}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${projectName}-ai-${suffix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ---------- Key Vault ----------
// Uses access policies (not RBAC) with enabledForTemplateDeployment so the
// deployment itself can create the secret below. The Function App's managed
// identity gets Get/List on secrets via a separate accessPolicies child
// resource (kept separate to avoid a deployment dependency cycle).
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${projectName}-kv-${suffix}'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForTemplateDeployment: true
  }
}

resource kvAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-07-01' = {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: functionApp.identity.principalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
  }
}

// Service Bus connection string, stored as a secret so the Function App can
// reference it without the plaintext value ever appearing in app settings.
resource sbConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ServiceBusConnection'
  properties: {
    value: listKeys('${serviceBus.id}/AuthorizationRules/RootManageSharedAccessKey', '2023-01-01-preview').primaryConnectionString
  }
}

// ---------- Consumption plan + Function App (.NET 8 isolated on Linux) ----------
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${projectName}-plan-${suffix}'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp'
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${projectName}-func-${suffix}'
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${listKeys(storage.id, '2023-05-01').keys[0].value}'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          // Key Vault reference: resolved at runtime through the Function App's
          // system-assigned managed identity (see kvAccessPolicy above).
          name: 'ServiceBusConnection'
          value: '@Microsoft.KeyVault(SecretUri=${sbConnectionSecret.properties.secretUri})'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output keyVaultName string = keyVault.name
output serviceBusNamespace string = serviceBus.name
