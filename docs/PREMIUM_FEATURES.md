# Mark and protect Premium features

Use `FeatureCatalog` in Core to define each feature's tier and protected settings keys. Report file export, archive export/import execution, screenshot schedule saves and the entire CLI require Premium. Free includes up to three saved activity labels and three world clocks. Keep tier decisions out of individual views.

`CliRouter` checks the runtime-owned snapshot against `ProductFeature.Cli` before dispatching any command, including help, version, diagnostics and the interactive shell. It checks again before shell actions and watch refreshes. Free returns `cli.premium.required` with exit code `11`; unavailable or invalid access state fails closed. CLI arguments and settings cannot grant access. This is a client feature gate over the shared application services, not authentication for a separate public API; desktop Free features keep their own policies. All CLI entry points, including help/version, now require the runtime connection.

Wrap a feature's controls in the shared WinUI component:

```xml
<controls:FeatureGate Feature="ActivityLabels" Access="{Binding FeatureAccess}">
    <controls:ActivityLabelsEditor />
</controls:FeatureGate>
```

Supply the runtime snapshot and UI language from the owning view. `FeatureGate` renders a Premium badge and locked-state explanation only for Premium-classified features. Activity labels remain editable in Free, so their editor and selector have no Premium badge. Access is locked while the snapshot is unavailable. Compact surfaces can set `ShowTitle="False"` and `ShowHint="False"`.

For windows that allow Free users to configure or preview a Premium action, place one localized `PremiumBadge` in the title bar, not in the body. Keep the action enabled so it can open the standard upgrade dialog. This applies to screenshot scheduling, advanced report export and archive transfer. Archive previews and installation identity edits remain Free. The Add clock button has a badge beside it; the fourth Free addition opens the upgrade dialog.

`WorkTrailApplication.PatchSettingsAsync` checks `FeatureAccessPolicy` before validation and persistence. Both CLI settings commands and IPC calls reach this boundary. Hiding or disabling controls is presentation, not enforcement. Register new protected settings in the catalog; operations outside the settings path must use the same policy at their application boundary.

Creation quotas are checked inside the serialized mutation boundary against the complete validated settings change. Free can create three labels, rename/delete/select saved labels and use one-off taskbar text. Free cannot create a fourth label or clock. Downgrade preserves existing over-limit catalogs, active labels and recorded history; editing or reducing a catalog remains allowed, but further growth is blocked. Concurrent and IPC requests use the same guards.

Archive execution checks the current tier before writing files or pausing tracking. An import plan previewed in Premium cannot be committed after a downgrade. Screenshot schedule settings are protected in Core before persistence. Report configuration and preview remain Free; file creation requires Premium.

## Debug simulation

In a Debug build, open the player's main menu and choose **Debug: Free profile → simulate Premium** or **Debug: Premium profile → simulate Free**. The shared runtime owns the override and serializes it with settings mutations. Dashboard updates carry the access snapshot to connected views. The override is memory-only and resets when the runtime restarts.

The simulation method, IPC operation and menu item are compiled only with `DEBUG`. A client cannot enable simulation in a runtime built without that symbol. No settings key or persisted `IsPremium` flag grants access.

## Commercial license integration

`IFeatureLicenseSource` is the boundary for a trusted, verified entitlement. The current factory has no commercial source configured and therefore starts Free. Store product discovery, purchase, receipt verification and entitlement refresh are not implemented by this change. Connect a verified license source in the Core composition root before offering commercial Premium access; never trust a UI or settings value as a license.

## Verification

`FeatureAccessPolicyTests` and `FeatureCreationQuotaTests` cover protected keys, creation quotas, concurrent requests, IPC, loss of entitlement, invalid license tiers and Debug-only simulation. `PremiumUiContractTests` checks title-bar badges and navigation. Test execution requires the repository's explicit approval.
