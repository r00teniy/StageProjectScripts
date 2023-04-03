using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using StageProjectScripts.Models;

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
    internal static void FillTableWithData(Transaction tr, List<DataElementModel> hatches, long tableHandle, int numberOfLines, string format)
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        //Getting table objects
        ObjectId id = db.GetObjectId(false, new Handle(tableHandle), 0);
        //Fillling table
        var tabl = tr.GetObject(id, OpenMode.ForWrite) as Table;
        for (int i = 0; i < numberOfLines; i++)
        {
            tabl.Cells[2 + i, 1].TextString = hatches.Where(x => (x.NumberInTable == i) && x.IsInsidePlot).Select(x => x.Amount).Sum().ToString(format);
            tabl.Cells[2 + i, 2].TextString = hatches.Where(x => (x.NumberInTable == i) && !x.IsInsidePlot).Select(x => x.Amount).Sum().ToString(format);
        }
    }
}
