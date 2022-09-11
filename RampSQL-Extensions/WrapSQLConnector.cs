using RampSQL.Query;
using RampSQL.Reader;
using System.Data;
using System.Data.Common;
using WrapSQL;

namespace RampSQL.Extensions
{
    public static class WrapSQLConnector
    {
        #region WrapSQL extensions
        public static void RampNoQuery(this WrapSQLBase sql, IQuerySection query) => sql.ExecuteNonQuery(query.ToString(), query.GetParameters());
        public static void RampNoQueryACon(this WrapSQLBase sql, IQuerySection query) => sql.ExecuteNonQueryACon(query.ToString(), query.GetParameters());
        public static T RampScalar<T>(this WrapSQLBase sql, IQuerySection query) => sql.ExecuteScalar<T>(query.ToString(), query.GetParameters());
        public static T RampScalarACon<T>(this WrapSQLBase sql, IQuerySection query) => sql.ExecuteScalarACon<T>(query.ToString(), query.GetParameters());
        public static object RampScalar(this WrapSQLBase sql, IQuerySection query) => sql.ExecuteScalar(query.ToString(), query.GetParameters());
        public static object RampScalarACon(this WrapSQLBase sql, IQuerySection query) => sql.ExecuteScalarACon(query.ToString(), query.GetParameters());
        public static RampReader RampQuery(this WrapSQLBase sql, IQuerySection query) => new RampReader(sql.ExecuteQuery(query.ToString(), query.GetParameters()));
        public static DataTable RampDataTable(this WrapSQLBase sql, IQuerySection query) => sql.CreateDataTable(query.ToString(), query.GetParameters());
        public static DataAdapter RampDataAdapter(this WrapSQLBase sql, IQuerySection query) => sql.GetDataAdapter(query.ToString(), query.GetParameters());

        #endregion
    }
}
