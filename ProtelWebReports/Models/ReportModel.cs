using System;
using System.Collections.Generic;

namespace ProtelWebReports.Models
{
    public class ReportModel
    {
        public string ReportId { get; set; }
        public string Title { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string RevenueType { get; set; }
        public string CommitmentType { get; set; }
        public List<string> ColumnNames { get; set; } = new List<string>();
        public List<List<string>> Rows { get; set; } = new List<List<string>>();
    }
}