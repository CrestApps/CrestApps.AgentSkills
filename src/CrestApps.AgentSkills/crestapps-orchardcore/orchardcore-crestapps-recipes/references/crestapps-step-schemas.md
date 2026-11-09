# CrestApps Step Schemas

CrestApps Recipes registers schemas for the CrestApps modules' own recipe steps. Each is gated on the feature that provides the step, so tooling only suggests steps the tenant can import. The schemas attach descriptions to known properties.

## AI

| Feature gate | Step schemas |
|---|---|
| `CrestApps.OrchardCore.AI` | `AIProfile`, `CreateAIProfileFromTemplate`, `AIProfileTemplate`, `AIDeployment`, `DeleteAIDeployments`, `AIProviderConnections`; Settings contributions `GeneralAISettings`, `DefaultAIDeploymentSettings` |
| `CrestApps.OrchardCore.AI.DataSources` | `AIDataSource` (its `Source` suggestions come from the registered `AIDataSourceSourceOptions`; imports default to `SearchIndexProfile` when omitted) |
| `CrestApps.OrchardCore.AI.ToolInstances` | `AIToolInstances` |
| `CrestApps.OrchardCore.AI.Personas` | `AIPersonas`; with `OrchardCore.Deployment` also the `AIPersonaDeploymentStep` deployment step (`IncludeAll`, `PersonaIds`); the `AIProfile` schema then describes `PersonaId` and `AIPersonaMetadata` |
| `CrestApps.OrchardCore.AI.Mcp` | `McpConnection` |
| `CrestApps.OrchardCore.AI.Mcp.Server` | `McpPrompt`, `McpResource` |
| `CrestApps.OrchardCore.AI.A2A` | `A2AConnection` |
| `OrchardCore.Workflows` + `CrestApps.OrchardCore.AI` | Workflow schemas for the AI chat session events and the `AICompletionFromProfileTask` and `AICompletionWithConfigTask` tasks |

`AIProfile` and `CreateAIProfileFromTemplate` enumerate known `Properties` entries (such as `AIProfileMetadata`, `FunctionInvocationMetadata`, `AgentInvocationMetadata`, `PromptTemplateMetadata`, `AnalyticsMetadata`, `DataSourceMetadata`, `AIDataSourceRagMetadata`, `DocumentsMetadata`, `MemoryMetadata`, `ClaudeSessionMetadata`, `CopilotSessionMetadata`, `AIProfileMcpMetadata`, `AIProfileA2AMetadata`) and `Settings` entries (such as `AIProfileSettings`, `ResponseHandlerProfileSettings`, `AIChatProfileSettings`, `AIProfileDataExtractionSettings`, `AIProfilePostSessionSettings`, `ChatModeProfileSettings`) while allowing extra keys. `AIProfileTemplate` adds `ProfileTemplateMetadata` and `SystemPromptTemplateMetadata`. Connection steps document metadata envelopes such as `SseMcpConnectionMetadata`, `StdioMcpConnectionMetadata`, `FtpConnectionMetadata`, `SftpConnectionMetadata` and `A2AConnectionMetadata`.

`CreateAIProfileFromTemplate` creates a profile from a template whose source is `Profile`, then applies the fields present in the recipe; omitted fields keep the template's values. A missing template, or one with another source, records a recipe error and skips the item.

```json
{
  "steps": [
    {
      "name": "CreateAIProfileFromTemplate",
      "Profiles": [
        {
          "TemplateId": "customer-support",
          "Name": "customer-support-prod",
          "DisplayText": "Customer Support",
          "ChatDeploymentName": "gpt-4o"
        }
      ]
    }
  ]
}
```

`AIProfileTemplate` entries need `Name`, `DisplayText` and `Source` (`Profile` or `SystemPrompt`). Without an `ItemId`, an existing template is matched by `Name` and `Source`. See the `orchardcore-ai-profile-templates` and `orchardcore-ai` skills for the AI step payloads themselves.

AI chat, documents, data sources, memory, Copilot and Claude features also register their site settings contributions (for example `AIChatAdminWidgetSettings`, `InteractionDocumentSettings`, `AIDataSourceSettings`, `ChatInteractionChatModeSettings`, `ChatInteractionMemorySettings`, `AIMemorySettings`, `CopilotSettings`, `ClaudeSettings`, `DefaultOrchestratorSettings`).

## Contact Center, Omnichannel and phone system

