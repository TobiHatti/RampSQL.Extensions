using Newtonsoft.Json;
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

        private RampModelBinder binder = null;

        [JsonIgnore]
        public RampModelBinder Binder
        {
            get
            {
                if (binder == null) binder = GetBinder();
                return binder;
            }
        }

        public static void LinkDatabase<T>(IWrapSqlConnector connector) where T : IWrapSql
        {
            WrapSqlType = typeof(T);
            ModelIOHandler.connector = connector;
        }

        public abstract RampModelBinder GetBinder();

        public IRampLoadable LoadFromKey<T>(RampColumn column, T key) => LoadFromRamp(SelectQueryBuilder().Where.Is(column, key));
        public IRampLoadable LoadFromKey<T>(RampColumn column, T key, Action<IRampLoadable> onFinishLoadingEvent) => LoadFromRamp(SelectQueryBuilder().Where.Is(column, key), onFinishLoadingEvent);
        public IRampLoadable LoadFromPrimaryKey<T>(T ID) => LoadFromRamp(SelectQueryBuilder().Where.Is(Binder.PrimaryKey.Column, ID));
        public IRampLoadable LoadFromPrimaryKey<T>(T ID, Action<IRampLoadable> onFinishLoadingEvent) => LoadFromRamp(SelectQueryBuilder().Where.Is(Binder.PrimaryKey.Column, ID), onFinishLoadingEvent);
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

        private static IRampLoadable[] ExecuteRangeLoad(IQuerySection query, Type targetType, Action<IRampLoadable[]> onFinishLoadingEvent = null, IRampBindable callingParent = null)
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

                    // Parent
                    if (binder.CallingParent != null && callingParent != null) binder.CallingParent.Set(callingParent);

                    foreach (BindEntry bind in binder.Binds)
                    {
                        // Primitives
                        if (bind.BindType == BindType.Primitive)
                            bind.Set(r[bind.Column]);

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
                        refModel.ExecuteLoad(SelectQueryBuilder(rq.ParentBind.Type).Where.Is(rq.ParentBind.ReferenceColumn, rq.RefLinkValue), null, rq.ParentBind.CallingParent);
                        break;
                    case BindType.ReferenceArray:
                        rq.ParentBind.Set(ExecuteRangeLoad(SelectQueryBuilder(rq.ParentBind.Type).Where.Is(rq.ParentBind.ReferenceColumn, rq.RefLinkValue), rq.ParentBind.Type, null, rq.ParentBind.CallingParent));
                        break;
                    case BindType.BindAll:
                        IRampLoadable baRefModel = rq.ParentBind.Get() as IRampLoadable;
                        baRefModel.ExecuteLoad(SelectQueryBuilder(rq.ParentBind.Type), null, rq.ParentBind.CallingParent);
                        break;
                    case BindType.BindAllArray:
                        rq.ParentBind.Set(ExecuteRangeLoad(SelectQueryBuilder(rq.ParentBind.Type), rq.ParentBind.Type, null, rq.ParentBind.CallingParent));
                        break;
                }
            }

            IRampLoadable[] resArray = (IRampLoadable[])Activator.CreateInstance(targetType.MakeArrayType(), models.Count);
            models.CopyTo(resArray, 0);

            foreach (var afterLoadEvent in afterLoadEventQueue) afterLoadEvent.Key(afterLoadEvent.Value);
            if (onFinishLoadingEvent != null) onFinishLoadingEvent(resArray);

            return resArray;
        }

        public IRampLoadable ExecuteLoad(IQuerySection query, Action<IRampLoadable> onFinishLoadingEvent = null, IRampBindable callingParent = null)
        {
            List<RefBindQueueEntry> refQueue = new List<RefBindQueueEntry>();
            List<KeyValuePair<Action<IRampBindable>, IRampBindable>> afterLoadEventQueue = new List<KeyValuePair<Action<IRampBindable>, IRampBindable>>();

            using (WrapSqlBase sql = (WrapSqlBase)Activator.CreateInstance(WrapSqlType, connector))
            {
                sql.Open();

                sql.ExecuteQuery(query).ReadAll((r) =>
                {
                    if (Binder.BeforeLoadEvent != null) Binder.BeforeLoadEvent(this);
                    if (Binder.AfterLoadEvent != null) afterLoadEventQueue.Add(new KeyValuePair<Action<IRampBindable>, IRampBindable>(Binder.AfterLoadEvent, this));

                    // Primary key
                    Binder.PrimaryKey.Set(r[Binder.PrimaryKey.Column]);

                    // Parent
                    if (Binder.CallingParent != null && callingParent != null) Binder.CallingParent.Set(callingParent);

                    foreach (BindEntry bind in Binder.Binds)
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
                        refModel.ExecuteLoad(SelectQueryBuilder(rq.ParentBind.Type).Where.Is(rq.ParentBind.ReferenceColumn, rq.RefLinkValue), null, rq.ParentBind.CallingParent);
                        break;
                    case BindType.ReferenceArray:
                        rq.ParentBind.Set(ExecuteRangeLoad(SelectQueryBuilder(rq.ParentBind.Type).Where.Is(rq.ParentBind.ReferenceColumn, rq.RefLinkValue), rq.ParentBind.Type, null, rq.ParentBind.CallingParent));
                        break;
                    case BindType.BindAll:
                        IRampLoadable baRefModel = rq.ParentBind.Get() as IRampLoadable;
                        baRefModel.ExecuteLoad(SelectQueryBuilder(rq.ParentBind.Type), null, rq.ParentBind.CallingParent);
                        break;
                    case BindType.BindAllArray:
                        rq.ParentBind.Set(ExecuteRangeLoad(SelectQueryBuilder(rq.ParentBind.Type), rq.ParentBind.Type, null, rq.ParentBind.CallingParent));
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
            SelectQuery query = new QueryEngine().SelectAllFrom(Binder.Target);
            foreach (TableLinkEntry tb in Binder.TableLinks) query.Join(tb.LocalColumn, tb.ReferenceColumn, tb.RefJoinType);
            return query;
        }







        public void SaveModel()
        {
            using (WrapSqlBase sql = (WrapSqlBase)Activator.CreateInstance(WrapSqlType, connector))
            {
                sql.Open();

                IQuerySection query;
                if (sql.ExecuteScalar<int>(new QueryEngine().SelectFrom(Binder.Target).Count().Where.Is(Binder.PrimaryKey.Column, Binder.PrimaryKey.Get())) == 0)
                {
                    query = new QueryEngine().InsertInto(Binder.Target);
                    foreach (BindEntry bind in Binder.Binds) (query as InsertKeyValueQuery).Value(bind.Column, bind.Get());
                    (query as InsertKeyValueQuery).Value(Binder.PrimaryKey.Column, Binder.PrimaryKey.Get()).GetLastID();
                    Binder.PrimaryKey.Set(sql.ExecuteScalar(query));
                }
                else
                {
                    query = new QueryEngine().Update(Binder.Target);
                    foreach (BindEntry bind in Binder.Binds) (query as UpdateKeyValueQuery).Value(bind.Column, bind.Get());
                    (query as UpdateKeyValueQuery).Where.Is(Binder.PrimaryKey.Column, Binder.PrimaryKey.Get());
                    sql.ExecuteNonQuery(query);
                }

                sql.Close();
            }
        }


    }
}
