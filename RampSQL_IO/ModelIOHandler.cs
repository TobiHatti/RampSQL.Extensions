using RampSQL.Binder;
using RampSQL.Query;
using RampSQL.Schema;
using System;
using System.Collections;
using System.Collections.Generic;
using WrapSQL;
using static RampSQL.Binder.RampModelBinder;

namespace RampSQL.Extensions
{
    public abstract class ModelIOHandler : IRampLoadable, IRampSaveable
    {
        private static WrapSQLBase _sql = null;
        private static WrapSQLBase Sql
        {
            get
            {
                if (_sql == null) throw new Exception("SQL Instance not set");
                return _sql;
            }
            set
            {
                _sql = value;
            }
        }

        public static void LinkDatabase(WrapSQLBase sqlConnection) => Sql = sqlConnection;

        public abstract RampModelBinder GetBinder();

        public IRampLoadable LoadFromPrimaryKey<T>(T ID) => LoadFromRamp(SelectQueryBuilder().Where.Is(GetBinder().PrimaryKey.Column, ID));
        public IRampLoadable LoadFromRamp(IQuerySection rampQuery) => ExecuteLoad(rampQuery);

        public static IRampLoadable[] LoadAll<T>() where T : IRampLoadable => LoadRangeFromRamp<T>(SelectQueryBuilder<T>());
        public static IRampLoadable[] LoadRange<T>(RampColumn column, params object[] values) where T : IRampLoadable => LoadRangeFromRamp<T>(SelectQueryBuilder<T>().Where.In(column, values));
        public static IRampLoadable[] LoadRangeFromRamp<T>(IQuerySection rampQuery) where T : IRampLoadable => ExecuteRangeLoad(rampQuery, typeof(T));
        public static IRampLoadable[] LoadRangeFromRamp(IQuerySection rampQuery, Type targetType) => ExecuteRangeLoad(rampQuery, targetType);

        private class RefBindQueueEntry
        {
            public object RefLinkValue { get; set; }
            public BindEntry ParentBind { get; set; }
        }

        private static IRampLoadable[] ExecuteRangeLoad(IQuerySection query, Type targetType)
        {
            Sql.Open();

            List<RefBindQueueEntry> refQueue = new List<RefBindQueueEntry>();

            Type genericListType = typeof(List<>).MakeGenericType(targetType);
            IList models = (IList)Activator.CreateInstance(genericListType);

            Sql.ExecuteQuery(query).ReadAll((r) =>
            {
                IRampLoadable model = (IRampLoadable)Activator.CreateInstance(targetType);
                RampModelBinder binder = model.GetBinder();

                // Primary key
                binder.PrimaryKey.Set(r[binder.PrimaryKey.Column]);

                foreach (BindEntry bind in binder.Binds)
                {
                    // Primitives
                    if (bind.BindType == BindType.Primitive)
                        bind.Set(r[bind.Column]);

                    // Activate Reference
                    if (bind.BindType == BindType.Reference)
                    {
                        IRampLoadable referenceModel = (IRampLoadable)Activator.CreateInstance(bind.Type);
                        bind.Set(referenceModel);

                        refQueue.Add(new RefBindQueueEntry()
                        {
                            RefLinkValue = r[bind.Column],
                            ParentBind = bind
                        });
                    }

                    if (bind.BindType == BindType.ReferenceArray)
                    {
                        refQueue.Add(new RefBindQueueEntry()
                        {
                            RefLinkValue = r[bind.Column],
                            ParentBind = bind
                        });
                    }
                }

                models.Add(model);

            });
            Sql.Close();


            foreach (RefBindQueueEntry rq in refQueue)
            {
                switch (rq.ParentBind.BindType)
                {
                    case BindType.Reference:
                        IRampLoadable refModel = rq.ParentBind.Get() as IRampLoadable;
                        RampModelBinder refBinder = refModel.GetBinder();
                        refModel.LoadFromRamp(new QueryEngine().SelectAllFrom(refBinder.Target).Where.Is(rq.ParentBind.ReferenceColumn, rq.RefLinkValue));
                        break;
                    case BindType.ReferenceArray:
                        rq.ParentBind.Set(LoadRangeFromRamp(new QueryEngine().SelectAllFrom(rq.ParentBind.ReferenceColumn.ParentTable).Where.Is(rq.ParentBind.ReferenceColumn, rq.RefLinkValue), rq.ParentBind.Type));
                        break;
                }
            }

            IRampLoadable[] resArray = (IRampLoadable[])Activator.CreateInstance(targetType.MakeArrayType(), models.Count);
            models.CopyTo(resArray, 0);

            return resArray;
        }

