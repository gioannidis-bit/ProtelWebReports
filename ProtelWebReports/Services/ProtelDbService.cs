using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace ProtelWebReports.Services
{
    public class ProtelDbService : IProtelDbService
    {
        private readonly string _connectionString;
        private readonly ILogger<ProtelDbService> _logger;

        public ProtelDbService(IConfiguration config, ILogger<ProtelDbService> logger)
        {
            _connectionString = config.GetConnectionString("ProtelDatabase");
            _logger = logger;
        }

        public async Task<QueryResult> ExecuteQueryAsync(string sql)
        {
            var result = new QueryResult();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(sql, connection))
                    {
                        // Set command timeout to a higher value for complex queries
                        command.CommandTimeout = 300; // 5 minutes

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Get column names
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                result.ColumnNames.Add(reader.GetName(i));
                            }

                            // Get rows
                            while (await reader.ReadAsync())
                            {
                                var row = new List<string>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    if (!reader.IsDBNull(i))
                                    {
                                        row.Add(reader.GetValue(i).ToString());
                                    }
                                    else
                                    {
                                        row.Add(string.Empty);
                                    }
                                }
                                result.Rows.Add(row);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL query");
                throw;
            }

            return result;
        }
    }
}