using RampSQL.Binder;
using RampSQL.Query;
using RampSQL.Schema;
using System;
using System.Collections;
using System.Collections.Generic;
using WrapSql;
using static RampSQL.Binder.RampModelBinder;

namespace RampSQL.Extensions
{
    public abstract class ModelIOHandler : IRampLoadable, IRampSaveable
    {
        private static Type WrapSqlType = null;
        private static IWrapSqlConnector connector;

        public static void LinkDatabase<T>(IWrapSqlConnector connector) where T : IWrapSql
        {
            WrapSqlType = typeof(T);
            ModelIOHandler.connector = connector;
        }

        public abstract RampModelBinder GetBinder();

        public IRampLoadable LoadFromPrimaryKey<T>(T ID) => LoadFromRamp(SelectQueryBuilder().Where.Is(GetBinder().PrimaryKey.Column, ID));
        public IRampLoadable LoadFromPrimaryKey<T>(T ID, Action<IRampLoadable> onFinishLoadingEvent) => LoadFromRamp(SelectQueryBuilder().Where.Is(GetBinder().PrimaryKey.Column, ID), onFinishLoadingEvent);
        public IRampLoadable LoadFromRamp(IQuerySection rampQuery) => ExecuteLoad(rampQuery);
        public IRampLoadable LoadFromRamp(IQuerySection rampQuery, Action<IRampLoadable> onFinishLoadingEvent) => ExecuteLoad(rampQuery, onFinishLoadingEvent);

        public static IRampLoadable[] LoadAll<T>() where T : IRampLoadable => LoadRangeFromRamp<T>(SelectQueryBuilder<T>());
        public static IRampLoadable[] LoadAll<T>(Action<IRampLoadable[]> onFinishLoadingEvent) where T : IRampLoadable => LoadRangeFromRamp<T>(SelectQueryBuilder<T>(), onFinishLoadingEvent);
        public static IRampLoadable[] LoadRange<T>(RampColumn column, params object[] values) where T : IRampLoadable => LoadRangeFromRamp<T>(SelectQueryBuilder<T>().Where.In(column, values));
        public static IRampLoadable[] LoadRange<T>(RampColumn column, Action<IRampLoadable[]> onFinishLoadingEvent, params object[] values) where T : IRampLoadable => LoadRangeFromRamp<T>(SelectQueryBuilder<T>().Where.In(column, values), onFinishLoadingEvent);
        public static IRampLoadable[] LoadRangeFromRamp<T>(IQuerySection rampQuery) where T : IRampLoadable => ExecuteRangeLoad(rampQuery, typeof(T));
        public static IRampLoadable[] LoadRangeFromRamp<T>(IQuerySection rampQuery, Action<IRampLoadable[]> onFinishLoadingEvent) where T : IRampLoadable => ExecuteRangeLoad(rampQuery, typeof(T), onFinishLoadingEvent);
        public static IRampLoadable[] LoadRangeFromRamp(IQuerySection rampQuery, Type targetType) => ExecuteRangeLoad(rampQuery, targetType);
        public static IRampLoadable[] LoadRangeFromRamp(IQuerySection rampQuery, Type targetType, Action<IRampLoadable[]> onFinishLoadingEvent) => ExecuteRangeLoad(rampQuery, targetType, onFinishLoadingEvent);

        private class RefBindQueueEntry
        {
            public object RefLinkValue { get; set; }
            public BindEntry ParentBind { get; set; }
        }

