using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using StageProjectScripts.Forms;
using StageProjectScripts.Functions;

[assembly: CommandClass(typeof(StageProjectScripts.MyCommands))]

namespace StageProjectScripts
{
    internal class MyCommands
    {
        [CommandMethod("ProjectScripts")]
        static public void StageProjectScripts()
        {
            var settingsStorage = new SettingsStorage();
            var variables = settingsStorage.ReadSettingsFromXML();
            variables.SavedData = settingsStorage.ReadData();
            var MW = new MainWindow(variables);
            MW.Show();
        }
        [CommandMethod("ProjectChecks")]
        static public void StageProjectChecks()
        {
            var settingsStorage = new SettingsStorage();
            var variables = settingsStorage.ReadSettingsFromXML();
            variables.SavedData = settingsStorage.ReadData();
            var CW = new ChecksWindow(variables);
            CW.Show();
        }
        [CommandMethod("TEST")]
        public static void Test()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions("\nSelect a polyline: ");
            peo.SetRejectMessage("Only a polyline !");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
                return;

            PromptEntityOptions peo1 = new PromptEntityOptions("\nSelect a polyline: ");
            peo.SetRejectMessage("Only a polyline !");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per1 = ed.GetEntity(peo1);
            if (per.Status != PromptStatus.OK)
                return;

            PromptEntityOptions peo2 = new PromptEntityOptions("\nSelect a polyline: ");
            peo.SetRejectMessage("Only a polyline !");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per2 = ed.GetEntity(peo2);
            if (per.Status != PromptStatus.OK)
                return;

            using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
            {

                Polyline pline = (Polyline)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                Polyline pline1 = (Polyline)tr.GetObject(per1.ObjectId, OpenMode.ForRead);
                Polyline pline2 = (Polyline)tr.GetObject(per2.ObjectId, OpenMode.ForRead);
                PromptPointOptions ppo = new PromptPointOptions("\nPick a point <quit>: ");
                double tolerance = Tolerance.Global.EqualPoint;
                MPolygon mpg = new MPolygon();
                //mpg.AppendLoopFromBoundary(pline, true, tolerance);
                //mpg.AppendLoopFromBoundary(pline1, true, tolerance);
                //mpg.AppendLoopFromBoundary(pline2, true, tolerance);
                mpg.CreateLoopsFromBoundaries(new ObjectIdCollection() { pline.ObjectId, pline1.ObjectId, pline2.ObjectId }, true, tolerance);
                var bT = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bT[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ppo.AllowNone = true;
                while (true)
                {
                    PromptPointResult ppr = ed.GetPoint(ppo);
                    if (ppr.Status != PromptStatus.OK)
                        break;
                    if (mpg.NumMPolygonLoops > 1)
                    {
                        for (var i = 0; i < mpg.NumMPolygonLoops; i++)
                        {
                            if (mpg.IsPointOnLoopBoundary(ppr.Value, i, tolerance))
                                Application.ShowAlertDialog("on Boundary");
                        }
                    }
                    else
                    {
                        if (mpg.IsPointOnLoopBoundary(ppr.Value, 0, tolerance))
                            Application.ShowAlertDialog("on Boundary");
                    }
                    bool inside = false;
                    if (mpg.IsPointInsideMPolygon(ppr.Value, Tolerance.Global.EqualPoint).Count > 0)
                    {
                        if (mpg.NumMPolygonLoops <= 1) inside = true;
                        else
                        {
                            int inslooop = 0;
                            for (int i = 0; i < mpg.NumMPolygonLoops; i++)
                            {
                                using (MPolygon mp = new MPolygon())
                                {
                                    mp.AppendMPolygonLoop(mpg.GetMPolygonLoopAt(i), false, tolerance);
                                    if (mp.IsPointInsideMPolygon(ppr.Value, tolerance).Count > 0) inslooop++;
                                }
                            }
                            if (inslooop % 2 > 0) inside = true;
                        }
                    }
                    Application.ShowAlertDialog(inside ? "Inside" : "Outside");

                }
                tr.Commit();
            }
        }
    }
}