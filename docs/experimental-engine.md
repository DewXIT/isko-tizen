# Reviewed engine hooks

The default browser directly uses API12 SDK members marked EditorBrowsable(Never)
when their reviewed declarations require no privilege beyond the manifest's
internet permission. Exact used signatures are audited in Isko.Core.Tests.
No reflection, P/Invoke or copied framework DLLs are used.

ProtectedPageEngine supplies popup/navigation policy, minimal HTTP interception,
JS evaluation, history and HTML fullscreen handling. The Lab project compiles the
same sources under a separate app ID for side-by-side development.

Request callbacks use immutable policy snapshots and complete each interceptor
once. Ignore continues a resource; SetResponse attempts the empty 403 block.
Off/exempt mode unregisters the interceptor. UI work is queued and invalidated
when a view/document is replaced. Native lifetime, effectiveness and privilege
availability still require device validation.

The two original host rules and optional fixture /ad-probe rule are not an external
filter list. Unknown gesture/navigation metadata is not classified as a malicious
redirect. Full-window layout is not HTML fullscreen. Bundled media observation is
passive; native destruction remains required for full resource release.

See [API audit](nui-api12.md), [protection](protection.md) and [testing](testing.md).
