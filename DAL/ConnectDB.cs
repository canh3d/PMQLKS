using System;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;

namespace QLKS_AnPhu.DAL
{
    public class ConnectDB
    {
        private const string DefaultConnectionString =
            @"Data Source=ASUSCANH;Initial Catalog=QLDatPhongKS_AnPhu;Integrated Security=True;Encrypt=False;TrustServerCertificate=True";

        public static string ConnectionString
        {
            get
            {
                ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["HotelDb"];

                if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                {
                    return DefaultConnectionString;
                }

                return settings.ConnectionString;
            }
        }

        // Hàm mở kết nối
        public static SqlConnection GetConnection()
        {
            SqlConnection conn = new SqlConnection(ConnectionString);

            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }

            return conn;
        }

        // Hàm lấy dữ liệu SELECT
        public static DataTable GetData(string sql, params SqlParameter[] parameters)
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return dt;
        }

        // Hàm thêm, sửa, xóa
        public static int ExecuteNonQuery(string sql, params SqlParameter[] parameters)
        {
            int result = 0;

            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }

                    result = cmd.ExecuteNonQuery();
                }
            }

            return result;
        }

        // Hàm lấy 1 giá trị, ví dụ COUNT, SUM, MAX
        public static object? ExecuteScalar(string sql, params SqlParameter[] parameters)
        {
            object? result = null;

            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }

                    result = cmd.ExecuteScalar();
                }
            }

            return result;
        }
    }
}