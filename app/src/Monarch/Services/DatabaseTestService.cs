using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using Microsoft.Data.SqlClient;

namespace Monarch.Services
{
    public class DatabaseTestService
    {
        private readonly string _connectionString;

        public DatabaseTestService()
        {
            string password = File.ReadAllText("/run/secrets/monarch_sql_monarch_password").Trim();
            _connectionString = $"Server=sqlserver;Database=monapi;User ID=monarch;Password={password};TrustServerCertificate=True;";;
        }

        public string TestConnection()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();
                return "Database connection succeeded.";
            }
            catch (Exception ex)
            {
                return $"Database connection failed: {ex.Message}";
            }
        }

        public List<(int Id, string ResourceName, string CurrentStatus, DateTime LastUpdated)> GetAllStatuses()
        {
            var results = new List<(int, string, string, DateTime)>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            string query = "SELECT id, resourceName, currentStatus, lastUpdated FROM sampleTable;";
            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                results.Add((
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDateTime(3)
                ));
            }

            return results;
        }

        public string? GetStatusByResource(string resourceName)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            string query = "SELECT currentStatus FROM sampleTable WHERE resourceName = @resourceName;";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@resourceName", resourceName);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return reader.GetString(0);
            }

            return null;
        }
    }
}
