using System.Threading.Tasks;

namespace ProtelWebReports.Services
{
    public class QueryResult
    {
        public System.Collections.Generic.List<string> ColumnNames { get; set; } = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<System.Collections.Generic.List<string>> Rows { get; set; } =
            new System.Collections.Generic.List<System.Collections.Generic.List<string>>();
    }

    public interface IProtelDbService
    {
        Task<QueryResult> ExecuteQueryAsync(string sql);
    }
}