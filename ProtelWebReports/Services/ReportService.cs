using ProtelWebReports.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ProtelWebReports.Services
{
    public class ReportService : IReportService
    {
        private readonly IProtelDbService _dbService;
        private readonly string _protelPath;

        public ReportService(IProtelDbService dbService, Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _dbService = dbService;
            _protelPath = config["ProtelSettings:ProtelPath"];
        }

        public async Task<ReportModel> GenerateReportAsync(string reportId, DateTime fromDate, DateTime toDate,
                                                         string revenueType, string comType)
        {
            // Load and parse XML file
            var xmlPath = Path.Combine(_protelPath, "xml", $"{reportId}.xml");
            if (!File.Exists(xmlPath))
            {
                throw new FileNotFoundException($"Report XML file not found: {xmlPath}");
            }

            var doc = XDocument.Load(xmlPath);

            // Extract SQL query
            var mainSqlQuery = ExtractMainQuery(doc);
            if (string.IsNullOrEmpty(mainSqlQuery))
            {
                throw new InvalidOperationException("Main SQL query not found in XML file");
            }

            // Replace parameters in the query
            mainSqlQuery = ReplaceParameters(mainSqlQuery, fromDate, toDate, revenueType, comType);

            // Execute query
            var reportData = await _dbService.ExecuteQueryAsync(mainSqlQuery);

            // Create the report model
            var reportModel = new ReportModel
            {
                ReportId = reportId,
                Title = ExtractReportTitle(doc),
                FromDate = fromDate,
                ToDate = toDate,
                RevenueType = revenueType == "0" ? "Gross" : "Net",
                CommitmentType = comType == "1" ? "Detailed" : "Global",
                ColumnNames = reportData.ColumnNames,
                Rows = reportData.Rows
            };

            return reportModel;
        }

        private string ExtractReportTitle(XDocument doc)
        {
            var titleElement = doc.Descendants("Title").FirstOrDefault();
            return titleElement?.Value ?? "Report";
        }

        private string ExtractMainQuery(XDocument doc)
        {
            var sqlQueryObjects = doc.Descendants("ProtelObject")
                .Where(o => o.Element("Class")?.Value == "SQLQuery");

            foreach (var obj in sqlQueryObjects)
            {
                var nameElement = obj.Element("Name");
                var queryElement = obj.Element("Query");

                if (nameElement != null && queryElement != null)
                {
                    var name = nameElement.Value;
                    // Find the main query that has "CollectData" in its name
                    if (name.Contains("CollectData"))
                    {
                        return queryElement.Value;
                    }
                }
            }

            // If no specific "CollectData" query found, return the first query
            return sqlQueryObjects.FirstOrDefault()?.Element("Query")?.Value;
        }

        private string ReplaceParameters(string query, DateTime fromDate, DateTime toDate,
                                       string revenueType, string comType)
        {
            // Format dates
            string fromDateStr = fromDate.ToString("yyyy-MM-dd");
            string toDateStr = toDate.ToString("yyyy-MM-dd");

            // Replace parameters
            string result = query
                .Replace("[#From Date : ]", fromDateStr)
                .Replace("[#To Date : ]", toDateStr)
                .Replace("[?FILE(RevenueSelection|prompt|nr)Revenue Type ]", revenueType)
                .Replace("[?FILE(hit_ComType|Descr|nID)Select Com Type Diff]", comType)
                .Replace("{ProtelPath}", _protelPath)
                .Replace("{MPEHotel}", "1") // Default hotel ID
                .Replace("{StationID}", "1"); // Default station ID

            return result;
        }
    }
}