using RampSQL.Binder;
using RampSQL.Extensions;
using RampSQL.Schema;
using WrapSql;

namespace ExtTest
{
    internal class Program
    {
        class RDB : IRampSchema
        {
            public static RampHouses Houses = new RampHouses();
            [BindTable("Houses")]
            public class RampHouses : RampTable
            {
                [BindColumn("ID", typeof(int))]
                public RampColumn ID { get; set; }
                [BindColumn("Housename", typeof(string))]
                public RampColumn HouseName { get; set; }
                [BindColumn("HouseNumber", typeof(int))]
                public RampColumn HouseNumber { get; set; }
                [BindColumn("IsBungalow", typeof(bool))]
                public RampColumn IsBungalow { get; set; }
                [BindColumn("HouseType", typeof(bool))]
                public RampColumn HouseType { get; set; }

            }

            public static RampResidents Residents = new RampResidents();
            [BindTable("Residents")]
            public class RampResidents : RampTable
            {
                [BindColumn("ID", typeof(int))]
                public RampColumn ID { get; set; }
                [BindColumn("HouseID", typeof(int))]
                public RampColumn HouseID { get; set; }
                [BindColumn("Name", typeof(string))]
                public RampColumn Name { get; set; }
                [BindColumn("Age", typeof(int))]
                public RampColumn Age { get; set; }

            }

            public static RampPets Pets = new RampPets();
            [BindTable("Pets")]
            public class RampPets : RampTable
            {
                [BindColumn("ID", typeof(int))]
                public RampColumn ID { get; set; }
                [BindColumn("ResidentID", typeof(int))]
                public RampColumn ResidentID { get; set; }
                [BindColumn("PetName", typeof(string))]
                public RampColumn PetName { get; set; }

            }

            public static RampHouseTypes HouseTypes = new RampHouseTypes();
            [BindTable("HouseTypes")]
            public class RampHouseTypes : RampTable
            {
                [BindColumn("ID", typeof(string))]
                public RampColumn ID { get; set; }
                [BindColumn("DisplayText", typeof(string))]
                public RampColumn DisplayText { get; set; }

            }
        }

        public class HouseModel : ModelIOHandler
        {
            public int ID { get; set; }
            public string HouseName { get; set; }
            public int HouseNumber { get; set; }
            public bool IsBungalow { get; set; }
            public ResidentModel[] Resident { get; set; }
            public string HouseLabelThing { get; set; }

            public override RampModelBinder GetBinder()
            {
                return new RampModelBinder()
                    .SetTarget(RDB.Houses)
                    .LinkTable(RDB.Houses.HouseType, RDB.HouseTypes.ID)
                    .BindPrimaryKey(RDB.Houses.ID, () => ID, (e) => ID = e)
                    .Bind(RDB.Houses.HouseName, () => HouseName, (e) => HouseName = e)
                    .Bind(RDB.Houses.HouseNumber, () => HouseNumber, (e) => HouseNumber = e)
                    .Bind(RDB.Houses.IsBungalow, () => IsBungalow, (e) => IsBungalow = e)
                    .Bind(RDB.HouseTypes.DisplayText, () => HouseLabelThing, (e) => HouseLabelThing = e)
                    .ReferenceBind(RDB.Houses.ID, RDB.Residents.HouseID, () => Resident, (e) => Resident = e);
            }
        }

        public class ResidentModel : ModelIOHandler
        {
            public int ID { get; set; }
            public int HouseID { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }
            public PetsModel[] Pet { get; set; }

            public override RampModelBinder GetBinder()
            {
                return new RampModelBinder()
                    .SetTarget(RDB.Residents)
                    .BindPrimaryKey(RDB.Residents.ID, () => ID, (e) => ID = e)
                    .Bind(RDB.Residents.HouseID, () => HouseID, (e) => HouseID = e)
                    .Bind(RDB.Residents.Name, () => Name, (e) => Name = e)
                    .Bind(RDB.Residents.Age, () => Age, (e) => Age = e)
                    .ReferenceBind(RDB.Residents.ID, RDB.Pets.ResidentID, () => Pet, (e) => Pet = e);
            }
        }

        public class PetsModel : ModelIOHandler
        {
            public int ID { get; set; }
            public int ResidentID { get; set; }
            public string PetName { get; set; }

            public override RampModelBinder GetBinder()
            {
                return new RampModelBinder()
                    .SetTarget(RDB.Pets)
                    .BindPrimaryKey(RDB.Pets.ID, () => ID, (e) => ID = e)
                    .Bind(RDB.Pets.ResidentID, () => ResidentID, (e) => ResidentID = e)
                    .Bind(RDB.Pets.PetName, () => PetName, (e) => PetName = e);

            }
        }

        static void Main(string[] args)
        {

            ModelIOHandler.LinkDatabase<WrapMySql>(new WrapMySqlData("endev.at", "u94925db7", "u94925db7", "cola0815"));


            PetsModel pet = new PetsModel();
            pet.LoadFromPrimaryKey(1);


            ResidentModel resident = new ResidentModel();
            resident = (ResidentModel)resident.LoadFromPrimaryKey(2);


            HouseModel house = new HouseModel();
            house.LoadFromPrimaryKey(3);


            PetsModel[] pets = (PetsModel[])PetsModel.LoadAll<PetsModel>();
            ResidentModel[] residents = (ResidentModel[])ResidentModel.LoadAll<ResidentModel>();
            HouseModel[] houses = (HouseModel[])HouseModel.LoadAll<HouseModel>();

        }
    }
}
