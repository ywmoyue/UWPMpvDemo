using System.IO;
using Windows.Storage;

namespace UWPMpvDemo
{
    public class LibMpv
    {
        public static string mpvPath = Path.Combine(ApplicationData.Current.LocalFolder.Path,
            "libmpv-2.dll");

        public static string tempVideo = Path.Combine(ApplicationData.Current.LocalFolder.Path ,
                                                      "test.mkv");
    }
}

