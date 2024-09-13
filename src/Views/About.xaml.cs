using System.Diagnostics;
using System.Windows;

namespace YAU.Views
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
            PublishDate();
            versionLbl.Content = "v" + Properties.Resources.Version;
            releaseTypeLbl.Content = Properties.Resources.ReleaseType + " Release";
        }

        private void PublishDate()
        {
            string dateTime = Properties.Resources.BuildDate;
            dateTime = dateTime.Remove(dateTime.Length - 9);
            dateLbl.Content = dateTime;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        private void btnChangelog_Click(object sender, RoutedEventArgs e)
        {
            var startInfo = new ProcessStartInfo("cmd", $"/c start https://github.com/Harmless05/YAU/releases/latest")
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(startInfo);
        }

        private void btnLicense_Click(object sender, RoutedEventArgs e)
        {
            var startInfo = new ProcessStartInfo("cmd", $"/c start https://github.com/Harmless05/YAU/blob/main/LICENSE")
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(startInfo);
        }

        private void btnGithub_Click(object sender, RoutedEventArgs e)
        {
            var startInfo = new ProcessStartInfo("cmd", $"/c start https://github.com/Harmless05/YAU")
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(startInfo);
        }
    }
}
