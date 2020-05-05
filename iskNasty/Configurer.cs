using System;
using System.Collections.Generic;
using System.IO;

namespace iskNasty
{
    public class Configurer
    {
        internal Dictionary<string, string> config;

        public Configurer(string filename)
        {
            config = new Dictionary<string, string>();
            string f = File.ReadAllText(filename);

            using (StringReader reader = new StringReader(f))
            {
                string line = string.Empty;
                do
                {
                    line = reader.ReadLine();
                    if (line != null)
                    {
                        if (line.IndexOf("#") != 0)
                        {
                            int point = line.IndexOf("=");
                            if (point > 0)
                            {
                                string key = line.Substring(0, point);
                                string value = line.Substring(point + 1, line.Length - point - 1);
                                config.Add(key, value);
                            }
                        }
                    }

                } while (line != null);
            }
        }

        public string Get(string key)
        {
            foreach (var d in config)
            {
                if (key.ToLower() == d.Key.ToLower())
                {
                    return d.Value;
                }
            }
            return "";
        }
    }

}
