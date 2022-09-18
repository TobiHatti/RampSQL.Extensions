using RampSQL.Binder;
using RampSQL.Query;
using System;

namespace RampSQL.Extensions
{
    public interface IRampLoadable : IRampBindable
    {
        IRampLoadable LoadFromPrimaryKey<T>(T ID);
        IRampLoadable LoadFromPrimaryKey<T>(T ID, Action<IRampLoadable> onFinishLoadingEvent);
        IRampLoadable LoadFromRamp(IQuerySection rampQuery);
        IRampLoadable LoadFromRamp(IQuerySection rampQuery, Action<IRampLoadable> onFinishLoadingEvent);
        IRampLoadable ExecuteLoad(IQuerySection query, Action<IRampLoadable> onFinishLoadingEvent = null, IRampBindable callingParent = null);
    }

    public interface IRampSaveable : IRampBindable
    {
        void SaveModel();
    }
}
