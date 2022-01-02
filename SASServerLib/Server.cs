using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace SASServerLib
{
    public struct PingReply
    {
        public bool Success { get; internal set; }
        public string Status { get; internal set; }
        public long RoundtripTime { get; internal set; }
        public int TTL { get; internal set; }
    }

    public struct Response
    {
        public bool Success { get; internal set; }
        public string Status { get; internal set; }
    }

    public class Server
    {
        private byte[] ServerIP = new byte[]{ 103,56,148,108 };

        private MySqlConnection SqlConnection { get; set; }
        private FtpWebRequest FtpWebRequest { get; set; }
        private string StringHost { get; set; }

        private NetworkCredential FtpCredentials { get; set; } = null;

        public string FTPPath { get; set; } = "/home";
        public bool IsConnected { get; internal set; } = false;
        public bool IsMySQLConnected { get; internal set; } = false;
        public bool IsFTPConnected { get; internal set; } = false;

        public Response Response = new Response();

        public Server()
        {
            IsConnected = RunPing().Success;
        }

        public int MySQLInsert(string table, string[] columns, string[] values)
        {
            if (!IsMySQLConnected)
            {
                Response.Status = "MySQL is not connected. Please call ConnectToMySQL(string dbName, string username, string password, int port = 3306) first!";
                return 0;
            }

            try
            {
                string strColumns = string.Join(", ", columns);
                string strValues = string.Join(", ", values);
                string query = "INSERT INTO " + table + " (" + strColumns + ") VALUES " + "(" + strValues + ")";
                MySqlCommand sqlCommand = new MySqlCommand(query, SqlConnection);
                return sqlCommand.ExecuteNonQuery();
            }
            catch (MySqlException exception)
            {
                Response.Success = false;
                Response.Status = exception.Message;
                return 0;
            }
        }

        public int MySQLUpdate(string table, string[] columns, string[] values, string condition = null)
        {
            if (!IsMySQLConnected)
            {
                Response.Status = "MySQL is not connected. Please call ConnectToMySQL(string dbName, string username, string password, int port = 3306) first!";
                return 0;
            }

            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < columns.Length; ++i)
                {
                    sb.Append(" " + columns.GetValue(i) + "=");
                    sb.Append("'" + values.GetValue(i) + "'");
                }
                string query = "UPDATE SET " + table + sb.ToString();
                if (condition != null)
                    query += " WHERE " + condition;
                MySqlCommand sqlCommand = new MySqlCommand(query, SqlConnection);
                return sqlCommand.ExecuteNonQuery();
            }
            catch (MySqlException exception)
            {
                Response.Success = false;
                Response.Status = exception.Message;
                return 0;
            }
        }

        public int MySQLDelete(string table, string condition = null)
        {
            if (!IsMySQLConnected)
            {
                Response.Status = "MySQL is not connected. Please call ConnectToMySQL(string dbName, string username, string password, int port = 3306) first!";
                return 0;
            }

            try
            {
                string query = "TRUNCATE TABLE " + table;
                if (condition != null)
                    query = "DELETE FROM " + table + " WHERE " + condition;
                MySqlCommand sqlCommand = new MySqlCommand(query, SqlConnection);
                return sqlCommand.ExecuteNonQuery();
            }
            catch (MySqlException exception)
            {
                Response.Success = false;
                Response.Status = exception.Message;
                return 0;
            }
        }

        public List<string>[] MySQLSelect(string table, string condition = null)
        {
            if (!IsMySQLConnected)
            {
                Response.Status = "MySQL is not connected. Please call ConnectToMySQL(string dbName, string username, string password, int port = 3306) first!";
                return new List<string>[0];
            }

            try
            {
                List<string> columns = GetTableColumns(table);
                List<string>[] data = new List<string>[columns.Count];
                for (int list = 0; list < columns.Count; ++list)
                {
                    data[list] = new List<string>();
                }

                string query = "SELECT * FROM " + table;
                if (condition != null)
                    query += " WHERE " + condition;

                MySqlCommand sqlCommand = new MySqlCommand(query, SqlConnection);
                MySqlDataReader sqlDataReader = sqlCommand.ExecuteReader();

                while (sqlDataReader.Read())
                {
                    int index = 0;
                    foreach (var column in columns)
                    {
                        data[index].Add(sqlDataReader.GetString(column));
                        index++;
                    }
                }
                sqlDataReader.Close();
                return data;
            }
            catch (MySqlException exception)
            {
                Response.Success = false;
                Response.Status = exception.Message;
                return new List<string>[0];
            }
        }

        public Response FTPUpload(string file, string uploadPath = null)
        {
            if (!IsFTPConnected)
            {
                Response.Status = "FTP is not connected. Please call ConnectToFTP(string username, string password, int port = 21) first!";
                Response.Success = false;
                return Response;
            }

            try
            {
                StreamReader source = new StreamReader(@"" + file);
                byte[] content = System.Text.Encoding.UTF8.GetBytes(source.ReadToEnd());
                source.Close();

                string requestUri = FtpWebRequest.RequestUri.OriginalString;
                if (uploadPath != null && uploadPath.StartsWith("/"))
                    requestUri += uploadPath;

                FtpWebRequest = (FtpWebRequest)WebRequest.Create(requestUri + "/" + Path.GetFileName(file));
                FtpWebRequest.CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable);
                FtpWebRequest.Method = WebRequestMethods.Ftp.UploadFile;
                FtpWebRequest.ContentLength = content.Length;
                FtpWebRequest.Credentials = FtpCredentials;

                Stream stream = FtpWebRequest.GetRequestStream();
                stream.Write(content, 0, content.Length);
                stream.Close();

                FtpWebResponse response = (FtpWebResponse)FtpWebRequest.GetResponse();
                Response.Status = response.StatusDescription;
                Response.Success = true;
                response.Close();
            }
            catch (WebException exception)
            {
                Response.Status = ((FtpWebResponse)exception.Response).StatusDescription;
                Response.Success = false;
            }
            catch (Exception exception)
            {
                Response.Status = exception.Message;
                Response.Success = false;
            }

            return Response;
        }

        public Response FTPDownload(string downloadPath)
        {
            if (!IsFTPConnected)
            {
                Response.Status = "FTP is not connected. Please call ConnectToFTP(string username, string password, int port = 21) first!";
                Response.Success = false;
                return Response;
            }

            try
            {
                string requestUri = FtpWebRequest.RequestUri.OriginalString;
                FtpWebRequest = (FtpWebRequest)WebRequest.Create(requestUri + "/" + downloadPath);
                FtpWebRequest.CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable);
                FtpWebRequest.Method = WebRequestMethods.Ftp.DownloadFile;
                FtpWebRequest.Credentials = FtpCredentials;
                FtpWebRequest.KeepAlive = false;

                FtpWebResponse response = (FtpWebResponse)FtpWebRequest.GetResponse();
                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream);
                reader.ReadToEnd();
                reader.Close();

                Response.Status = response.StatusDescription;
                Response.Success = true;
                response.Close();
            }
            catch (WebException exception)
            {
                Response.Status = ((FtpWebResponse)exception.Response).StatusDescription;
                Response.Success = false;
            }
            catch (Exception exception)
            {
                Response.Status = exception.Message;
                Response.Success = false;
            }

            return Response;
        }

        public Response ConnectToMySQL(string dbName, string username, string password, int port = 3306)
        {
            if (!IsConnected)
            {
                string state = "Unable to connect to server.";
                state += " Contact server administrator! (" + Response.Status + ").";
                Response.Status = state;
                return Response;
            }

            string connectionString = "SERVER=" + StringHost + ";PORT=" + port + ";";
            connectionString += "DATABASE=" + dbName + ";UID=" + username + ";PASSWORD=" + password + ";";
            if (OpenMySqlConnection(connectionString))
            {
                IsMySQLConnected = true;
                Response.Success = true;
                Response.Status = "Connected!";
            }
            return Response;
        }

        public Response ConnectToFTP(string username, string password, int port = 21)
        {
            try
            {
                //FTPPath += "/" + username;
                string ftpUri = "ftp://" + StringHost + ":" + port;
                FtpWebRequest = (FtpWebRequest)WebRequest.Create(ftpUri + FTPPath);
                FtpWebRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                FtpCredentials = new NetworkCredential(username, password);
                FtpWebRequest.Credentials = FtpCredentials;
                FtpWebRequest.KeepAlive = false;
                FtpWebResponse ftp = (FtpWebResponse)FtpWebRequest.GetResponse();
                Response.Status = ftp.WelcomeMessage;
                Response.Success = true;
                IsFTPConnected = true;
                ftp.Close();
            }
            catch (Exception exception)
            {
                Response.Success = false;
                Response.Status = exception.Message;
            }

            return Response;
        }

        public PingReply RunPing(int timeout = 60)
        {
            PingReply pingReply = new PingReply();
            IPAddress iPAddress = new IPAddress(ServerIP);
            StringHost = iPAddress.MapToIPv4().ToString();
            System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping();
            System.Net.NetworkInformation.PingReply reply = ping.Send(iPAddress, timeout * 1000);
            bool success = (reply.Status == System.Net.NetworkInformation.IPStatus.Success);
            pingReply.RoundtripTime = reply.RoundtripTime;
            pingReply.Status = reply.Status.ToString();
            Response.Status = pingReply.Status;
            pingReply.TTL = reply.Options.Ttl;
            pingReply.Success = success;
            return pingReply;
        }

        public bool CloseMySqlConnection()
        {
            try
            {
                SqlConnection.Close();
                IsMySQLConnected = false;
                return true;
            }
            catch (MySqlException exception)
            {
                Response.Status = exception.Message;
                return false;
            }
        }

        private bool OpenMySqlConnection(string connectionString)
        {
            try
            {
                SqlConnection = new MySqlConnection(connectionString);
                SqlConnection.Open();
                return true;
            }
            catch (MySqlException exception)
            {
                Response.Status = exception.Message;
                switch (exception.Number)
                {
                    case 0:
                        Response.Status = "Cannot connect to server.  Contact administrator";
                        break;
                    case 1045:
                        Response.Status = "Invalid username/password, please try again";
                        break;
                }
                return false;
            }
        }

        private List<string> GetTableColumns(string table)
        {
            List<string> columns = new List<string>();
            MySqlCommand sqlCommand = new MySqlCommand("SHOW COLUMNS FROM " + table, SqlConnection);
            MySqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
            while (sqlDataReader.Read())
            {
                columns.Add(sqlDataReader.GetString("Field"));
            }
            sqlDataReader.Close();
            return columns;
        }
    }
}
