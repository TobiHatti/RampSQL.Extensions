using RampSQL.Binder;
using RampSQL.Query;
using RampSQL.Reader;
using RampSQL.Schema;
using System.Data.Common;
using WrapSQL;

namespace RampSQL.Extensions
{
    public abstract class ModelIOHandler : IRampLoadable, IRampSaveable
    {
        private WrapSQLBase sql;
        public abstract RampModelBinder GetBinder();

        public void LinkDatabase(WrapSQLBase sql) => this.sql = sql;
        public IRampLoadable LoadAll() => LoadAll(this.sql);

        public IRampLoadable LoadAll(WrapSQLBase sql) => LoadFromRamp(sql, new QueryEngine().SelectFrom(GetBinder().Target).All());

        public IRampLoadable LoadFromPrimaryKey<T>(T ID) => LoadFromPrimaryKey<T>(this.sql, ID);
        public IRampLoadable LoadFromPrimaryKey<T>(WrapSQLBase sql, T ID)
        {
            RampModelBinder binder = GetBinder();
            return LoadFromRamp(sql, new QueryEngine().SelectFrom(binder.Target).All().Where.Is(binder.PrimaryKey.Column, ID));
        }

        public IRampLoadable LoadFromQuery(string query, params object[] values) => LoadFromQuery(this.sql, query, values);
        public IRampLoadable LoadFromQuery(WrapSQLBase sql, string query, params object[] values)
        {
            IRampLoadable model = null;
            sql.Open();
            using (RampReader reader = new RampReader(sql.ExecuteQuery(query, values)))
            {
                while (reader.Read()) model = LoadFromReader(reader);
            }
            sql.Close();
            return model;
        }

        public IRampLoadable LoadFromRamp(IQuerySection rampQuery) => LoadFromRamp(this.sql, rampQuery);
        public IRampLoadable LoadFromRamp(WrapSQLBase sql, IQuerySection rampQuery) => LoadFromQuery(sql, rampQuery.ToString(), rampQuery.GetParameters());

        public IRampLoadable LoadFromReader(DbDataReader reader) => LoadFromReader(new RampReader(reader));
        public IRampLoadable LoadFromReader(RampReader reader)
        {
            RampModelBinder binder = GetBinder();
            foreach (RampModelBinder.BindEntry bind in binder.Binds) bind.Set(reader[bind.Column]);
            binder.PrimaryKey.Set(reader[binder.PrimaryKey.Column]);
            return this;
        }


        public IRampLoadable LoadRange(RampColumn column, params object[] values) => LoadRange(this.sql, column, values);
        public IRampLoadable LoadRange(WrapSQLBase sql, RampColumn column, params object[] values) => LoadFromRamp(sql, new QueryEngine().SelectFrom(GetBinder().Target).All().Where.In(column, values));

        public void SaveModel() => SaveModel(this.sql);
        public void SaveModel(WrapSQLBase sql)
        {
            RampModelBinder binder = GetBinder();
            sql.Open();

            IQuerySection query;
            if (sql.ExecuteScalar<int>(new QueryEngine().SelectFrom(binder.Target).Count().Where.Is(binder.PrimaryKey.Column, binder.PrimaryKey.Get())) == 0)
            {
                query = new QueryEngine().InsertInto(binder.Target);
                foreach (RampModelBinder.BindEntry bind in binder.Binds) (query as InsertKeyValueQuery).Value(bind.Column, bind.Get());
                (query as InsertKeyValueQuery).Value(binder.PrimaryKey.Column, binder.PrimaryKey.Get()).GetLastID();
                binder.PrimaryKey.Set(sql.ExecuteScalar(query));
            }
            else
            {
                query = new QueryEngine().Update(binder.Target);
                foreach (RampModelBinder.BindEntry bind in binder.Binds) (query as UpdateKeyValueQuery).Value(bind.Column, bind.Get());
                (query as UpdateKeyValueQuery).Where.Is(binder.PrimaryKey.Column, binder.PrimaryKey.Get());
                sql.ExecuteNonQuery(query);
            }

            sql.Close();
        }
    }
}
