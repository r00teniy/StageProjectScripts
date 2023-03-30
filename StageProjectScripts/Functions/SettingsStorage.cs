using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.ComponentModel;
using System.IO;

using System.Xml.Serialization;

namespace StageProjectScripts.Functions;
internal static class SettingsStorage
{
    private static string fileName = "CityParameters.xml";
    private static XmlSerializer serializer = new(typeof(BindingList<CityModel>));
    internal static void SaveCityParameters()
    {
        StreamWriter myWriter = new(fileName);
        serializer.Serialize(myWriter, Variables.cityList);
        myWriter.Close();
    }
    internal static void ReadCityParameters()
    {
        if (File.Exists(fileName))
        {
            using (Stream reader = new FileStream(fileName, FileMode.Open))
            {
                Variables.cityList = (BindingList<CityModel>)serializer.Deserialize(reader);
            }
        }
    }
    internal static void SaveCity(string cityName)
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                db.SetCustomProperty("Город", cityName);
                tr.Commit();
            }
        }
    }
    internal static string ReadCity()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        string cityName = "";
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                cityName = db.GetCustomProperty("Город");
                tr.Commit();
            }
        }
        return cityName;
    }
}
