using System.Text.RegularExpressions;

static class DeviceSelection {
    public static bool IsCable(string name) {
        return Regex.IsMatch(name, @"VB-Audio Virtual Cable|^CABLE Input(?:\s*\(|$)", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(name, @"16\s*Ch|Output", RegexOptions.IgnoreCase);
    }
    public static bool IsMicrophone(string name) {
        return Regex.IsMatch(name, "麦克风阵列|microphone array|array microphone", RegexOptions.IgnoreCase);
    }
}
