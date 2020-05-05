using System;
using System.Data;
using MySql.Data.MySqlClient;

namespace iskNasty
{
    public class MySqlWorker
    {
        private readonly string _host;
        private readonly string _name;
        private readonly string _login;
        private readonly string _password;

        public MySqlWorker(string Host, string Name, string Login, string Password)
        {
            _host = Host;
            _name = Name;
            _login = Login;
            _password = Password;
        }

        public DataTable Exec(string Query)
        {
            MySqlConnection _conn;
            _conn = new MySqlConnection($"Database = {_name}; Data Source = {_host}; User Id = {_login}; Password = {_password}; SslMode=none");


            MySqlCommand cmd = new MySqlCommand(Query, _conn)
            {
                CommandType = CommandType.Text
            };

            DataTable dt = new DataTable("ResultDataTable");
            _conn.Open();
            MySqlDataAdapter sda = new MySqlDataAdapter(cmd);
            sda.Fill(dt);
            _conn.Close();
            return dt;

        }

        public string GetFirstValue(DataTable dt)
        {
            try
            {
                if (dt.Rows.Count == 0 || dt.Columns.Count == 0)
                {
                    return "";
                }
                return dt.Rows[0][0].ToString();

            }
            catch
            {
                return "";
            }
        }
    }
}
