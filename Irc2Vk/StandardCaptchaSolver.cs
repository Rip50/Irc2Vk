using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using VkNet.Utils.AntiCaptcha;

namespace Irc2Vk
{
    class StandardCaptchaSolver : ICaptchaSolver
    {
        public string Solve(string url)
        {
            var res = Application.Current.Dispatcher.Invoke(() =>
            {
                var dlg = new CaptchaDialog();

                dlg.SetBitmap(
                    new JpegBitmapDecoder(
                        new MemoryStream(new WebClient().DownloadData(url)),
                        BitmapCreateOptions.None,
                        BitmapCacheOption.Default).Frames[0]);
                return dlg.ShowDialog().GetValueOrDefault(false) ? dlg.Text : "";
            });
            return res;
        }

        public void CaptchaIsFalse()
        {
        }
    }
}
