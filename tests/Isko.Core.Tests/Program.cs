using Isko.Core;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
var owner = new SingleViewOwner<object>();
var creates = 0;
var first = owner.Open(() => { creates++; return new object(); });
Check(ReferenceEquals(first, owner.Open(() => { creates++; return new object(); })) && creates == 1, "Repeated opens must reuse the single owned view");
Check(!owner.TryClose(_ => throw new Exception("native release failed")), "Release failure must be visible");
Check(owner.CleanupFailed && ReferenceEquals(first, owner.Current), "Failed release must retain ownership");
var rejected = false;
try { owner.Open(() => { creates++; return new object(); }); } catch (InvalidOperationException) { rejected = true; }
Check(rejected && creates == 1, "Failed cleanup must block even a replacement attempt");
Check(owner.TryClose(view => Check(ReferenceEquals(first, view), "Retry must release the original view")), "Retry must recover");
Check(owner.Current == null && !owner.CleanupFailed, "Successful release must clear ownership");
var second = owner.Open(() => { creates++; return new object(); });
Check(!ReferenceEquals(first, second) && creates == 2, "Reopen after close must create a fresh view");
Check(owner.TryClose(_ => {
    Check(!owner.TryClose(_ => throw new Exception()), "Reentrant close must not release twice");
    var blocked = false;
    try { owner.Open(() => new object()); } catch (InvalidOperationException) { blocked = true; }
    Check(blocked, "Reentrant open must not leak a new view during disposal");
}), "Close after reentrant attempts must succeed");
var failedFactory = false;
try { owner.Open(() => throw new Exception("engine unavailable")); } catch { failedFactory = true; }
Check(failedFactory && owner.Current == null && !owner.CleanupFailed, "Failed creation must not retain a phantom view");
for (var i = 0; i < 100; i++) {
    owner.Open(() => new object());
    Check(owner.TryClose(_ => { }), "Repeated hide/close must release ownership");
}
Console.WriteLine("PASS single-view ownership: reuse, failed cleanup, retry, reentrancy, failed creation, 100 close/reopen cycles (no native engine execution)");

var chrome = new ChromeState();
Check(chrome.Surface == BrowserSurface.Home && chrome.Overlay == ChromeOverlay.None && !chrome.PageInteractive, "Start at home launcher");
Check(!chrome.Show(ChromeOverlay.Controls) && chrome.Overlay == ChromeOverlay.None, "Home has no page controls bar");
chrome.Show(ChromeOverlay.Menu);
Check(chrome.Dismiss() && chrome.Overlay == ChromeOverlay.None, "Back from home drawer returns to tiles");
chrome.PageOpened();
Check(chrome.PageInteractive, "An opened page owns the screen and remote");
chrome.Show(ChromeOverlay.Controls); chrome.Show(ChromeOverlay.Menu);
Check(chrome.Surface == BrowserSurface.Page && chrome.MenuOpen && !chrome.PageInteractive, "Menu must preserve the playing page");
Check(chrome.Dismiss() && chrome.Overlay == ChromeOverlay.Controls, "Back from drawer returns to the page bar");
Check(chrome.Dismiss() && chrome.PageInteractive, "Back from the bar returns to the page without closing it");
Check(!chrome.Dismiss(), "No overlay does not consume another Back");
chrome.Show(ChromeOverlay.Address);
Check(chrome.CloseAll() && chrome.PageInteractive, "Fullscreen video clears every overlay");
chrome.Show(ChromeOverlay.Menu); chrome.PageClosed();
Check(chrome.Surface == BrowserSurface.Home && chrome.Overlay == ChromeOverlay.None, "Hide/close resets both page and drawer");
var regions = new[] {
    new FocusRegion(100, 0, 600, 64), new FocusRegion(720, 0, 160, 64),
    new FocusRegion(100, 200, 320, 100), new FocusRegion(440, 200, 320, 100)
};
Check(DirectionalFocus.Next(regions, 0, 1, 0) == 1, "Right stays in the address lane");
Check(DirectionalFocus.Next(regions, 1, -1, 0) == 0, "Left returns to address");
Check(DirectionalFocus.Next(regions, 0, 0, 1) == 2, "Down reaches the aligned home card");
Check(DirectionalFocus.Next(regions, 2, 1, 0) == 3, "Right moves between cards");
Check(DirectionalFocus.Next(regions, 2, 0, -1) == 0, "Up returns from card to address");
Check(DirectionalFocus.Next(regions, 2, -1, 0) == 2, "No wrap outside a row");
Check(DirectionalFocus.Next(Array.Empty<FocusRegion>(), -1, 0, 1) == -1, "Empty focus set has no target");
Console.WriteLine("PASS shell state and directional remote navigation (portable model, no native input execution)");

