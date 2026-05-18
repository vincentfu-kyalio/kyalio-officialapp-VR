namespace Kyalio.Models
{
    /// <summary>
    /// Public ID prefixes from the unified ID format: &lt;prefix&gt;_&lt;nanoid12&gt;.
    /// IDs are opaque to clients — these constants are for validation and clarity only.
    /// </summary>
    public static class IdPrefix
    {
        public const string Project       = "prj_";
        public const string Package       = "pkg_";
        public const string Program       = "prg_";
        public const string Specialty     = "spc_";
        public const string Role          = "rol_";
        public const string Video         = "vid_";
        public const string Image         = "img_";
        public const string Member        = "usr_";
        public const string Favorite      = "fav_";
        public const string WatchHistory  = "whs_";

        public const int TotalLength = 16; // prefix(3) + '_' + nanoid(12)
    }
}
