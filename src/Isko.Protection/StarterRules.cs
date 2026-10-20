using System;

namespace Isko.Protection
{
    // Original starter list written for Isko: hosts of well-known advertising networks only, no analytics,
    // CDNs, video hosts or site-specific rules. Network rules apply to third-party requests; a page's own
    // site is never blocked. Incomplete by design and unverified on a TV: see docs/protection.md.
    public static class StarterRules
    {
        public static readonly string Text = String.Join("\n", new[]
        {
            "! Isko starter list v1 (line numbers are rule ids in telemetry; append, do not reorder)",
            "||pubads.g.doubleclick.net^$third-party",
            "||pagead2.googlesyndication.com^$third-party",
            "||doubleclick.net^$third-party",
            "||googlesyndication.com^$third-party",
            "||googleadservices.com^$third-party",
            "||adnxs.com^$third-party",
            "||adsrvr.org^$third-party",
            "||rubiconproject.com^$third-party",
            "||pubmatic.com^$third-party",
            "||openx.net^$third-party",
            "||casalemedia.com^$third-party",
            "||criteo.com^$third-party",
            "||criteo.net^$third-party",
            "||adform.net^$third-party",
            "||smartadserver.com^$third-party",
            "||amazon-adsystem.com^$third-party",
            "||moatads.com^$third-party",
            "||taboola.com^$third-party",
            "||outbrain.com^$third-party",
            "||mgid.com^$third-party",
            "||revcontent.com^$third-party",
            "||popads.net^$third-party",
            "||popcash.net^$third-party",
            "||exoclick.com^$third-party",
            "||exosrv.com^$third-party",
            "||juicyads.com^$third-party",
            "||trafficjunky.net^$third-party",
            "||propellerads.com^$third-party",
            "||hilltopads.net^$third-party",
            "||clickadu.com^$third-party",
            "##.adsbygoogle",
            "##[data-ad-slot]",
        });
    }
}
