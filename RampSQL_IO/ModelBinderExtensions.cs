using RampSQL.Binder;
using System;

namespace RampSQL.Extensions
{
    public static class ModelBinderExtensions
    {


        public static RampModelBinder BeforeLoad(this RampModelBinder binder, Action<IRampBindable> beforeLoadEvent)
        {
            binder.BeforeLoadEvent = beforeLoadEvent;
            return binder;
        }

        public static RampModelBinder AfterLoad(this RampModelBinder binder, Action<IRampBindable> afterLoadEvent)
        {
            binder.AfterLoadEvent = afterLoadEvent;
            return binder;
        }
    }
}
