using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;



namespace StageProjectScripts.Functions;

internal class DataExport
{
    
    internal static bool SetBlockAttribute(BlockReference block, string attrName, string attrValue)
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        var output = new List<Dictionary<string, string>>();
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                //var br = (BlockReference)tr.GetObject(block.ObjectId, OpenMode.ForWrite);
                foreach (ObjectId id in block.AttributeCollection)
                {
                    // open the attribute reference
                    var attRef = (AttributeReference)tr.GetObject(id, OpenMode.ForRead);
                    //Checking fot tag & setting value
                    if (attRef.Tag == attrName)
                    {
                        attRef.UpgradeOpen();
                        attRef.TextString = attrValue;
                        tr.Commit();
                        return true;
                    }
                }
            }
            tr.Commit();
        }
        return false;
    }
}
