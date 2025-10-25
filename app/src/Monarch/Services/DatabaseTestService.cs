using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using Microsoft.Data.SqlClient;
using Monarch.Models;

namespace Monarch.Services
{
    public class DatabaseTestService
    {
        private readonly string _connectionString;

        public DatabaseTestService()
        {
            string password = File.ReadAllText("/run/secrets/monarch_sql_monarch_password");
            _connectionString = $"Server=sqlserver;Database=monapi;User ID=monarch;Password={password};TrustServerCertificate=True;";;
        }

        public List<ResourceStatus> GetAllStatuses()
        {
            var results = new List<ResourceStatus>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            string query = "SELECT resourceName, currentStatus FROM sampleTable;";
            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                results.Add(new ResourceStatus
                    {
                        ResourceName = reader.GetString(reader.GetOrdinal("resourceName")),
                        CurrentStatus = reader.GetString(reader.GetOrdinal("currentStatus"))
                    }
                );
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