var cursor = new PageCursor();
cursor.Reset(0);
Check(cursor.Visible(0) && cursor.X == 960 && cursor.Y == 540, "A new page shows the cursor at the centre");
Check(cursor.Move(1, 0, 100) == (0, 0) && cursor.X > 960 && cursor.X <= 1000, "A single press moves a precise, visible step");
var firstStep = cursor.X - 960;
var beforeRepeat = cursor.X; cursor.Move(1, 0, 400);
Check(cursor.X - beforeRepeat > firstStep, "Remote repeats 300ms apart still accelerate");
for (var t = 450; t < 2000; t += 50) cursor.Move(1, 0, t);
Check(cursor.X == 1919, "The cursor stops at the right edge");
Check(cursor.Move(1, 0, 2000).X == 3, "Holding against an edge scrolls, faster while held");
Check(cursor.Move(0, 1, 2600) == (0, 0) && cursor.Y == 540 + firstStep, "A new direction restarts the precise step");
Check(cursor.Activate(2700), "OK on a shown cursor clicks");
Check(!cursor.Visible(2700 + PageCursor.IdleMs), "The cursor hides after idling");
var hiddenX = cursor.X; var hiddenY = cursor.Y;
Check(cursor.Move(-1, 0, 9000) == (0, 0) && cursor.X == hiddenX && cursor.Visible(9000), "The first press after idle only reveals it");
cursor.Hide();
Check(!cursor.Activate(9100) && cursor.Y == hiddenY && cursor.Visible(9100), "OK on a hidden cursor reveals instead of clicking");
cursor.Reset(9200); cursor.Move(0, -1, 9300);
for (var t = 9350; t < 11000; t += 50) cursor.Move(0, -1, t);
Check(cursor.Y == 0 && cursor.Move(0, -1, 11000).Y == -3, "Pushing up at the top scrolls up");
Console.WriteLine("PASS page cursor: acceleration, edges, edge scrolling, idle reveal (portable model, no native input execution)");
Check(StreamUrls.IsManifest("https://cdn.example.net/live/master.m3u8?token=abc") && StreamUrls.IsManifest("http://x.example/a/b/index.M3U8") && StreamUrls.IsManifest("https://x.example/manifest.mpd?x=1"), "HLS and DASH manifests are recognised");
Check(!StreamUrls.IsManifest("https://x.example/seg_001.ts") && !StreamUrls.IsManifest("https://x.example/page?u=a.m3u8") && !StreamUrls.IsManifest("ftp://x.example/a.m3u8") && !StreamUrls.IsManifest(null) && !StreamUrls.IsManifest(""), "segments, query-only mentions and other schemes are not manifests");
Check(!StreamUrls.IsManifest("https://x.example/" + new string('a', 5000) + ".m3u8"), "oversized addresses are refused");
Check(StreamUrls.Describe("https://cdn.example.net/a.m3u8?token=SECRET") == "cdn.example.net" && StreamUrls.Describe("nope") == "[invalid]", "diagnostics keep only the host");
Check(StreamUrls.IsMediaSegment("https://cdn.example.net/live/seg_00012.ts?t=1") && StreamUrls.IsMediaSegment("https://x.example/a/chunk.m4s") && StreamUrls.IsMediaSegment("https://x.example/v/audio.AAC"), "media segments are recognised");
Check(!StreamUrls.IsMediaSegment("https://x.example/master.m3u8") && !StreamUrls.IsMediaSegment("https://x.example/app.js") && !StreamUrls.IsMediaSegment("https://x.example/a.ts.js") && !StreamUrls.IsMediaSegment(null), "manifests, scripts and look-alikes are not segments");
Console.WriteLine("PASS stream addresses: manifests recognised, segments and look-alikes rejected, host-only description (portable)");

