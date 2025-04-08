using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace UWPMpvDemo
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MpvClient mpvClient;
        private MpvRender mpvRender;

        public MainPage()
        {
            this.InitializeComponent();
            InitializePlayer();
            DispatcherTimer timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            //_mpvPlayer.RenderFrame();
        }

        private void InitializePlayer()
        {
            mpvClient = new MpvClient();
            mpvClient.Initialize();
            mpvRender = new MpvRender(mpvClient);
            //mpvRender.Initialize(SwapChainPanel, LibMpv.tempVideo);
            mpvRender.Initialize(SwapChainPanel, LibMpv.tempVideoM4s, LibMpv.tempAudioM4s);
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (mpvClient.IsPaused())
                mpvClient.Play();
            else
                mpvClient.Pause();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            mpvRender.Dispose();
            mpvClient.Dispose();
        }
    }
}
