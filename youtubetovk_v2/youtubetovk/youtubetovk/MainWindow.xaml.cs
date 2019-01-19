using System;
using System.Windows;
using System.Windows.Navigation;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace youtubetovk
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //private string group_id = "133245931"; // ид группы.
        private string group_id = "81059900"; // ид группы.
        private int hours_count = 3; // через сколько часов планировать. начиная с 11.00 следующего дня.
        private int offset = 20; // с какого видео на youtube начинать.
        private int last = 5; // сколько роликов постить.
        private string video_name = "Видео группы [Ролики с yotube.com] https://vk.com/lolyotube"; //название видео
        private string description = "Видео группы [Ролики с yotube.com] https://vk.com/lolyotube Вступайте, репостите, ставьте лайк, комментируйте если видео вам понравилось. Помогите группе развиться!"; // описание видео
        private string message = "Репостите, ставьте лайк, комментируйте если видео вам понравилось. Помогите группе развиться!"; //Текст поста.
        private int day = 1; //на какой день после сегодня постить.
        private string access_token = "";
        private int current = 0;
        private List<string> links = new List<string> {};
        private int linkcount = 0;
        private WebClient client = new WebClient();
        private WebClient client1 = new WebClient();
        private WebClient client2 = new WebClient();

        public MainWindow()
        {
            InitializeComponent();
            log.AppendText("Настроено на группу с id = " + group_id + "\n");
            log.AppendText("Нажмите на кнопку загрузить ссылки с youtube.\n");
        }

        private static void _DeleteSingleCookie(string name, Uri url)
        {
            // Calculate "one day ago"
            DateTime expiration = DateTime.UtcNow - TimeSpan.FromDays(1);
            // Format the cookie as seen on FB.com.  Path and domain name are important factors here.
            string cookie = String.Format("{0}=; expires={1}; path=/; domain=.vk.com", name, expiration.ToString("R"));
            // Set a single value from this cookie (doesnt work if you try to do all at once, for some reason)
            Application.SetCookie(url, cookie);
        }

        private static void _DeleteEveryCookie(Uri url)
        {
            string cookie = string.Empty;
            try
            {
                // Get every cookie (Expiration will not be in this response)
                cookie = Application.GetCookie(url);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // "no more data is available" ... happens randomly so ignore it.
            }
            if (!string.IsNullOrEmpty(cookie))
            {
                // This may change eventually, but seems quite consistent for Facebook.com.
                // ... they split all values w/ ';' and put everything in foo=bar format.
                string[] values = cookie.Split(';');

                foreach (string s in values)
                {
                    if (s.IndexOf('=') > 0)
                    {
                        // Sets value to null with expiration date of yesterday.
                        _DeleteSingleCookie(s.Substring(0, s.IndexOf('=')).Trim(), url);
                    }
                }
            }
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            _DeleteEveryCookie(new Uri("https://oauth.vk.com/"));
            log.AppendText("Загружаем окно авторизации вконтакте.\n");
            log.AppendText("Введите логин и пароль вконтакте.\n");
            current = 0;
            browser.LoadCompleted += Browser_LoadCompleted;
            browser.Navigate("https://oauth.vk.com/authorize?client_id=5999898&redirect_uri=https://oauth.vk.com/blank.html&display=popup&scope=video,wall&response_type=token&v=5.63");
            button.IsEnabled = false;
        }

        private void Browser_LoadCompleted(object sender, NavigationEventArgs e)
        {
            switch (current)
            {
                case 0:
                    log.AppendText("Парсим авторизацию вконтакте.\n");
                    String url = e.Uri.ToString();
                    var position = url.IndexOf("blank.html#");
                    if (position > 0)
                    {
                        string[] tokens = url.Split('#');
                        if (tokens.Length == 2)
                        {
                            string[] tokens1 = tokens[1].Split('&');
                            if (tokens1.Length == 3)
                            {
                                access_token = tokens1[0];
                                //log.AppendText(access_token + "\n");
                                log.AppendText("access_token получен!\n");
                                log.AppendText("Грузим популярное youtube.\nhttps://www.youtube.com/feed/trending\n");
                                log.AppendText("Поиск ссылок на видео youtube. Подождите.\n");
                                current = 1;
                                browser.Navigate("https://www.youtube.com/feed/trending");
                            }
                        }
                    }
                    break;
                case 1:
                    dynamic doc = browser.Document;
                    String htmlText = doc.documentElement.InnerHtml;

                    var offset = 0;
                    var found = true;
                    String last = "";

                    while (found)
                    {
                        var strpos = htmlText.IndexOf("/watch?", offset);
                        if (strpos > 0)
                        {
                            var link = htmlText.Substring(strpos, 20);
                            offset += 20;
                            if (last != link)
                            {
                                var newlink = "www.youtube.com" + link;
                                links.Add(newlink);
                                log.AppendText(newlink + "\n");
                                last = link;
                            }   
                        }
                        else
                        {
                            found = false;
                            current = 2;
                            log.AppendText("Поиск ссылок на видео youtube завершен!\nПосле нажатия на Запостить ролики на завтра ждите.\nМаксимум 5 минут.\nМожете запостить!\n");
                            button1.IsEnabled = true;
                        }
                    }
                    break;
                case 2:
                    break;
            }
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            if (links.Count > 1)
            {
                linkcount = offset;
                var hours = 0;
                while (linkcount < offset + last)
                {
                    Stream data = client.OpenRead("https://api.vk.com/method/video.save?" + access_token +
                        "&v=5.63&name=" + video_name + "&wallpost=0&group_id=" + group_id + "&link=" 
                        + links[linkcount] + "&description=" + description);

                    StreamReader reader = new StreamReader(data);
                    string content = reader.ReadToEnd();

                    if (!content.Contains("error"))
                    {
                        int start_pos = content.IndexOf("upload_url\":\"");
                        start_pos += 13;
                        int endpos = content.IndexOf("\"", start_pos);
                        string url = content.Substring(start_pos, endpos - start_pos);
                        url = url.Replace(@"\", string.Empty);

                        int vid_start_pos = content.IndexOf("\"video_id\":");
                        vid_start_pos += 11;
                        int vid_endpos = content.IndexOf(",", vid_start_pos);
                        string vid = content.Substring(vid_start_pos, vid_endpos - vid_start_pos);
                        content = "";

                        Stream data1 = client1.OpenRead(url);
                        StreamReader reader1 = new StreamReader(data1);
                        string content1 = reader1.ReadToEnd();

                        if (content1.Contains("error"))
                        {
                            //if (last + 1 < 72)
                            //    last++;
                            log.AppendText("Ошибка. Ссылка " + links[linkcount] + " не добавлена.\n " + content1 + "\n");
                        }
                        else
                        {
                            log.AppendText("Ссылка " + links[linkcount] + " добавлена.\n");
                            DateTime d1 = DateTime.Today.AddDays(day).AddHours(hours+3);
                            Int32 unixTimestamp = (Int32)(d1.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                            Stream data2 = client2.OpenRead("https://api.vk.com/method/wall.post?" + access_token + "&v=5.63&owner_id=-" + group_id + "&from_group=1&attachments=video-" + group_id +"_" + vid + "&signed=0&publish_date=" + unixTimestamp + "&message=" + message);
                            StreamReader reader2 = new StreamReader(data2);
                            string content2 = reader2.ReadToEnd();
                            if (content2.Contains("error"))
                            {
                                log.AppendText("Ошибка. Ссылка " + links[linkcount] + " не запланирована.\n " + content1 + "\n");
                                //if (last + 1 < 72)
                                //    last++;
                            }
                            else
                            {
                                log.AppendText("Ссылка " + links[linkcount] + " запланирована!\n");
                                hours += hours_count;
                            }
                            content2 = "";
                        }

                        content1 = "";
                        data1.Close();
                        reader1.Close();

                        data.Close();
                        reader.Close();
                    }
                    else
                    {
                        log.AppendText("Ошибка.\n "+ content + "\n");
                    }
                    System.Threading.Thread.Sleep(100);
                    linkcount++;
                }
                log.AppendText("Ссылки запланированы. Проверьте группу.\n");
                log.AppendText("Если все ссылки с ошибками, то возможно они уже запланированы.\n");
                log.AppendText("В этом случае проверьте группу еще раз.\n");
                log.AppendText("Чтобы добавлять по новой перезапустите программу!");
            }
            else
            {
                log.AppendText("Ссылки не загружены. Нажмите загрузить.\n");
            }

        }

        private void log_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            log.ScrollToEnd();
        }
    }
}