        private IRampLoadable ExecuteLoad(IQuerySection query)
        {
            Sql.Open();

            List<RefBindQueueEntry> refQueue = new List<RefBindQueueEntry>();

            Sql.ExecuteQuery(query).ReadAll((r) =>
            {
                RampModelBinder binder = GetBinder();

                // Primary key
                binder.PrimaryKey.Set(r[binder.PrimaryKey.Column]);

                foreach (BindEntry bind in binder.Binds)
                {
                    // Primitives
                    if (bind.BindType == BindType.Primitive)
                        bind.Set(r[bind.Column]);

                    // Activate Reference
                    if (bind.BindType == BindType.Reference)
                    {
                        IRampLoadable referenceModel = (IRampLoadable)Activator.CreateInstance(bind.Type);
                        bind.Set(referenceModel);

                        refQueue.Add(new RefBindQueueEntry()
                        {
                            RefLinkValue = r[bind.Column],
                            ParentBind = bind
                        });
                    }

                    if (bind.BindType == BindType.ReferenceArray)
                    {
                        refQueue.Add(new RefBindQueueEntry()
                        {
                            RefLinkValue = r[bind.Column],
                            ParentBind = bind
                        });
                    }
                }
            });
            Sql.Close();


            foreach (RefBindQueueEntry rq in refQueue)
            {
                switch (rq.ParentBind.BindType)
                {
                    case BindType.Reference:
                        IRampLoadable refModel = rq.ParentBind.Get() as IRampLoadable;
                        RampModelBinder refBinder = refModel.GetBinder();
                        refModel.LoadFromRamp(new QueryEngine().SelectAllFrom(refBinder.Target).Where.Is(rq.ParentBind.ReferenceColumn, rq.RefLinkValue));
                        break;
                    case BindType.ReferenceArray:
                        rq.ParentBind.Set(LoadRangeFromRamp(new QueryEngine().SelectAllFrom(rq.ParentBind.ReferenceColumn.ParentTable).Where.Is(rq.ParentBind.ReferenceColumn, rq.RefLinkValue), rq.ParentBind.Type));
                        break;
                }
            }
            return this;
        }


        private static JoinQuery SelectQueryBuilder<T>() where T : IRampLoadable
        {
            T instance = (T)Activator.CreateInstance(typeof(T));
            RampModelBinder binder = instance.GetBinder();
            SelectQuery query = new QueryEngine().SelectAllFrom(binder.Target);
            return query;
        }

        private JoinQuery SelectQueryBuilder()
        {
            RampModelBinder binder = GetBinder();
            SelectQuery query = new QueryEngine().SelectAllFrom(binder.Target);
            return query;
        }

        public void SaveModel()
        {
            RampModelBinder binder = GetBinder();
            Sql.Open();

            IQuerySection query;
            if (Sql.ExecuteScalar<int>(new QueryEngine().SelectFrom(binder.Target).Count().Where.Is(binder.PrimaryKey.Column, binder.PrimaryKey.Get())) == 0)
            {
                query = new QueryEngine().InsertInto(binder.Target);
                foreach (BindEntry bind in binder.Binds) (query as InsertKeyValueQuery).Value(bind.Column, bind.Get());
                (query as InsertKeyValueQuery).Value(binder.PrimaryKey.Column, binder.PrimaryKey.Get()).GetLastID();
                binder.PrimaryKey.Set(Sql.ExecuteScalar(query));
            }
            else
            {
                query = new QueryEngine().Update(binder.Target);
                foreach (BindEntry bind in binder.Binds) (query as UpdateKeyValueQuery).Value(bind.Column, bind.Get());
                (query as UpdateKeyValueQuery).Where.Is(binder.PrimaryKey.Column, binder.PrimaryKey.Get());
                Sql.ExecuteNonQuery(query);
            }

            Sql.Close();
        }
    }
}
