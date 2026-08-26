// =========================================================
// main.bicep
// Provisions, from scratch: ACR, Log Analytics, Container Apps
// Environment, Key Vault (with Asgardeo secrets), and the
// GoRide Container App wired up with managed identity access.
// =========================================================

@description('Base name used to derive resource names (lowercase, no spaces)')
param baseName string = 'goride'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('The Asgardeo app client secret (pass via --parameters, never hardcode)')
@secure()
param asgardeoClientSecretValue string

@description('The Asgardeo management app client secret (pass via --parameters, never hardcode)')
@secure()
param asgardeoMgmtClientSecretValue string

@description('Non-secret Asgardeo values')
param asgardeoBaseUrl string = 'https://api.asgardeo.io/t/goride'
param asgardeoClientId string
param asgardeoMgmtClientId string
param asgardeoRiderRoleId string
param asgardeoDriverRoleId string

@description('Port the container listens on. The placeholder helloworld image uses 80; switch to 8080 (or your actual port) once you deploy your real GoRide image.')
param targetPort int = 80

@description('Container image to deploy, e.g. gorideacr.azurecr.io/goride-api:latest. Leave default for first deploy before an image exists.')
param containerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

// ---------- Names ----------
var acrName = '${baseName}acr${uniqueString(resourceGroup().id)}'
var keyVaultName = '${baseName}-kv-${uniqueString(resourceGroup().id)}'
var logAnalyticsName = '${baseName}-logs'
var containerAppEnvName = '${baseName}-env'
var containerAppName = '${baseName}-api'

// ---------- User-assigned managed identity ----------
// Created up front so we can grant it Key Vault + ACR access
// BEFORE the Container App tries to use it, avoiding the
// circular dependency that system-assigned identities cause here.
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-identity'
  location: location
}

// ---------- Container Registry ----------
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

// ---------- Log Analytics (required by Container Apps Environment) ----------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ---------- Container Apps Environment ----------
resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ---------- Key Vault ----------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    accessPolicies: []
  }
}

resource asgardeoClientSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'asgardeo-client-secret'
  properties: {
    value: asgardeoClientSecretValue
  }
}

resource asgardeoMgmtClientSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'asgardeo-mgmt-client-secret'
  properties: {
    value: asgardeoMgmtClientSecretValue
  }
}

// ---------- Role assignment: identity -> Key Vault Secrets User ----------
// Granted now, BEFORE the Container App exists, since this identity
// already exists as its own resource.
resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appIdentity.id, 'KeyVaultSecretsUser')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
    )
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------- Role assignment: identity -> AcrPull ----------
resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, appIdentity.id, 'AcrPull')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d' // AcrPull
    )
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------- Container App ----------
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: targetPort
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: appIdentity.id
        }
      ]
      secrets: [
        {
          name: 'asgardeo-client-secret'
          keyVaultUrl: asgardeoClientSecret.properties.secretUri
          identity: appIdentity.id
        }
        {
          name: 'asgardeo-mgmt-client-secret'
          keyVaultUrl: asgardeoMgmtClientSecret.properties.secretUri
          identity: appIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: containerAppName
          image: containerImage
          env: [
            { name: 'Asgardeo__BaseUrl', value: asgardeoBaseUrl }
            { name: 'Asgardeo__ClientId', value: asgardeoClientId }
            { name: 'Asgardeo__ClientSecret', secretRef: 'asgardeo-client-secret' }
            { name: 'AsgardeoMgmt__ClientId', value: asgardeoMgmtClientId }
            { name: 'AsgardeoMgmt__ClientSecret', secretRef: 'asgardeo-mgmt-client-secret' }
            { name: 'AsgardeoRoles__RiderRoleId', value: asgardeoRiderRoleId }
            { name: 'AsgardeoRoles__DriverRoleId', value: asgardeoDriverRoleId }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
  dependsOn: [
    kvRoleAssignment
    acrPullRoleAssignment
  ]
}

// ---------- Outputs ----------
output acrLoginServer string = acr.properties.loginServer
output keyVaultName string = keyVault.name
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
output containerAppName string = containerApp.name
output appIdentityPrincipalId string = appIdentity.properties.principalId
