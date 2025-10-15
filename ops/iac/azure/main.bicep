// Azure deployment stub for LaySumm PLS platform.
// Resources: AKS/Container Apps, Service Bus, Storage, Cognitive Search, Key Vault, Monitor.
// Parameterize environment (dev/stage/prod) + region for residency control.

param environment string
param location string = resourceGroup().location
param namePrefix string

// Container registry
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: '${namePrefix}${environment}acr'
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    adminUserEnabled: false
    networkRuleSet: {
      defaultAction: 'Deny'
      virtualNetworkRules: [] // Use Private Endpoints
    }
  }
}

// Kubernetes cluster (swap to Container Apps if desired)
resource aks 'Microsoft.ContainerService/managedClusters@2024-02-01' = {
  name: '${namePrefix}-${environment}-aks'
  location: location
  sku: {
    name: 'Base'
    tier: 'Standard'
  }
  properties: {
    dnsPrefix: '${namePrefix}-${environment}'
    identity: {
      type: 'SystemAssigned'
    }
    agentPoolProfiles: [
      {
        name: 'system'
        count: 3
        vmSize: 'Standard_D4s_v5'
        mode: 'System'
      }
    ]
    apiServerAccessProfile: {
      enablePrivateCluster: true
    }
  }
}

// Service Bus namespace for ingestion/batch queues
resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: '${namePrefix}-${environment}-sb'
  location: location
  sku: {
    name: 'Premium'
    tier: 'Premium'
    capacity: 1
  }
}

// Storage account for document blob container
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: toLower('${namePrefix}${environment}docs')
  location: location
  sku: {
    name: 'Standard_ZRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

// Key Vault for secrets + redaction maps
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${namePrefix}-${environment}-kv'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
  }
}

// Azure Cognitive Search for hybrid retrieval
resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: '${namePrefix}-${environment}-search'
  location: location
  sku: {
    name: 'standard'
  }
  properties: {
    replicaCount: 2
    partitionCount: 1
    hostingMode: 'default'
  }
}

// OTLP exporter (Azure Monitor workspace)
resource monitor 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${namePrefix}-${environment}-logs'
  location: location
  properties: {
    retentionInDays: 30
  }
}

// Outputs consumed by GitHub Actions environments
output containerRegistryName string = acr.name
output kubeName string = aks.name
output serviceBusNamespace string = serviceBus.name
output storageAccountName string = storage.name
output keyVaultName string = keyVault.name
output searchServiceName string = search.name
output logAnalyticsWorkspaceId string = monitor.properties.customerId
