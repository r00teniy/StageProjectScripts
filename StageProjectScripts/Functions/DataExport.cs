using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using StageProjectScripts.Models;

namespace StageProjectScripts.Functions;

internal class DataExport
{
    internal void CreateTempLine(Variables variables, Transaction tr, List<(Point3d, Point3d)> pts)
    {
        //temporary solution
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        var bT = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bT[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        foreach (var pt in pts)
        {
            LayerCheck(tr, variables.TempLayer, Color.FromColorIndex(ColorMethod.ByAci, variables.TempLayerColor), variables.TempLayerLineWeight, variables.TempLayerPrintable);
            var (ptStart, ptEnd) = pt;
            using (Line acLine = new())
            {
                acLine.Color = Color.FromColorIndex(ColorMethod.ByAci, variables.TempLayerColor);
                acLine.Layer = variables.TempLayer;
                acLine.StartPoint = ptStart;
                acLine.EndPoint = ptEnd;
                // Add the new object to the block table record and the transaction
                btr.AppendEntity(acLine);
                tr.AddNewlyCreatedDBObject(acLine, true);
            }
        }
    }
    internal bool SetBlockAttribute(BlockReference block, string attrName, string attrValue)
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
    //Creating Mleader with text
    internal void CreateMleaderWithText(Transaction tr, List<string> texts, List<Point3d> pts, string layer)
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        var bT = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bT[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        //creating Mleaders
        for (int i = 0; i < texts.Count; i++)
        {
            //createing Mleader
            MLeader leader = new();
            leader.SetDatabaseDefaults();
            leader.ContentType = ContentType.MTextContent;
            leader.Layer = layer;
            MText mText = new();
            mText.SetDatabaseDefaults();
            mText.Width = 0.675;
            mText.Height = 1.25;
            mText.TextHeight = 1.25;
            mText.SetContentsRtf(texts[i]);
            mText.Location = new Point3d(pts[i].X + 2, pts[i].Y + 2, pts[i].Z);
            mText.Rotation = 0;
            mText.BackgroundFill = true;
            mText.BackgroundScaleFactor = 1.1;
            leader.MText = mText;
            _ = leader.AddLeaderLine(pts[i]);
            btr.AppendEntity(leader);
            tr.AddNewlyCreatedDBObject(leader, true);
        }

    }
    //Function to clear temporary geometry
    public void ClearTemp(Variables variables)
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                var blockTable = tr.GetObject(db.BlockTableId, OpenMode.ForRead, false) as BlockTable;
                var btr = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite, false) as BlockTableRecord;
                foreach (ObjectId objectId in btr)
                {
                    var obj = tr.GetObject(objectId, OpenMode.ForWrite, false, true) as Entity;
                    if (obj.Layer == variables.TempLayer) // Checking for temporary layer
                    {
                        obj.Erase();
                    }
                }
            }
            tr.Commit();
        }
    }
    //Function to check if layer exist and create if not
    public void LayerCheck(Transaction tr, string layer, Color color, double lw, Boolean isPlottable)
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
        if (!lt.Has(layer))
        {
            var newLayer = new LayerTableRecord
            {
                Name = layer,
                Color = color,
                LineWeight = (LineWeight)(lw * 100),
                IsPlottable = isPlottable
            };

            lt.UpgradeOpen();
            lt.Add(newLayer);
            tr.AddNewlyCreatedDBObject(newLayer, true);
        }
    }
    internal void CreateMleaderWithBlockForGroupOfobjects(Transaction tr, List<List<Point3d>> pointList, string id, string style, string layer, string blockName, string[] attr)
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        Editor ed = doc.Editor;
        BlockTable bT = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bT[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        //Getting rotation of current UCS to pass it to block
        Matrix3d UCS = ed.CurrentUserCoordinateSystem;
        CoordinateSystem3d cs = UCS.CoordinateSystem3d;
        double rotAngle = cs.Xaxis.AngleOnPlane(new Plane(Point3d.Origin, Vector3d.ZAxis));
        //Creating Mleaders for each group
        foreach (var group in pointList)
        {
            DBDictionary mlStyles = tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead) as DBDictionary;
            ObjectId mlStyleId = mlStyles.GetAt(style);
            var leader = new MLeader();
            leader.SetDatabaseDefaults();
            leader.MLeaderStyle = mlStyleId;
            leader.ContentType = ContentType.BlockContent;
            leader.Layer = layer;
            leader.BlockContentId = bT[blockName];
            leader.BlockPosition = new Point3d(group[0].X + 5, group[0].Y + 5, 0);
            leader.BlockRotation = rotAngle;
            int idx = leader.AddLeaderLine(group[0]);
            // adding leader points for each element
            // TODO: temporary solution, need sorting algorithm for better performance.
            if (group.Count > 1)
            {
                foreach (Point3d m in group)
                {
                    leader.AddFirstVertex(idx, m);
                }
            }
            //Handle Block Attributes
            BlockTableRecord blkLeader = tr.GetObject(leader.BlockContentId, OpenMode.ForRead) as BlockTableRecord;
            //Doesn't take in consideration oLeader.BlockRotation
            Matrix3d Transfo = Matrix3d.Displacement(leader.BlockPosition.GetAsVector());
            foreach (ObjectId blkEntId in blkLeader)
            {
                AttributeDefinition AttributeDef = tr.GetObject(blkEntId, OpenMode.ForRead) as AttributeDefinition;
                if (AttributeDef != null)
                {
                    AttributeReference AttributeRef = new();
                    AttributeRef.SetAttributeFromBlock(AttributeDef, Transfo);
                    AttributeRef.Position = AttributeDef.Position.TransformBy(Transfo);
                    // setting attributes
                    if (AttributeRef.Tag == attr[0])
                    {
                        AttributeRef.TextString = id;
                    }
                    if (AttributeRef.Tag == attr[1])
                    {
                        AttributeRef.TextString = group.Count.ToString();
                    }
                    leader.SetBlockAttribute(blkEntId, AttributeRef);
                }
            }
            // adding Mleader to blocktablerecord
            btr.AppendEntity(leader);
            tr.AddNewlyCreatedDBObject(leader, true);
        }
    }
    internal void FillTableWithData(Transaction tr, List<DataElementModel> hatches, string tableHandle, int numberOfLines, string format)
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = doc.Database;
        //Getting table objects
        ObjectId id = db.GetObjectId(false, new Handle(Convert.ToInt64(tableHandle, 16)), 0);
        //Fillling table
        var tabl = tr.GetObject(id, OpenMode.ForWrite) as Table;
        for (int i = 0; i < numberOfLines; i++)
        {
            tabl.Cells[2 + i, 1].TextString = hatches.Where(x => (x.NumberInTable == i) && x.IsInsidePlot).Select(x => x.Amount).Sum().ToString(format);
            tabl.Cells[2 + i, 2].TextString = hatches.Where(x => (x.NumberInTable == i) && !x.IsInsidePlot).Select(x => x.Amount).Sum().ToString(format);
        }
    }
}