| Feature gate | Step schemas |
|---|---|
| `CrestApps.OrchardCore.ContactCenter` | `ContactCenterVoiceMedia`; Settings `ContactCenterExternalTransferSettings` |
| `CrestApps.OrchardCore.ContactCenter.Queues` | `ContactCenterSkill`, `ContactCenterQueueGroup`, `ContactCenterQueue` |
| `CrestApps.OrchardCore.ContactCenter.BusinessHours` | `ContactCenterBusinessHoursCalendar` |
| `CrestApps.OrchardCore.ContactCenter.AgentEntitlements` | `ContactCenterAgentEntitlement` |
| `CrestApps.OrchardCore.ContactCenter.Agents` | `AgentStateReasonCode` |
| `CrestApps.OrchardCore.ContactCenter.InboundVoice` | `ContactCenterEntryPoint` |
| `CrestApps.OrchardCore.ContactCenter.Dialer` | `ContactCenterDialerProfile` |
| `...ContactCenter.Recording`, `...ContactCenter.SecureCapture` | Settings `ContactCenterRecordingSettings`, `SecureCaptureSettings` |
| `CrestApps.OrchardCore.Omnichannel.Activities` | `OmnichannelCampaignGroup`, `OmnichannelCampaign`, `OmnichannelChannelEndpoint`, `OmnichannelDisposition`, `OmnichannelCadence`, `OmnichannelSubjectAction` |
| `CrestApps.OrchardCore.Omnichannel.Crm` | `OmnichannelLeadStatus`, `OmnichannelOpportunityStage` |
| `CrestApps.OrchardCore.Omnichannel.Messaging` | `OmnichannelMessageTemplate` |
| `CrestApps.OrchardCore.Telephony` | `TelephonyExtension`; Settings `TelephonySettings`; the soft phone feature adds `SoftPhoneWidgetSettings` |
| `CrestApps.OrchardCore.Telnyx`, `CrestApps.OrchardCore.Telnyx.Sms` | `TelnyxSettings`, `TelnyxSmsSettings`, plus Settings contributions of the same names |
| `CrestApps.OrchardCore.Asterisk` | `AsteriskSettings`, plus a Settings contribution of the same name |
| `OrchardCore.Workflows` + `CrestApps.OrchardCore.ContactCenter` | Workflow schemas for `ContactCenterEvent`, `EnqueueActivityTask`, `ScheduleCallbackTask`, `SetAgentPresenceTask`, `StartCallRecordingTask`, `StartOmnichannelActivityTask`, `StopCallRecordingTask`, `TransferToAgentTask` |

Provider settings steps take secrets in clear text, protect them on import, never export them, and keep the stored secret when a recipe omits it. Their `Settings` step contributions describe the generic `Settings` step, which stores values as given, so a secret there must already be protected for the destination tenant.

## Other CrestApps modules

| Feature gate | Step schemas |
|---|---|
| `CrestApps.OrchardCore.Users` + `OrchardCore.Users` | `IndexUsers` |
| `CrestApps.OrchardCore.Users.DisplayName` | Settings `DisplayNameSettings` |
| `CrestApps.OrchardCore.Users.Avatars` | Settings `UserAvatarOptions` |
| `CrestApps.OrchardCore.TimeZones` | `TimeZoneMaps` |
| `CrestApps.OrchardCore.DncRegistry` (and `.UsaFtc`, `.CanadaDncl`) | Settings `DncRegistrySettings`, `UsaFtcDncRegistrySettings`, `CanadaDnclRegistrySettings` |
| `CrestApps.OrchardCore.Products` | `Currencies` |
| `CrestApps.OrchardCore.Customers` | `Customers` |
| `CrestApps.OrchardCore.Inventory` | `Stock` |
| `CrestApps.OrchardCore.Stripe` | `StripeSettings` |

Modules also register their own schemas from startups gated on `CrestApps.OrchardCore.Recipes` (for example Taxation registers `TaxCategory`, `TaxType`, `TaxJurisdiction`, `TaxRule`, `TaxTable` and the `TaxationPart` schema). Every module-owned schema is registered by the module that owns the step, from a startup that requires both `CrestApps.OrchardCore.Recipes` and the module's feature (plus `OrchardCore.Workflows` for activity schemas).

## Module-owned step schemas

Each of these steps has its own `<step>.schema.json` in the `orchardcore-recipes` skill's `references/recipe-schemas` folder. Read it before emitting the step; the owning skill explains what the records mean.

