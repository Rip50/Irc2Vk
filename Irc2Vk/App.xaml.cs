using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VkNet;

namespace Irc2Vk
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        IrcListener _ircListener;
        VkBot _vkBot;
        private UsersActivitiesManager _userActivitiesManager;

        private void OnStartup(object sender, StartupEventArgs e)
        {
            ulong appID = 0;                                        // ID приложения
            string email = "0";                         // email или телефон
            string pass = "0";                                 // пароль для авторизации
            var settings = VkNet.Enums.Filters.Settings.Messages | VkNet.Enums.Filters.Settings.Friends;       // Приложение имеет доступ к друзьям

            var vk = new VkApi(new StandardCaptchaSolver());
            try
            {
                vk.Authorize(new ApiAuthParams
                {
                    ApplicationId = appID,
                    Login = email,
                    Password = pass,
                    Settings = settings
                    
                });
                _userActivitiesManager = new UsersActivitiesManager(new Dictionary<long, DateTime>(), new TimeSpan(6, 0, 0));
                
                _ircListener = new IrcListener("0", "0", "0");
                _vkBot = new VkBot(vk, _ircListener);
                _ircListener.UserBanned += _userActivitiesManager.RemoveUserActivity;
                _vkBot.RecivedMessageFromUid += _userActivitiesManager.RefreshUserActivity;
                _userActivitiesManager.UserAdded += _ircListener.CreateNewBot;
                _userActivitiesManager.UserRemoved += _ircListener.RemoveBot;
            } catch (VkNet.Exception.VkApiException exc)
            {
                System.Windows.MessageBox.Show($"Ошибка авторизации: {exc.Message}");
                App.Current.Shutdown();
            }
        }

        private void Debug(List<long> uids, List<string> messages)
        {
            var mw = MainWindow as Irc2Vk.MainWindow;
            if (mw == null) return;
            mw.Text = string.Join("\n", messages);
        }
    }
}
