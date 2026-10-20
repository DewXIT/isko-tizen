using System;

namespace Isko.Protection
{
    // A TV browser has nowhere useful to save a file, and ad pages use downloads (installers, archives)
    // as a lure. Downloads are always refused, independent of the protection switch and site exceptions.
    public static class DownloadPolicy
    {
        private static readonly string[] ArchiveOrExecutable =
        {
            "application/zip", "application/x-zip-compressed", "application/x-zip", "application/x-rar-compressed",
            "application/vnd.rar", "application/x-7z-compressed", "application/gzip", "application/x-gzip",
            "application/x-tar", "application/x-bzip2", "application/x-xz",
            "application/x-msdownload", "application/x-msdos-program", "application/x-msi",
            "application/vnd.microsoft.portable-executable", "application/x-dosexec", "application/x-executable",
            "application/x-apple-diskimage", "application/x-iso9660-image", "application/x-sh", "application/x-shellscript",
            "application/vnd.android.package-archive", "application/java-archive", "application/x-debian-package",
            "application/x-redhat-package-manager", "application/x-bat"
        };

        // engineSaysDownload: the engine already decided to save the response. A generic
        // application/octet-stream alone is not refused here, because a navigation response with that
        // type is the engine's call to make and refusing it blindly could hide a real document.
        public static bool Blocks(string responseMime, bool engineSaysDownload)
        {
            if (engineSaysDownload) return true;
            if (String.IsNullOrWhiteSpace(responseMime)) return false;
            int semicolon = responseMime.IndexOf(';');
            string mime = (semicolon >= 0 ? responseMime.Substring(0, semicolon) : responseMime).Trim().ToLowerInvariant();
            return Array.IndexOf(ArchiveOrExecutable, mime) >= 0;
        }
    }
}
