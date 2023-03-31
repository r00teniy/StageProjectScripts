using System;
using System.Linq;
using System.Reflection;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using Autodesk.AutoCAD.EditorInput;

namespace StageProjectScripts.Functions
{
    internal static class TemporaryCode
    {
        public static void DoCount(string X, int a, int b, int c)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            string selectedOSN = X;
            XrefGraph XrGraph = db.GetHostDwgXrefGraph(false);
            ObjectId idh = db.GetObjectId(false, new Handle(Variables.th), 0); // Getting table object 
            ObjectId idpl = db.GetObjectId(false, new Handle(Variables.tpl), 0);
            ObjectId idpa = db.GetObjectId(false, new Handle(Variables.tpa), 0);
            ObjectId idb = db.GetObjectId(false, new Handle(Variables.tb), 0);
            double[] hatchValues = new double[Variables.laylistHatch.Length];
            string[] hatchErrors = new string[Variables.laylistHatch.Length];
            double[] plineLengthValues = new double[Variables.laylistPlL.Length];
            double[] plineAreaValues = new double[Variables.laylistPlA.Length];
            string[] plineLengthErrors = new string[Variables.laylistPlL.Length];
            string[] plineAreaErrors = new string[Variables.laylistPlA.Length];
            double[] blockValues = new double[10];
            string[] blockErrors = new string[10];
            int[] PL_count = { a, b, c, 1, 1 };

            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                OSNOVA = XrGraph.GetXrefNode(0);
                for (int i = 1; i < XrGraph.NumNodes; i++)
                {
                    XrefGraphNode XrNode = XrGraph.GetXrefNode(i);
                    if (XrNode.Name == selectedOSN)
                    {
                        OSNOVA = XrNode;
                    }
                }
                using (DocumentLock acLckDoc = doc.LockDocument())
                {
                    var blockTable = trans.GetObject(db.BlockTableId, OpenMode.ForRead, false) as BlockTable;
                    var blocktableRecord = trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite, false) as BlockTableRecord;
                    var blocktableRecordOSN = trans.GetObject(OSNOVA.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
                    foreach (ObjectId objectId in blocktableRecordOSN)
                    {
                        if (objectId.ObjectClass == rxClassPoly)
                        {
                            var poly = trans.GetObject(objectId, OpenMode.ForRead) as Polyline;
                            for (int i = 0; i < laylistPlL.Length; i++)
                            {
                                if ((poly.Layer == selectedOSN + "|" + laylistPlL[i]) && poly != null)
                                {
                                    plineLengthValues[i] += poly.GetDistanceAtParameter(poly.EndParam) / PL_count[i];
                                }
                            }
                            for (int i = 0; i < laylistPlA.Length; i++)
                            {
                                if ((poly.Layer == selectedOSN + "|" + laylistPlA[i]) && poly != null)
                                {
                                    // Getting from AcadObject for cases of selfintersection
                                    object pl = poly.AcadObject;
                                    plineAreaValues[i] += (double)pl.GetType().InvokeMember("Area", BindingFlags.GetProperty, null, pl, null);
                                }
                            }
                        }
                        if (objectId.ObjectClass == rxClassHatch)
                        {
                            var hat = trans.GetObject(objectId, OpenMode.ForRead) as Hatch;
                            for (int i = 0; i < laylistHatch.Length; i++)
                            {
                                if ((hat.Layer == selectedOSN + "|" + laylistHatch[i]) && hat != null)
                                {
                                    try
                                    {
                                        hatchValues[i] += hat.Area;
                                    }
                                    catch
                                    {
                                        hatchErrors[i] = "Самопересечение";
                                        //changing to count self-intersecting hatches
                                        Plane plane = hat.GetPlane();
                                        int nLoops = hat.NumberOfLoops;
                                        double corArea = 0.0;
                                        for (int k = 0; k < nLoops; k++)
                                        {
                                            HatchLoop loop = hat.GetLoopAt(k);
                                            HatchLoopTypes hlt = hat.LoopTypeAt(k);
                                            Polyline looparea = SelfIntArea(loop, plane);
                                            // Can get correct value from AcadObject, but need to add in first
                                            blocktableRecord.AppendEntity(looparea);
                                            trans.AddNewlyCreatedDBObject(looparea, true);
                                            object pl = looparea.AcadObject;
                                            var corrval = (double)pl.GetType().InvokeMember("Area", BindingFlags.GetProperty, null, pl, null);
                                            looparea.Erase(); // Erasing polyline we just created
                                            if (hlt == HatchLoopTypes.External) // External loops with +
                                            {
                                                corArea += corrval;
                                            }
                                            else // Internal with -
                                            {
                                                corArea -= corrval;
                                            }
                                        }
                                        hatchValues[i] += corArea;
                                    }
                                }
                            }
                        }

                    }
                    //Counting tactile indicators
                    foreach (ObjectId objectId in blocktableRecord)
                    {
                        if (objectId.ObjectClass == rxClassBlockReference)
                        {
                            var br = trans.GetObject(objectId, OpenMode.ForRead) as BlockReference;
                            if (br.Layer == laylistBlock[2])
                            {
                                if (br != null && br.IsDynamicBlock)
                                {
                                    DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
                                    for (int i = 0; i < tactileNames.Length; i++)
                                    {
                                        if (tactileNames[i] == pc[0].Value.ToString())
                                        {
                                            if (tactileNames[i] == "Линии вдоль")
                                            {
                                                blockValues[2 + i] += Convert.ToDouble(pc[1].Value) / 0.6; // first 2 are same, but rotated 90
                                            }
                                            else if (tactileNames[i] == "2 Линии")
                                            {
                                                blockValues[i] += Convert.ToDouble(pc[1].Value) / 0.3; // 1 line and 2 line are same
                                            }
                                            else if (tactileNames[i] == "Шуцлиния")
                                            {
                                                blockValues[i] += Convert.ToDouble(pc[1].Value) / 0.6;
                                            }
                                            else
                                            {
                                                blockValues[1 + i] += Convert.ToDouble(pc[1].Value) / 0.6;
                                            }
                                        }
                                    }
                                }
                            }

                        }
                    }
                    // Rounding curbs
                    for (int i = 2; i < plineLengthValues.Length; i++)
                    {
                        plineLengthValues[i] = Math.Ceiling(plineLengthValues[i]);
                    }
                    // Counting blocks
                    blockValues[0] = GetBlocksPosition(blocktableRecord, trans, laylistBlock[0]).Count;
                    blockValues[1] = GetBlocksPosition(blocktableRecord, trans, laylistBlock[1]).Count;
                    // Checking hatches
                    for (int i = 0; i < hatchValues.Length; i++)
                    {
                        if (hatchValues[i] == 0)
                        {
                            hatchErrors[i] = "Нет Элементов";
                        }
                        if (hatchValues[i] > 0 && hatchErrors[i] != "Самопересечение")
                        {
                            hatchErrors[i] = "Всё в порядке";
                        }
                    }
                    // Checking polylines
                    for (int i = 0; i < plineLengthValues.Length; i++)
                    {
                        if (plineLengthValues[i] == 0)
                        {
                            plineLengthErrors[i] = "Нет Элементов";
                        }
                        if (plineLengthValues[i] > 0)
                        {
                            plineLengthErrors[i] = "Всё в порядке";
                        }
                    }
                    for (int i = 0; i < plineAreaValues.Length; i++)
                    {
                        if (plineAreaValues[i] == 0)
                        {
                            plineAreaErrors[i] = "Нет Элементов";
                        }
                        if (plineAreaValues[i] > 0)
                        {
                            plineAreaErrors[i] = "Всё в порядке";
                        }
                    }
                    // Checking blocks
                    for (int i = 0; i < blockValues.Length; i++)
                    {
                        if (blockValues[i] == 0)
                        {
                            blockErrors[i] = "Нет Элементов";
                        }
                        if (blockValues[i] > 0)
                        {
                            blockErrors[i] = "Всё в порядке";
                        }
                    }
                    // Rounding pline length
                    for (int i = 0; i < plineLengthValues.Length; i++)
                    {
                        plineLengthValues[i] = Math.Ceiling(plineLengthValues[i]);
                    }
                    // Filling hatch table
                    var tablH = trans.GetObject(idh, OpenMode.ForWrite) as Table;
                    for (int i = 0; i < hatchValues.Length; i++)
                    {
                        tablH.SetTextString(2 + i, 1, hatchValues[i].ToString("0.##"));
                        tablH.SetTextString(2 + i, 2, hatchErrors[i]);
                    }
                    // Filling polyline table (length)
                    var tablP = trans.GetObject(idpl, OpenMode.ForWrite) as Table;
                    for (int i = 0; i < plineLengthValues.Length; i++)
                    {
                        tablP.SetTextString(2 + i, 1, plineLengthValues[i].ToString("0"));
                        tablP.SetTextString(2 + i, 2, plineLengthErrors[i]);
                    }
                    // Filling polyline table (area)
                    var tablA = trans.GetObject(idpa, OpenMode.ForWrite) as Table;
                    for (int i = 0; i < plineAreaValues.Length; i++)
                    {
                        tablA.SetTextString(2 + i, 1, plineAreaValues[i].ToString("0.##"));
                        tablA.SetTextString(2 + i, 2, plineAreaErrors[i]);
                    }
                    // Filling block table
                    var tablB = trans.GetObject(idb, OpenMode.ForWrite) as Table;
                    for (int i = 0; i < blockValues.Length; i++)
                    {
                        tablB.SetTextString(2 + i, 1, blockValues[i].ToString("0.##"));
                        tablB.SetTextString(2 + i, 2, blockErrors[i]);
                    }
                    trans.Commit();
                }
            }
        }
    }
}
