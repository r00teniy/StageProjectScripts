﻿using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace StageProjectScripts.Functions;
internal class SettingsStorage
{
    internal Variables ReadSettingsFromXML()
    {
        XmlSerializer serializer = new(typeof(Variables));
        Variables variables;
        using (XmlReader reader = XmlReader.Create(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\ProjectScriptSettings.xml"))
        {
            variables = (Variables)serializer.Deserialize(reader);
        }
        return variables;
    }
    internal void SaveData(string xRefName, string plotsName, string plotNumber)
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                if (xRefName != "")
                {
                    db.SetCustomProperty("Основа", xRefName);
                }
                if (plotsName != "")
                {
                    db.SetCustomProperty("Границы", plotsName);
                }
                if (plotNumber != "")
                {
                    db.SetCustomProperty("ГПЗУ", plotNumber);
                }
                tr.Commit();
            }
        }
    }
    internal string[] ReadData()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        string[] cityName = new string[3];
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                cityName[0] = db.GetCustomProperty("Основа");
                cityName[1] = db.GetCustomProperty("Границы");
                cityName[2] = db.GetCustomProperty("ГПЗУ");
                tr.Commit();
            }
        }
        return cityName;
    }
}
