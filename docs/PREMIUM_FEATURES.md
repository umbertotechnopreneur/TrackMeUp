# Mark and protect Premium features

Use `FeatureCatalog` in Core to define each feature's tier and protected settings keys. Saved activity labels are Premium; the other declared features are Free. Keep tier decisions out of individual views.

Wrap a feature's controls in the shared WinUI component:

```xml
<controls:FeatureGate Feature="ActivityLabels" Access="{Binding FeatureAccess}">
    <controls:ActivityLabelsEditor />
</controls:FeatureGate>
```

Supply the runtime snapshot and UI language from the owning view. The component renders the localized title, the Premium badge and the locked-state explanation. The badge stays visible after access is granted. Access is locked while the snapshot is unavailable. Compact surfaces can set `ShowTitle="False"` and `ShowHint="False"`.

`TrackMeUpApplication.PatchSettingsAsync` checks `FeatureAccessPolicy` before validation and persistence. Both CLI settings commands and IPC calls reach this boundary. Hiding or disabling controls is presentation, not enforcement. Register new protected settings in the catalog; operations outside the settings path must use the same policy at their application boundary.

The label-selection guard covers both label IDs and selection of saved labels through the taskbar's text route. One-off taskbar text remains Free. Clearing selection is always allowed. Switching to Free clears a selected saved label without deleting definitions or changing recorded history.

## Debug simulation

In a Debug build, open the player's main menu and choose **Debug: Free profile → simulate Premium** or **Debug: Premium profile → simulate Free**. The shared runtime owns the override and serializes it with settings mutations. Dashboard updates carry the access snapshot to connected views. The override is memory-only and resets when the runtime restarts.

The simulation method, IPC operation and menu item are compiled only with `DEBUG`. A client cannot enable simulation in a runtime built without that symbol. No settings key or persisted `IsPremium` flag grants access.

## Commercial license integration

`IFeatureLicenseSource` is the boundary for a trusted, verified entitlement. The current factory has no commercial source configured and therefore starts Free. Store product discovery, purchase, receipt verification and entitlement refresh are not implemented by this change. Connect a verified license source in the Core composition root before offering commercial Premium access; never trust a UI or settings value as a license.

## Verification

`FeatureAccessPolicyTests` covers protected keys, the alternate taskbar selection route, clearing selection, loss of entitlement, invalid license tiers and Debug-only simulation. Test execution requires the repository's explicit approval. UI, IPC and restart scenarios are listed in `DEVELOPMENT_CHECKLIST.md`.