        private static IRampLoadable[] ExecuteRangeLoad(IQuerySection query, Type targetType, Action<IRampLoadable[]> onFinishLoadingEvent = null)
        {
            List<RefBindQueueEntry> refQueue = new List<RefBindQueueEntry>();
            List<KeyValuePair<Action<IRampBindable>, IRampBindable>> afterLoadEventQueue = new List<KeyValuePair<Action<IRampBindable>, IRampBindable>>();
            Type genericListType = typeof(List<>).MakeGenericType(targetType);
            IList models = (IList)Activator.CreateInstance(genericListType);

            using (WrapSqlBase sql = (WrapSqlBase)Activator.CreateInstance(WrapSqlType, connector))
            {
                sql.Open();

                sql.ExecuteQuery(query).ReadAll((r) =>
                {
                    IRampLoadable model = (IRampLoadable)Activator.CreateInstance(targetType);
                    RampModelBinder binder = model.GetBinder();

                    if (binder.BeforeLoadEvent != null) binder.BeforeLoadEvent(model);
                    if (binder.AfterLoadEvent != null) afterLoadEventQueue.Add(new KeyValuePair<Action<IRampBindable>, IRampBindable>(binder.AfterLoadEvent, model));

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

                            if (bind.BindType == BindType.Reference)
                            {
                                refQueue.Add(new RefBindQueueEntry()
                                {
                                    RefLinkValue = r[bind.Column],
                                    ParentBind = bind
                                });
                            }

                            if (bind.BindType == BindType.BindAll)
                            {
                                refQueue.Add(new RefBindQueueEntry()
                                {
                                    RefLinkValue = null,
                                    ParentBind = bind
                                });
                            }
                        }

                        if (bind.BindType == BindType.ReferenceArray)
                        {
                            refQueue.Add(new RefBindQueueEntry()
                            {
                                RefLinkValue = r[bind.Column],
                                ParentBind = bind
                            });
                        }

                        if (bind.BindType == BindType.BindAllArray)
                        {
                            refQueue.Add(new RefBindQueueEntry()
                            {
                                RefLinkValue = null,
                                ParentBind = bind
                            });
                        }
                    }

                    models.Add(model);

                });
                sql.Close();
            }

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

            foreach (var afterLoadEvent in afterLoadEventQueue) afterLoadEvent.Key(afterLoadEvent.Value);
            if (onFinishLoadingEvent != null) onFinishLoadingEvent(resArray);

