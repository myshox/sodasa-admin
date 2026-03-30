using System.Reflection;

namespace SQ_Email_Tools
{
    /// <summary>EXE 版號（與 SQ_Email_Tools.csproj 的 &lt;Version&gt; 一致）</summary>
    internal static class AppVersion
    {
        public static string DisplayShort
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                if (v == null) return "1.5.1";
                return $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }
    }
}