| Module (skill) | Step schemas |
|---|---|
| AI file sources and web crawlers (`orchardcore-ai-file-sources`, `orchardcore-ai-web-crawlers`) | `FileSources`, `WebCrawlers` |
| Accounts (`orchardcore-accounts`, `orchardcore-accounts-b2b`) | `Accounts`, `PriceLists` |
| Calendars, Scheduling, Bookings (`orchardcore-calendars`, `orchardcore-scheduling`, `orchardcore-bookings`) | `Calendars`, `Scheduling`, `BookingDates` |
| Skills, Teams (`orchardcore-skills`, `orchardcore-teams`) | `Skills`, `Teams` |
| Workforce (`orchardcore-workforce`, `orchardcore-workforce-contact-center`) | `Workforce`, `WorkforceShiftPlanning`, `WorkforceTimeOff`, `WorkforceTimeClock`, `WorkforceLaborRules`, `WorkforceAdherence`, `WorkforceForecasting` |
| Time Tracker, Work Orders, Assets, Geo (`orchardcore-time-tracker`, `orchardcore-work-orders`, `orchardcore-assets`, `orchardcore-geo`) | `TimeTracker`, `WorkOrders`, `Assets`, `Geo` |
| Data Connectors (`orchardcore-data-connectors`) | `DataConnection`, `ConnectedDataSet` |
| Report Builder (`orchardcore-report-builder`) | `ReportDesigns` (+ deployment step) |
| Webhooks (`orchardcore-webhooks`) | `WebhookSubscriptions` (+ deployment step) |
| Omnichannel messaging and DNC (`orchardcore-omnichannel-messaging`, `orchardcore-dnc-registry`) | `MessagingBroadcasts`, `LocalDncLists` |
| Tenant hierarchy (`orchardcore-tenant-hierarchy`) | `TenantHierarchyAccessGrants` |
| Commerce (`orchardcore-markets`, `orchardcore-checkout`, `orchardcore-shipping`, `orchardcore-inventory`, `orchardcore-downloads`, `orchardcore-memberships`, `orchardcore-marketplace`, `orchardcore-marketing`, `orchardcore-customers`, `orchardcore-point-of-sale`, `orchardcore-taxation`) | `Markets`, `Coupons`, `ShippingZones`, `StockLocations`, `DigitalFiles`, `Memberships`, `Vendors`, `MarketingCampaigns`, `MarketingSegments`, `LoyaltyAccounts`, `StoreCreditAccounts`, `ReferralProfiles`, `Registers`, `MerchantTaxRegistration`, `ExemptionCertificate`, `TaxCategory`, `TaxType`, `TaxJurisdiction`, `TaxRule`, `TaxTable` |

Module-owned site settings, parts and fields appear in the composed `Settings` and `ContentDefinition` schemas rather than as their own files. Examples: AI cost, contact memory, MCP server and WebMCP settings; phone number verification providers; the commerce, Geo, map provider and Dispatch settings; and the `WebMcpPart`, `LeadPart`, `OpportunityPart`, `AccountPart`, `BookingPart`, `WizardPart`, `SchemaOrgPart` and report widget parts.

Behaviour to keep in mind when writing these steps:

- `TimeZoneMaps` and the other audited catalog steps ignore `OwnerId`, `Author`, `CreatedUtc` and `ModifiedUtc`; the importing site stamps its own audit values.
- `GeneralAISettings.EnableAnalytics` is a legacy alias that exported plans still carry; it is ignored. Usage tracking is the AI Usage Analytics feature.
- Several provider keys set through the generic `Settings` step (Google Maps, Azure Maps, Google Places, EasyPost, TaxJar) and the AzureSpeech, ElevenLabs and Cartesia deployment `ApiKey` are read back as Data Protection–protected values, so a clear key does not work there. Set them in the admin, or import a value protected for the destination tenant.

## Exporting reference schemas

The source repository's `utilities/CrestApps.OrchardCore.RecipeSchemaExporter` writes the composed schemas to files. It loads every `CrestApps.OrchardCore*` assembly in the CMS targets and instantiates every step, content, settings, deployment and workflow activity schema definition it finds, including module-owned and internal ones, so exported reference schemas match the runtime providers. Set `ORCHARDCORE_SOURCE_DIR` to an Orchard Core checkout so feature IDs are scanned from its manifests. Without a live tenant, example lists are empty. A guard test (`RecipeSchemaCoverageArchitectureTests`) fails the build when a recipe step handler or workflow activity has no schema.
