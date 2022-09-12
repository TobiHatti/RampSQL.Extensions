using RampSQL.Binder;
using RampSQL.Query;
using RampSQL.Reader;
using RampSQL.Schema;
using System.Data.Common;
using WrapSQL;

namespace RampSQL.Extensions
{
    public interface IRampLoadable : IRampBindable
    {
        void LinkDatabase(WrapSQLBase sql);
        IRampLoadable LoadFromPrimaryKey<T>(T ID);
        IRampLoadable LoadFromPrimaryKey<T>(WrapSQLBase sql, T ID);
        IRampLoadable LoadFromReader(RampReader reader);
        IRampLoadable LoadFromReader(DbDataReader reader);
        IRampLoadable LoadFromRamp(IQuerySection rampQuery);
        IRampLoadable LoadFromRamp(WrapSQLBase sql, IQuerySection rampQuery);
        IRampLoadable LoadFromQuery(string query, params object[] values);
        IRampLoadable LoadFromQuery(WrapSQLBase sql, string query, params object[] values);
        IRampLoadable LoadAll();
        IRampLoadable LoadAll(WrapSQLBase sql);
        IRampLoadable LoadRange(RampColumn column, params object[] values);
        IRampLoadable LoadRange(WrapSQLBase sql, RampColumn column, params object[] values);
    }

    public interface IRampSaveable : IRampBindable
    {
        void LinkDatabase(WrapSQLBase sql);
        void SaveModel();
        void SaveModel(WrapSQLBase sql);
    }
}
