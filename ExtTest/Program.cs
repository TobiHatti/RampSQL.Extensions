using RampSQL.Extensions;
using RampSQL.Query;
using RampSQL.Reader;
using RampSQL.Schema;
using RampSQL.Search;
using WrapSQL;

namespace ExtTest
{
    internal class Program
    {
        class RDB : IRampSchema
        {

            public static RampTable_Data Data = new RampTable_Data();
            [BindTable("data")]
            public class RampTable_Data : RampTable
            {
                [BindColumn("a", typeof(string))]
                public RampColumn Name { get; set; }
                [BindColumn("b", typeof(string))]
                public RampColumn Description { get; set; }

            }
        }

        static void Main(string[] args)
        {
            using (WrapMySQL sql = new WrapMySQL("", "", "", ""))
            {
                sql.Open();
                sql.RampNoQuery(new QueryEngine().SelectFrom(""));

                using (RampReader reader = sql.RampQuery(new QueryEngine().SelectFrom("").Where.Is(null, "")))
                {
                    while (reader.Read())
                    {
                        int i = reader.Get<int>(RDB.Data.Name);
                    }
                }

                sql.Close();

                SearchEngine engine = null;

                engine.Connector()
            }


        }
    }
}
