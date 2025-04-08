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

        public static string tempVideoM4s = Path.Combine(ApplicationData.Current.LocalFolder.Path,
            "video.m4s");

        public static string tempAudioM4s = Path.Combine(ApplicationData.Current.LocalFolder.Path,
            "audio.m4s");

        public static string tempMpd  = "file:///"+Path.Combine(ApplicationData.Current.LocalFolder.Path,
            "temp.mpd");
    }
}