var inputBuffer = new PageInputBuffer();
inputBuffer.Scroll(0.5f, 0.5f, 0, 0.1f);
Check(inputBuffer.TryTake(out var inputAction) && inputAction.Kind == PageInputKind.Scroll, "The first wheel operation starts");
var oldTicket = inputBuffer.Ticket;
for (var i = 0; i < 1000; i++) inputBuffer.Scroll(0.6f, 0.7f, 0, 0.1f);
Check(!inputBuffer.TryTake(out _), "A slow native callback keeps exactly one script in flight");
inputBuffer.Complete(oldTicket);
Check(inputBuffer.TryTake(out inputAction) && inputAction.ScrollY == 0.5f && inputAction.X == 0.6f, "A long hold coalesces into a bounded scroll at the latest position");
Check(!inputBuffer.Complete(oldTicket), "A duplicate completion cannot free the next operation");
inputBuffer.Complete(inputBuffer.Ticket);
Check(!inputBuffer.TryTake(out _), "There is no backlog of 1000 wheel scripts");
inputBuffer.Scroll(0.5f, 0.5f, 0, 0.3f); inputBuffer.Click(0.2f, 0.3f);
Check(inputBuffer.TryTake(out inputAction) && inputAction.Kind == PageInputKind.Click, "A new click drops old pending wheel input");
inputBuffer.Click(0.4f, 0.5f); inputBuffer.Click(0.7f, 0.8f);
inputBuffer.Complete(inputBuffer.Ticket);
Check(inputBuffer.TryTake(out inputAction) && inputAction.X == 0.7f, "Rapid pending clicks retain the latest location");
inputBuffer.Reset(); inputBuffer.Click(0.1f, 0.1f); inputBuffer.TryTake(out _);
Check(!inputBuffer.Complete(oldTicket) && !inputBuffer.TryTake(out _), "A late callback from the old document cannot complete new work");
inputBuffer.Complete(inputBuffer.Ticket);
Check(!inputBuffer.HasWork, "The input timer can stop when the current operation completes");
inputBuffer.Click(0.5f, 0.5f); inputBuffer.TryTake(out _);
var suspendedTicket = inputBuffer.Ticket;
inputBuffer.Scroll(0.5f, 0.5f, 0, 0.1f); inputBuffer.DiscardPending();
Check(inputBuffer.HasWork && !inputBuffer.TryTake(out _), "Opening chrome must not free an outstanding native call");
Check(inputBuffer.Complete(suspendedTicket) && !inputBuffer.HasWork, "A late reply releases input after queued overlay input was discarded");
Console.WriteLine("PASS bounded page input: slow callback, coalescing, new click, document lifetime (portable model)");

// Static metadata inspection only. Never load reference/native assemblies for execution.
// The published reference DLL exposes hidden symbols too, so compilation alone is insufficient.
if (args.Length == 1) {
    using var stream = File.OpenRead(args[0]);
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    var assemblies = metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name)).ToArray();
    Check(assemblies.Contains("Tizen.NUI"), "Modern build must use NUI");
    Check(!assemblies.Contains("Tizen.WebView") && !assemblies.Contains("ElmSharp"), "Modern build must not reference legacy engine/UI assemblies");
    var used = new HashSet<string>();
    foreach (var handle in metadata.MemberReferences) {
        var member = metadata.GetMemberReference(handle);
        if (member.Parent.Kind != HandleKind.TypeReference) continue;
        var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (metadata.GetString(type.Name) == "WebView") used.Add(metadata.GetString(member.Name));
    }
    Check(used.Contains("LoadUrl") && used.Contains("get_Url"), "Audit must inspect an actual compiled browser backend");
    Console.WriteLine("PASS API12 assembly audit: NUI only; no legacy engine assemblies");
}

