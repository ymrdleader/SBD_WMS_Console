using System;
using System.Data;
using System.IO;
using System.Linq;
using MySql.Data.MySqlClient;

namespace SBD_WMS_Console
{
    class Program
    {
        static void Main(string[] args)
        {
            updateComProduct();
        }
        /// <summary>
        /// 寫入Log
        /// </summary>
        private static void Log(string logMessage)
        {
            if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + @"\log\"))
            {
                Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + @"\log\");
            }
            string DIRNAME = AppDomain.CurrentDomain.BaseDirectory + @"\log\";
            string FILENAME = DIRNAME + DateTime.Now.ToString("yyyyMMdd") + ".txt";
            if (!File.Exists(FILENAME))
            {
                File.Create(FILENAME).Close();
            }
            using (StreamWriter w = File.AppendText(FILENAME))
            {
                w.Write("\r\n");
                w.WriteLine("---------------------------------Log記錄-------------------------------------");
                w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
                w.WriteLine("  :");
                w.WriteLine("Log訊息 : {0}", logMessage);
                w.WriteLine("-----------------------------------------------------------------------------");
                w.Close();
            }
        }
        private static void updateComProduct()
        {
            string querySql = string.Empty;
            string querySunSql = string.Empty;
            string updateSql = string.Empty;
            using (MySqlConnection cn = new MySqlConnection(Properties.Settings.Default.SBD_WMSConnectionString))
            {
                using (MySqlConnection cnSun = new MySqlConnection(Properties.Settings.Default.SunstigeConnection))
                {
                    cn.Open();
                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = cn;
                    cmd.CommandTimeout = 100000;

                    MySqlTransaction transaction;
                    transaction = cn.BeginTransaction();
                    cmd.Transaction = transaction;

                    cnSun.Open();
                    MySqlCommand cmdSun = new MySqlCommand();
                    cmdSun.Connection = cnSun;
                    cmdSun.CommandTimeout = 100000;

                    MySqlTransaction transactionSun;
                    transactionSun = cnSun.BeginTransaction();
                    cmdSun.Transaction = transactionSun;

                    try
                    {
                        querySql = $@"select Prodid,ProdidName,IsForShipment from comproduct";
                        cmd.CommandText = querySql;
                        MySqlDataReader dr = cmd.ExecuteReader();
                        DataTable dt = new DataTable();
                        dt.Load(dr);
                        dr.Close();

                        querySunSql = $@"select t_item,t_dsca from sap_ttiitm001";
                        cmdSun.CommandText = querySunSql;
                        MySqlDataReader drSun = cmdSun.ExecuteReader();
                        DataTable dtSun = new DataTable();
                        dtSun.Load(drSun);
                        drSun.Close();

                        // 遍歷 dtSun 的每一行
                        foreach (DataRow rowSun in dtSun.Rows)
                        {
                            string prodidSun = rowSun["t_item"].ToString();

                            // 在 dt 中查找相同的 Prodid
                            DataRow foundRow = dt.Select($"Prodid = '{prodidSun}'").FirstOrDefault();

                            // 如果在 dt 中有找到相同的 Prodid
                            if (foundRow != null)
                            {
                                // 更新 ProdidName
                                foundRow["ProdidName"] = rowSun["t_dsca"].ToString();
                            }
                            else
                            {
                                // 創建新的 DataRow 對象
                                DataRow newRow = dt.NewRow();

                                // 設置 newRow 的欄位值
                                newRow["Prodid"] = rowSun["t_item"];
                                newRow["ProdidName"] = rowSun["t_dsca"];
                                newRow["IsForShipment"] = false;

                                // 將 newRow 添加到 dt
                                dt.Rows.Add(newRow);
                            }
                        }

                        using (MySqlDataAdapter da = new MySqlDataAdapter())
                        {
                            da.SelectCommand = new MySqlCommand(querySql, cn);
                            da.SelectCommand.Transaction = transaction;

                            MySqlCommandBuilder builder = new MySqlCommandBuilder(da);

                            da.UpdateCommand = builder.GetUpdateCommand();
                            da.InsertCommand = builder.GetInsertCommand();
                            da.DeleteCommand = builder.GetDeleteCommand();

                            da.Update(dt);

                            transaction.Commit();  // 提交事務
                        }
                        Log("Upload Status Success.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        transactionSun.Rollback();
                        Log("Upload Exception: " + ex.ToString());
                    }
                }
            }
        }
    }
}