            return resArray;
        }

        private IRampLoadable ExecuteLoad(IQuerySection query, Action<IRampLoadable> onFinishLoadingEvent = null)
        {
            List<RefBindQueueEntry> refQueue = new List<RefBindQueueEntry>();
            List<KeyValuePair<Action<IRampBindable>, IRampBindable>> afterLoadEventQueue = new List<KeyValuePair<Action<IRampBindable>, IRampBindable>>();

            using (WrapSqlBase sql = (WrapSqlBase)Activator.CreateInstance(WrapSqlType, connector))
            {
                sql.Open();

                sql.ExecuteQuery(query).ReadAll((r) =>
                {
                    RampModelBinder binder = GetBinder();

                    if (binder.BeforeLoadEvent != null) binder.BeforeLoadEvent(this);
                    if (binder.AfterLoadEvent != null) afterLoadEventQueue.Add(new KeyValuePair<Action<IRampBindable>, IRampBindable>(binder.AfterLoadEvent, this));

                    // Primary key
                    binder.PrimaryKey.Set(r[binder.PrimaryKey.Column]);

                    foreach (BindEntry bind in binder.Binds)
                    {
                        // Primitives
                        if (bind.BindType == BindType.Primitive)
                        {
                            if (bind.CustomReader == null) bind.Set(r[bind.Column]);
                            else bind.Set(bind.CustomReader(r, bind.Column));
                        }


                        // Activate Reference
                        if (bind.BindType == BindType.Reference || bind.BindType == BindType.BindAll)
                        {
                            IRampLoadable referenceModel = (IRampLoadable)Activator.CreateInstance(bind.Type);
                            bind.Set(referenceModel);

                            if (bind.BindType == BindType.Reference)
                            {
                                refQueue.Add(new RefBindQueueEntry()
                                {
                                    RefLinkValue = r[bind.Column],
                                    ParentBind = bind
                                });
                            }

                            if (bind.BindType == BindType.BindAll)
                            {
                                refQueue.Add(new RefBindQueueEntry()
                                {
                                    RefLinkValue = null,
                                    ParentBind = bind
                                });
                            }
                        }

                        if (bind.BindType == BindType.ReferenceArray)
                        {
                            refQueue.Add(new RefBindQueueEntry()
                            {
                                RefLinkValue = r[bind.Column],
                                ParentBind = bind
                            });
                        }

                        if (bind.BindType == BindType.BindAllArray)
                        {
                            refQueue.Add(new RefBindQueueEntry()
                            {
                                RefLinkValue = null,
                                ParentBind = bind
                            });
                        }
                    }
                });
                sql.Close();
            }

            foreach (RefBindQueueEntry rq in refQueue)
            {
                switch (rq.ParentBind.BindType)
                {
                    case BindType.Reference:
                        IRampLoadable refModel = rq.ParentBind.Get() as IRampLoadable;
                        refModel.LoadFromRamp(SelectQueryBuilder(rq.ParentBind.Type).Where.Is(rq.ParentBind.ReferenceColumn, rq.RefLinkValue));
                        break;
                    case BindType.ReferenceArray:
                        rq.ParentBind.Set(LoadRangeFromRamp(SelectQueryBuilder(rq.ParentBind.Type).Where.Is(rq.ParentBind.ReferenceColumn, rq.RefLinkValue), rq.ParentBind.Type));
                        break;
                    case BindType.BindAll:
                        IRampLoadable baRefModel = rq.ParentBind.Get() as IRampLoadable;
                        baRefModel.LoadFromRamp(SelectQueryBuilder(rq.ParentBind.Type));
                        break;
                    case BindType.BindAllArray:
                        rq.ParentBind.Set(LoadRangeFromRamp(SelectQueryBuilder(rq.ParentBind.Type), rq.ParentBind.Type));
                        break;
                }
            }

            foreach (var afterLoadEvent in afterLoadEventQueue) afterLoadEvent.Key(afterLoadEvent.Value);
            if (onFinishLoadingEvent != null) onFinishLoadingEvent(this);

            return this;
        }

        private static JoinQuery SelectQueryBuilder<T>() where T : IRampLoadable
        {
            T instance = (T)Activator.CreateInstance(typeof(T));
            RampModelBinder binder = instance.GetBinder();
            SelectQuery query = new QueryEngine().SelectAllFrom(binder.Target);
            foreach (TableLinkEntry tb in binder.TableLinks) query.Join(tb.LocalColumn, tb.ReferenceColumn, tb.RefJoinType);
            return query;
        }

        private static JoinQuery SelectQueryBuilder(Type targetType)
        {
            IRampLoadable instance = (IRampLoadable)Activator.CreateInstance(targetType);
            RampModelBinder binder = instance.GetBinder();
            SelectQuery query = new QueryEngine().SelectAllFrom(binder.Target);
            foreach (TableLinkEntry tb in binder.TableLinks) query.Join(tb.LocalColumn, tb.ReferenceColumn, tb.RefJoinType);
            return query;
        }

        private JoinQuery SelectQueryBuilder()
        {
            RampModelBinder binder = GetBinder();
            SelectQuery query = new QueryEngine().SelectAllFrom(binder.Target);
            foreach (TableLinkEntry tb in binder.TableLinks) query.Join(tb.LocalColumn, tb.ReferenceColumn, tb.RefJoinType);
            return query;
        }

        public void SaveModel()
        {
            RampModelBinder binder = GetBinder();

            using (WrapSqlBase sql = (WrapSqlBase)Activator.CreateInstance(WrapSqlType, connector))
            {
                sql.Open();

                IQuerySection query;
                if (sql.ExecuteScalar<int>(new QueryEngine().SelectFrom(binder.Target).Count().Where.Is(binder.PrimaryKey.Column, binder.PrimaryKey.Get())) == 0)
                {
                    query = new QueryEngine().InsertInto(binder.Target);
                    foreach (BindEntry bind in binder.Binds) (query as InsertKeyValueQuery).Value(bind.Column, bind.Get());
                    (query as InsertKeyValueQuery).Value(binder.PrimaryKey.Column, binder.PrimaryKey.Get()).GetLastID();
                    binder.PrimaryKey.Set(sql.ExecuteScalar(query));
                }
                else
                {
                    query = new QueryEngine().Update(binder.Target);
                    foreach (BindEntry bind in binder.Binds) (query as UpdateKeyValueQuery).Value(bind.Column, bind.Get());
                    (query as UpdateKeyValueQuery).Where.Is(binder.PrimaryKey.Column, binder.PrimaryKey.Get());
                    sql.ExecuteNonQuery(query);
                }

                sql.Close();
            }
        }
    }
}
