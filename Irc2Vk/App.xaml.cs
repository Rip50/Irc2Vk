using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
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

        private T LoadConfig<T>(string name) where T: struct
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(),"configs", $"{name}.json");
            if (File.Exists(path))
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            return new T();

        }

        private void CreateConfigIfNotExists<T>(string name) where T : struct
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "configs");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{name}.json");
            if (File.Exists(path)) return;
            using (var stream = File.Create(path))
            {
                var serialized = JsonConvert.SerializeObject(new T(), Formatting.Indented);
                using (var writer = new StreamWriter(stream)) 
                    writer.Write(serialized);
            }
        }


        private void SaveConfig<T>(string name, T config) where T : struct
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "configs");
            if(!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{name}.json");
            if (!File.Exists(path))
                File.Create(path);
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                CreateConfigIfNotExists<VkBotConfig>("vkbot");
                CreateConfigIfNotExists<ClientsConfig>("clients");
                CreateConfigIfNotExists<IrcConfig>("irc");

                var settings = VkNet.Enums.Filters.Settings.Messages | VkNet.Enums.Filters.Settings.Friends;
                // Приложение имеет доступ к друзьям

                var vk = new VkApi(new StandardCaptchaSolver());

                var vkParams = LoadConfig<VkBotConfig>("vkbot");
                var clientsParams = LoadConfig<ClientsConfig>("clients");

                try
                {
                    vk.Authorize(new ApiAuthParams
                    {
                        ApplicationId = vkParams.AppId,
                        Login = vkParams.Email,
                        Password = vkParams.Pass,
                        Settings = settings

                    });

                    var ircParams = LoadConfig<IrcConfig>("irc");
                    _ircListener = new IrcListener(ircParams, clientsParams);
                    _vkBot = new VkBot(vk, _ircListener);

                    _vkBot.Start();
                    _ircListener.Start();
                }
                catch (VkNet.Exception.VkApiException exc)
                {
                    System.Windows.MessageBox.Show($"Ошибка авторизации: {exc.Message}");
                    App.Current.Shutdown();
                }
            }
            catch (Exception exc)
            {
                System.Windows.MessageBox.Show($"Фатальная ошибка: {exc.Message}");
                App.Current.Shutdown();
            }
        }

        
    }
}
