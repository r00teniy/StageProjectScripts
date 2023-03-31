using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace StageProjectScripts.Functions
{
    internal static class DataProcessing
    {
        internal static void CalculateVolumes(string xRef, int a, int b, int c)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            // Getting table objects 
            ObjectId idh = db.GetObjectId(false, new Handle(Variables.th), 0);
            ObjectId idpl = db.GetObjectId(false, new Handle(Variables.tpl), 0);
            ObjectId idpa = db.GetObjectId(false, new Handle(Variables.tpa), 0);
            ObjectId idb = db.GetObjectId(false, new Handle(Variables.tb), 0);
            int[] PL_count = { a, b, c, 1, 1 };
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {

                }
            }
        }
    }
}
