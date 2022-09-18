using RampSQL.Reader;
using System.Data;
using System.Data.Common;
using WrapSql;

namespace RampSQL.Query
{
    public static class WrapSQLConnector
    {
        #region WrapSQL extensions
        public static void ExecuteNonQuery(this WrapSqlBase sql, IQuerySection query) => sql.ExecuteNonQuery(query.ToString(), query.GetParameters());
        public static void ExecuteNonQueryACon(this WrapSqlBase sql, IQuerySection query) => sql.ExecuteNonQueryACon(query.ToString(), query.GetParameters());
        public static T ExecuteScalar<T>(this WrapSqlBase sql, IQuerySection query) => sql.ExecuteScalar<T>(query.ToString(), query.GetParameters());
        public static T ExecuteScalarACon<T>(this WrapSqlBase sql, IQuerySection query) => sql.ExecuteScalarACon<T>(query.ToString(), query.GetParameters());
        public static object ExecuteScalar(this WrapSqlBase sql, IQuerySection query) => sql.ExecuteScalar(query.ToString(), query.GetParameters());
        public static object ExecuteScalarACon(this WrapSqlBase sql, IQuerySection query) => sql.ExecuteScalarACon(query.ToString(), query.GetParameters());
        public static RampReader ExecuteQuery(this WrapSqlBase sql, IQuerySection query) => new RampReader(sql.ExecuteQuery(query.ToString(), query.GetParameters()));
        public static DataTable CreateDataTable(this WrapSqlBase sql, IQuerySection query) => sql.CreateDataTable(query.ToString(), query.GetParameters());
        public static DataAdapter GetDataAdapter(this WrapSqlBase sql, IQuerySection query) => sql.GetDataAdapter(query.ToString(), query.GetParameters());

        #endregion
    }
}
