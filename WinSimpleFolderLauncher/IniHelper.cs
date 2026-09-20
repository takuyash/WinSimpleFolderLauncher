
namespace WinSimpleFolderLauncherINI
{
    public static class IniHelper
    {
        public static Dictionary<string, string> ReadIni(string path)
        {
            var result = new Dictionary<string, string>();
            if (!File.Exists(path)) return result;

            foreach (var line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed.StartsWith(";") || trimmed.StartsWith("[")) continue;

                var kv = trimmed.Split(new char[] { '=' }, 2);
                if (kv.Length == 2)
                    result[kv[0].Trim()] = kv[1].Trim();
            }
            return result;
        }
    }
}