// Hidden-API review gate. EditorBrowsable(Never) members are allowed when Samsung's SDK lets an
// ordinary app use them: present in the API12 reference and documenting no privilege beyond the
// manifest's internet. Each one used must be listed here after that review; a new one fails the
// test until reviewed. Signatures are compared exactly, so a hidden overload does not taint a
// public one. Reference assemblies come from the restored NuGet package.
if (args.Length == 1) {
    var reviewed = new HashSet<string> {
        // UI: icon tint and image fitting.
        "Tizen.NUI.BaseComponents.View::get_Color()",
        "Tizen.NUI.BaseComponents.View::set_Color(Tizen.NUI.Color)",
        "Tizen.NUI.BaseComponents.ImageView::set_FittingMode(Tizen.NUI.FittingModeType)",
        // UI: decode the 1254px logo at display size.
        "Tizen.NUI.BaseComponents.ImageView::set_DesiredWidth(Int32)",
        "Tizen.NUI.BaseComponents.ImageView::set_DesiredHeight(Int32)",
        // Engine: popup/redirect policy.
        "Tizen.NUI.BaseComponents.WebView::add_NavigationPolicyDecided(System.EventHandler<Tizen.NUI.WebViewPolicyDecidedEventArgs>)",
        "Tizen.NUI.BaseComponents.WebView::remove_NavigationPolicyDecided(System.EventHandler<Tizen.NUI.WebViewPolicyDecidedEventArgs>)",
        "Tizen.NUI.BaseComponents.WebView::add_NewWindowPolicyDecided(System.EventHandler<Tizen.NUI.WebViewPolicyDecidedEventArgs>)",
        "Tizen.NUI.BaseComponents.WebView::remove_NewWindowPolicyDecided(System.EventHandler<Tizen.NUI.WebViewPolicyDecidedEventArgs>)",
        "Tizen.NUI.WebViewPolicyDecidedEventArgs", "Tizen.NUI.WebViewPolicyDecidedEventArgs::get_PolicyDecisionMaker()",
        "Tizen.NUI.WebPolicyDecisionMaker", "Tizen.NUI.WebPolicyDecisionMaker::get_Url()",
        "Tizen.NUI.WebPolicyDecisionMaker::Use()", "Tizen.NUI.WebPolicyDecisionMaker::Ignore()",
        // Engine: refuse downloads. Reviewed against the TizenFX API12 source: no privilege annotation on any of these.
        "Tizen.NUI.BaseComponents.WebView::add_ResponsePolicyDecided(System.EventHandler<Tizen.NUI.WebViewPolicyDecidedEventArgs>)",
        "Tizen.NUI.BaseComponents.WebView::remove_ResponsePolicyDecided(System.EventHandler<Tizen.NUI.WebViewPolicyDecidedEventArgs>)",
        "Tizen.NUI.WebPolicyDecisionMaker+DecisionType", "Tizen.NUI.WebPolicyDecisionMaker::get_PolicyDecisionType()",
        "Tizen.NUI.WebPolicyDecisionMaker::get_ResponseMime()",
        "Tizen.NUI.WebContext+DownloadStartedCallback",
        "Tizen.NUI.WebContext::RegisterDownloadStartedCallback(Tizen.NUI.WebContext+DownloadStartedCallback)",
        // Engine: request filtering.
        "Tizen.NUI.BaseComponents.WebView::get_Context()", "Tizen.NUI.WebContext",
        "Tizen.NUI.WebContext+HttpRequestInterceptedCallback",
        "Tizen.NUI.WebContext::RegisterHttpRequestInterceptedCallback(Tizen.NUI.WebContext+HttpRequestInterceptedCallback)",
        "Tizen.NUI.WebHttpRequestInterceptor", "Tizen.NUI.WebHttpRequestInterceptor::get_Url()",
        "Tizen.NUI.WebHttpRequestInterceptor::Ignore()", "Tizen.NUI.WebHttpRequestInterceptor::SetResponse(String,Byte[])",
        // Engine: history, page scripts and HTML fullscreen.
        "Tizen.NUI.BaseComponents.WebView::CanGoBack()", "Tizen.NUI.BaseComponents.WebView::GoBack()",
        "Tizen.NUI.BaseComponents.WebView::EvaluateJavaScript(String)",
        "Tizen.NUI.BaseComponents.WebView::EvaluateJavaScript(String,Tizen.NUI.BaseComponents.WebView+JavaScriptMessageHandler)",
        "Tizen.NUI.BaseComponents.WebView+JavaScriptMessageHandler",
        "Tizen.NUI.BaseComponents.WebView::ExitFullscreen()",
        // Engine: release rendered tiles and unused engine memory when a page closes. API12 reference docs
        // list no privilege for either member; only the manifest's internet applies.
        "Tizen.NUI.BaseComponents.WebView::ClearAllTilesResources()",
        "Tizen.NUI.WebContext::FreeUnusedMemory()",
        // API12 video path switch: public SDK property, no additional privilege documented.
        "Tizen.NUI.BaseComponents.WebView::get_VideoHoleEnabled()",
        "Tizen.NUI.BaseComponents.WebView::set_VideoHoleEnabled(Boolean)",
        // API12: development-only inspector, internet already declared; absent in normal builds.
        "Tizen.NUI.BaseComponents.WebView::StartInspectorServer(UInt32)",
        "Tizen.NUI.BaseComponents.WebView::StopInspectorServer()",
        "Tizen.NUI.BaseComponents.WebView::add_FullscreenEntered(System.EventHandler<System.EventArgs>)",
        "Tizen.NUI.BaseComponents.WebView::remove_FullscreenEntered(System.EventHandler<System.EventArgs>)",
        "Tizen.NUI.BaseComponents.WebView::add_FullscreenExited(System.EventHandler<System.EventArgs>)",
        "Tizen.NUI.BaseComponents.WebView::remove_FullscreenExited(System.EventHandler<System.EventArgs>)",
    };
    var packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    var refs = Path.Combine(packages, "tizen.net.api12", "12.0.0.18510", "ref", "net6.0");
    Check(Directory.Exists(refs), "API12 reference assemblies not found at " + refs);
    var hidden = new HashSet<string>(); var hiddenTypes = new HashSet<string>();
    foreach (var file in Directory.GetFiles(refs, "Tizen*.dll")) {
        using var refStream = File.OpenRead(file);
        using var refPe = new PEReader(refStream);
        var md = refPe.GetMetadataReader();
        foreach (var th in md.TypeDefinitions) {
            var type = md.GetTypeDefinition(th); var name = Audit.DefName(md, th);
            if (Audit.Hidden(md, type.GetCustomAttributes())) hiddenTypes.Add(name);
            foreach (var mh in type.GetMethods()) {
                var m = md.GetMethodDefinition(mh);
                if (Audit.Hidden(md, m.GetCustomAttributes())) hidden.Add(name + "::" + md.GetString(m.Name) + Audit.Sig(md, m.Signature));
            }
            foreach (var ph in type.GetProperties()) {
                var p = md.GetPropertyDefinition(ph); if (!Audit.Hidden(md, p.GetCustomAttributes())) continue;
                foreach (var a in new[] { p.GetAccessors().Getter, p.GetAccessors().Setter }) if (!a.IsNil) {
                    var m = md.GetMethodDefinition(a); hidden.Add(name + "::" + md.GetString(m.Name) + Audit.Sig(md, m.Signature)); }
            }
            foreach (var eh in type.GetEvents()) {
                var e = md.GetEventDefinition(eh); if (!Audit.Hidden(md, e.GetCustomAttributes())) continue;
                foreach (var a in new[] { e.GetAccessors().Adder, e.GetAccessors().Remover }) if (!a.IsNil) {
                    var m = md.GetMethodDefinition(a); hidden.Add(name + "::" + md.GetString(m.Name) + Audit.Sig(md, m.Signature)); }
            }
        }
    }
    using var appStream = File.OpenRead(args[0]);
    using var appPe = new PEReader(appStream);
    var app = appPe.GetMetadataReader();
    var violations = new SortedSet<string>(); var usedHidden = new SortedSet<string>(); var inspected = 0;
    foreach (var handle in app.MemberReferences) {
        var member = app.GetMemberReference(handle);
        if (member.Parent.Kind != HandleKind.TypeReference) continue;
        var declaring = Audit.RefName(app, (TypeReferenceHandle)member.Parent);
        if (!declaring.StartsWith("Tizen.")) continue;
        inspected++;
        var key = declaring + "::" + app.GetString(member.Name) + Audit.Sig(app, member.Signature);
        if (hidden.Contains(key)) { usedHidden.Add(key); if (!reviewed.Contains(key)) violations.Add(key); }
    }
    foreach (var th in app.TypeReferences) {
        var name = Audit.RefName(app, th);
        if (hiddenTypes.Contains(name)) { usedHidden.Add(name); if (!reviewed.Contains(name)) violations.Add(name); }
    }
    Check(inspected > 50, "Hidden-API audit must inspect the compiled shell");
    Check(violations.Count == 0, "Unreviewed hidden API12 members referenced: " + string.Join(", ", violations));
    Console.WriteLine($"PASS API12 hidden-API review: {inspected} Tizen member references, {usedHidden.Count} hidden, all reviewed");
}

