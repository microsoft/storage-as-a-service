param location string
param appInsightsName string
param logAnalyticsWorkspaceId string

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
	name: format(appInsightsName, 'appi')
	location: location
	kind: 'workspace'
	properties: {
		WorkspaceResourceId: logAnalyticsWorkspaceId
		Application_Type: 'web'
		Flow_Type: 'Bluefield'
	}
}

output instrumentationKey string = appInsights.properties.InstrumentationKey
