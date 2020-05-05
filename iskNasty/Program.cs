using System;

namespace iskNasty
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Let's go!");

            try
            {
                ProcessConnect();
                Console.WriteLine("Connected");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.ReadLine();
        }

        public static void ProcessConnect()
        {

            Configurer conf;

            try
            {
                conf = new Configurer("isk_config.cfg");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Can't start isk - no config file: {ex.Message}");
                Console.ReadLine();
                return;
            }

            var isk = new IskNasty(int.Parse(conf.Get("app_id")), conf.Get("app_hash"),
                conf.Get("authdb_host"),
                conf.Get("authdb_name"),
                conf.Get("authdb_login"),
                conf.Get("authdb_pass"),
                bool.Parse(conf.Get("test_mode")));

            isk.Connect();

            isk.OnInfo += Isk_OnInfo;
            isk.OnDebug += Isk_OnDebug;
            isk.OnError += Isk_OnError;
            isk.OnFatal += Isk_OnFatal;
            isk.OnStop += Isk_OnStop;
            isk.OnUpdate += Isk_OnUpdate;

            isk.UpdateUserCache();

            while (!isk.isAuthorized())
            {
                string phone = conf.Get("phone");
                var hash = isk.StartAuthorisation(phone);
                Console.Write($"Get me code, sended to phone +{phone}: ");
                var code = Console.ReadLine();
                isk.ProcessAuthorization(phone, hash, code);
            }

            //isk.StartUserCacheUpdate();
            //isk.StartChannelCheck();
            isk.GetUnreadMessages();
        }

        private static void Isk_OnUpdate(object sender, string e) => Console.WriteLine($"Update >> {e}");

        private static void Isk_OnStop(object sender, string e) => Console.WriteLine($"Stop >> {e}");

        private static void Isk_OnFatal(object sender, string e) => Console.WriteLine($"FATAL >> {e}");

        private static void Isk_OnError(object sender, string e) => Console.WriteLine($"ERROR >> {e}");

        private static void Isk_OnDebug(object sender, string e) => Console.WriteLine($"debug >> {e}");

        private static void Isk_OnInfo(object sender, string e) => Console.WriteLine($"Info >> {e}");
    }
}