static class Audit
{
    public static string DefName(MetadataReader md, TypeDefinitionHandle h)
    {
        var t = md.GetTypeDefinition(h); var d = t.GetDeclaringType();
        return d.IsNil ? md.GetString(t.Namespace) + "." + md.GetString(t.Name) : DefName(md, d) + "+" + md.GetString(t.Name);
    }
    public static string RefName(MetadataReader md, TypeReferenceHandle h)
    {
        var t = md.GetTypeReference(h);
        return t.ResolutionScope.Kind == HandleKind.TypeReference
            ? RefName(md, (TypeReferenceHandle)t.ResolutionScope) + "+" + md.GetString(t.Name)
            : md.GetString(t.Namespace) + "." + md.GetString(t.Name);
    }
    public static string Sig(MetadataReader md, BlobHandle blob)
    {
        var reader = md.GetBlobReader(blob);
        if (reader.ReadSignatureHeader().Kind == SignatureKind.Field) return "";
        reader = md.GetBlobReader(blob);
        var signature = new SignatureDecoder<string, object?>(new Names(), md, null).DecodeMethodSignature(ref reader);
        return "(" + string.Join(",", signature.ParameterTypes) + ")";
    }
    public static bool Hidden(MetadataReader md, CustomAttributeHandleCollection attributes)
    {
        foreach (var h in attributes) {
            var attribute = md.GetCustomAttribute(h);
            var type = attribute.Constructor.Kind switch {
                HandleKind.MemberReference => md.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
                HandleKind.MethodDefinition => (EntityHandle)md.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
                _ => default
            };
            var name = type.Kind == HandleKind.TypeReference ? md.GetString(md.GetTypeReference((TypeReferenceHandle)type).Name)
                : type.Kind == HandleKind.TypeDefinition ? md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)type).Name) : "";
            if (name != "EditorBrowsableAttribute") continue;
            var value = md.GetBlobReader(attribute.Value); value.ReadUInt16();
            if (value.ReadInt32() == 1) return true; // EditorBrowsableState.Never
        }
        return false;
    }
    private sealed class Names : ISignatureTypeProvider<string, object?>
    {
        public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
        public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => DefName(r, h);
        public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => RefName(r, h);
        public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) => "spec";
        public string GetSZArrayType(string e) => e + "[]";
        public string GetArrayType(string e, ArrayShape s) => e + "[,]";
        public string GetPointerType(string e) => e + "*";
        public string GetByReferenceType(string e) => e + "&";
        public string GetGenericInstantiation(string g, ImmutableArray<string> a) => g.Split('`')[0] + "<" + string.Join(",", a) + ">";
        public string GetGenericTypeParameter(object? c, int i) => "!" + i;
        public string GetGenericMethodParameter(object? c, int i) => "!!" + i;
        public string GetFunctionPointerType(MethodSignature<string> s) => "fn";
        public string GetModifiedType(string m, string u, bool r) => u;
        public string GetPinnedType(string e) => e;
    }
}
