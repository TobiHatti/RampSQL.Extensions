using RampSQL.Binder;
using RampSQL.Query;

namespace RampSQL.Extensions
{
    public interface IRampLoadable : IRampBindable
    {
        IRampLoadable LoadFromPrimaryKey<T>(T ID);
        IRampLoadable LoadFromRamp(IQuerySection rampQuery);
    }

    public interface IRampSaveable : IRampBindable
    {
        void SaveModel();
    }
}
