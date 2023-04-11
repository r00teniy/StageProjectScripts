using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using StageProjectScripts.Models;

using AcBr = Autodesk.AutoCAD.BoundaryRepresentation;

namespace StageProjectScripts.Functions
{
    internal static class DataProcessing
    {
        internal static Polyline GetPlotBorder(Transaction tr, string plotXref, string plotNumber)
        {
            var plotBorders = DataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, Variables.plotLayers + plotNumber.Replace(':', '_'), plotXref);
            if (plotBorders.Count != 1)
            {
                System.Windows.MessageBox.Show("На слое участка ГПЗУ должна быть ровно одна полилиния", "Error", System.Windows.MessageBoxButton.OK);
                return null;
            }
            else
            {
                return plotBorders[0];
            }
        }
        internal static void CheckForBorderIntersections(string plotXref, string plotNumber)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            List<DataElementModel> hatchModelList = new();
            List<DataElementModel> plineLengthModelList = new();
            List<DataElementModel> plineAreaModelList = new();
            List<Point3d> errorPoints = new();
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline plotBorder = GetPlotBorder(tr, plotXref, plotNumber);
                    if (plotBorder == null)
                    {
                        return;
                    }
                    List<Hatch>[] hatches = new List<Hatch>[Variables.laylistHatch.Length];
                    for (var i = 0; i < Variables.laylistHatch.Length; i++)
                    {
                        hatches[i] = DataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, Variables.laylistHatch[i]);
                    }
                    //Checking hatches
                    for (var i = 0; i < Variables.laylistHatch.Length; i++)
                    {
                        foreach (var hat in hatches[i])
                        {
                            if (ArePointsOnBothSidesOfBorder(GetPointsFromObject<Hatch>(hat), plotBorder) is var res && res != null)
                            {
                                errorPoints.Add((Point3d)res);
                            }
                        }
                    }
                    //Checking polylines
                    List<Polyline>[] polylinesForLines = new List<Polyline>[Variables.laylistPlL.Length + Variables.laylistPlA.Length - 1];
                    for (var i = 0; i < Variables.laylistPlL.Length; i++)
                    {
                        polylinesForLines[i] = DataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, Variables.laylistPlL[i]);
                    }
                    for (int i = 1; i < Variables.laylistPlA.Length; i++)
                    {
                        polylinesForLines[Variables.laylistPlL.Length + i - 1] = DataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, Variables.laylistPlA[i]);
                    }
                    for (var i = 0; i < polylinesForLines.Length; i++)
                    {
                        foreach (var pl in polylinesForLines[i])
                        {
                            if (ArePointsOnBothSidesOfBorder(GetPointsFromObject<Polyline>(pl), plotBorder) is var res && res != null)
                            {
                                errorPoints.Add((Point3d)res);
                            }
                        }
                    }
                    //Results
                    if (errorPoints.Count == 0)
                    {
                        System.Windows.MessageBox.Show("Пересечений элементов с границей ГПЗУ нет", "Сообщение", System.Windows.MessageBoxButton.OK);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"Найдено {errorPoints.Count} объектов, пересекающих границу ГПЗУ", "Error", System.Windows.MessageBoxButton.OK);
                        DataExport.CreateTempCircleOnPoint(tr, errorPoints);
                    }
                    tr.Commit();
                }
            }
        }
        internal static void LabelPavements(string xRef)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    List<Hatch>[] hatches = new List<Hatch>[Variables.laylistHatch.Length];
                    for (var i = 0; i < Variables.pLabelValues.Length; i++)
                    {
                        hatches[i] = DataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, Variables.laylistHatch[i], xRef);
                    }
                    List<string> texts = new();
                    List<Point3d> pts = new();
                    //filling lists with data
                    for (var i = 0; i < Variables.pLabelValues.Length; i++)
                    {
                        foreach (var hat in hatches[i])
                        {
                            //getting center of each hatch
                            Extents3d extents = hat.GeometricExtents;
                            pts.Add(extents.MinPoint + (extents.MaxPoint - extents.MinPoint) / 2.0);
                            //adding label texts based on layer
                            texts.Add(Variables.pLabelValues[i]);
                        }
                    }
                    //creating MLeaders
                    DataExport.CreateMleaderWithText(tr, texts, pts, Variables.pLabelLayer);
                    tr.Commit();
                }
            }
        }
        internal static void LabelGreenery()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    for (int i = 0; i < Variables.greeneryGroupingDistance.Length; i++)
                    {
                        var greeneryBlocks = GroupBlocksByDistance(DataImport.GetBlocksPosition(tr, Variables.laylistBlockCount[i]), Variables.greeneryGroupingDistance[i]);
                        // TODO: Add better grouping mechanism
                        DataExport.CreateMleaderWithBlockForGroupOfobjects(tr, greeneryBlocks, Variables.greeneryId[i], Variables.greeneryMleaderStyleName, Variables.oLabelLayer, Variables.greeneryMleaderBlockName, Variables.greeneryAttr);
                    }
                    tr.Commit();
                }
            }
        }
        //Function to group elements based on distance between 2 of them
        public static List<List<Point3d>> GroupBlocksByDistance(List<Point3d> Points, double Distance)
        {
            // Calclulate distance between points
            List<List<double>> Dist = new List<List<double>>();
            for (int i = 0; i < Points.Count; i++)
            {
                Dist.Add(new List<double>());

                for (int j = 0; j < Points.Count; j++)
                {
                    Dist[i].Add(Points[i].DistanceTo(Points[j]));
                }
            }
            // Making lists of close objects
            List<List<int>> Close = new List<List<int>>();
            for (int i = 0; i < Points.Count; i++)
            {
                Close.Add(new List<int>());
                for (int j = 0; j < Points.Count; j++)
                {
                    if (Dist[i][j] < Distance)
                    {
                        Close[i].Add(j);
                    }
                }
            }
            // Making groups
            List<List<int>> Groups = new List<List<int>>();
            for (int i = 0; i < Points.Count; i++)
            {
                List<int> temp = new List<int>();
                List<int> temp2 = new List<int>();
                temp.Add(Close[i][0]);
                temp2.Add(Close[i][0]);
                int X = 0;
                while (X == 0)
                {
                    for (int k = 0; k < temp.Count; k++)
                    {
                        foreach (int l in Close[temp[k]])
                        {
                            if (!temp.Contains(l))
                            {
                                temp2.Add(l);
                            }
                        }
                    }
                    if (temp == temp2) // If we didn't add new objects we go to next one
                    {
                        X = 1;
                    }
                    else
                    {
                        temp = temp2;
                    }
                }
                Groups.Add(temp);
            }
            // Clean group
            List<List<int>> GroupsClean = new List<List<int>>();
            for (int i = 0; i < Groups.Count; i++)
            {
                if (Groups[i] != null && Groups[i].Count > 1)
                {
                    foreach (int j in Groups[i])
                    {
                        if (j != i)
                        {
                            Groups[j] = null;
                        }
                    }
                }
            }
            foreach (List<int> m in Groups)
            {
                if (m != null)
                {
                    GroupsClean.Add(m);
                }
            }
            // Change back to points
            List<List<Point3d>> GroupPoints = new List<List<Point3d>>();
            for (int i = 0; i < GroupsClean.Count; i++)
            {
                GroupPoints.Add(new List<Point3d>());
                foreach (int j in GroupsClean[i])
                {
                    GroupPoints[i].Add(Points[j]);
                }
            }
            return GroupPoints;
        }
        internal static void CalculateVolumes(string xRef, string plotXref, string plotNumber)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            List<DataElementModel> hatchModelList = new();
            List<DataElementModel> plineLengthModelList = new();
            List<DataElementModel> plineAreaModelList = new();
            List<DataElementModel> normalBlocksModelList = new();
            List<DataElementModel> paramBlocksModelList = new();
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline plotBorder = GetPlotBorder(tr, plotXref, plotNumber);
                    if (plotBorder == null)
                    {
                        return;
                    }
                    //Getting data for Hatch table
                    try
                    {
                        List<Hatch>[] hatches = new List<Hatch>[Variables.laylistHatch.Length];
                        for (var i = 0; i < Variables.laylistHatch.Length; i++)
                        {
                            hatches[i] = DataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, Variables.laylistHatch[i], xRef);
                        }
                        for (var i = 0; i < Variables.laylistHatch.Length; i++)
                        {
                            var hatchAreas = GetHatchArea(tr, hatches[i]);
                            var areHatchesInside = AreObjectsInsidePlot<Hatch>(plotBorder, hatches[i]);
                            for (var j = 0; j < hatches[i].Count; j++)
                            {
                                hatchModelList.Add(new DataElementModel(hatchAreas[j], i, areHatchesInside[j]));
                            }
                        }
                        //Filling hatch table
                        DataExport.FillTableWithData(tr, hatchModelList, Variables.th, Variables.laylistHatch.Length, "0.##");
                    }
                    catch (System.Exception ex)
                    {
                        System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
                    }
                    //Getting data for Polyline length table
                    try
                    {
                        List<Polyline>[] polylinesForLines = new List<Polyline>[Variables.laylistPlL.Length];
                        for (var i = 0; i < Variables.laylistPlL.Length; i++)
                        {
                            polylinesForLines[i] = DataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, Variables.laylistPlL[i], xRef);
                        }
                        for (var i = 0; i < Variables.laylistPlL.Length; i++)
                        {
                            var plineLengths = polylinesForLines[i].Select(x => x.Length / Variables.curbLineCount[i]).ToList();
                            var arePlinesInside = AreObjectsInsidePlot<Polyline>(plotBorder, polylinesForLines[i]);
                            for (var j = 0; j < polylinesForLines[i].Count; j++)
                            {
                                plineLengthModelList.Add(new DataElementModel(plineLengths[j], i, arePlinesInside[j]));
                            }
                        }
                        //Filling Polyline length table
                        DataExport.FillTableWithData(tr, plineLengthModelList, Variables.tpl, Variables.laylistPlL.Length, "0");
                    }
                    catch (System.Exception ex)
                    {
                        System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
                    }
                    //Getting data for Polyline area table
                    try
                    {
                        List<Polyline>[] polylinesForAreas = new List<Polyline>[Variables.laylistPlA.Length];
                        for (var i = 0; i < Variables.laylistPlA.Length; i++)
                        {
                            polylinesForAreas[i] = DataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, Variables.laylistPlA[i], xRef);
                        }
                        try
                        {
                            var check = ArePointsInsidePolyline(GetPointsFromObject(polylinesForAreas[0][0]), plotBorder);
                            object pl = polylinesForAreas[0][0];
                            var plineArea = (double)pl.GetType().InvokeMember("Area", BindingFlags.GetProperty, null, pl, null);
                            plineAreaModelList.Add(new DataElementModel(plineArea, 0, true));
                        }
                        catch
                        {
                            Region workingRegion = default;
                            if (polylinesForAreas[0].Count == 1)
                            {
                                workingRegion = RegionFromClosedCurve(polylinesForAreas[0][0]);
                            }
                            else if (polylinesForAreas[0].Count == 0)
                            {
                                System.Windows.MessageBox.Show("В файле основы нет границы благоустройства", "Error", System.Windows.MessageBoxButton.OK);
                                return;
                            }
                            else
                            {
                                foreach (var item in polylinesForAreas[0])
                                {
                                    var reg = RegionFromClosedCurve(item);
                                    if (workingRegion != default)
                                    {
                                        workingRegion.BooleanOperation(BooleanOperationType.BoolUnite, reg);
                                    }
                                    else
                                    {
                                        workingRegion = reg;
                                    }
                                }
                            }
                            var gpzuRegion = RegionFromClosedCurve(plotBorder);
                            workingRegion.BooleanOperation(BooleanOperationType.BoolSubtract, gpzuRegion);
                            var areaOutside = workingRegion.Area;
                            double areaInside = -areaOutside;
                            //For the case when we have several zones:
                            foreach (var item in polylinesForAreas[0])
                            {
                                object pl = item;
                                var plineArea = (double)pl.GetType().InvokeMember("Area", BindingFlags.GetProperty, null, pl, null);
                                areaInside += plineArea;
                            }

                            plineAreaModelList.Add(new DataElementModel(areaInside, 0, true));
                            plineAreaModelList.Add(new DataElementModel(areaOutside, 0, false));
                        }
                        for (int i = 1; i < Variables.laylistPlA.Length; i++)
                        {
                            var arePlinesInside = AreObjectsInsidePlot<Polyline>(plotBorder, polylinesForAreas[i]);
                            for (int j = 0; j < polylinesForAreas[i].Count; j++)
                            {
                                object pl = polylinesForAreas[i][j];
                                var plineArea = (double)pl.GetType().InvokeMember("Area", BindingFlags.GetProperty, null, pl, null);
                                plineAreaModelList.Add(new DataElementModel(plineArea, i, arePlinesInside[j]));
                            }
                        }
                        //Filling Polyline length table
                        DataExport.FillTableWithData(tr, plineAreaModelList, Variables.tpa, Variables.laylistPlA.Length, "0.##");
                    }
                    catch (System.Exception ex)
                    {
                        System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
                    }
                    //Collecting data for normal blocks table
                    try
                    {
                        //Counting blocks including those in arrays
                        List<List<Point3d>> blockPositions = new();
                        List<List<bool>> areBlocksInside = new();
                        for (int i = 0; i < Variables.laylistBlockCount.Length; i++)
                        {
                            blockPositions.Add(new List<Point3d>());
                            blockPositions[i] = DataImport.GetBlocksPosition(tr, Variables.laylistBlockCount[i]);
                            areBlocksInside.Add(new List<bool>());
                            areBlocksInside[i] = AreObjectsInsidePlot(plotBorder, blockPositions[i]);
                        }
                        //Creating table data
                        for (var i = 0; i < Variables.laylistBlockCount.Length; i++)
                        {
                            normalBlocksModelList.Add(new DataElementModel(areBlocksInside[i].Where(x => x == true).Count(), i, true));
                            normalBlocksModelList.Add(new DataElementModel(areBlocksInside[i].Where(x => x == false).Count(), i, false));
                        }
                        //Filling Normal blocks table
                        DataExport.FillTableWithData(tr, normalBlocksModelList, Variables.tbn, Variables.laylistBlockCount.Length, "0");
                    }
                    catch (System.Exception ex)
                    {
                        System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
                    }
                    //Collecting data for blocks with params table
                    try
                    {
                        //Counting blocks with parameters
                        List<BlockReference>[] blocksWithParams = new List<BlockReference>[Variables.laylistBlockWithParams.Length];
                        for (var i = 0; i < Variables.laylistBlockWithParams.Length; i++)
                        {
                            blocksWithParams[i] = DataImport.GetAllElementsOfTypeOnLayer<BlockReference>(tr, Variables.laylistBlockWithParams[i]);
                        }
                        var paramTableRow = 0;
                        for (var i = 0; i < Variables.laylistBlockWithParams.Length; i++)
                        {
                            var areBlocksInside = AreObjectsInsidePlot<BlockReference>(plotBorder, blocksWithParams[i]);
                            for (int j = 0; j < blocksWithParams[i].Count; j++)
                            {
                                var br = blocksWithParams[i][j];
                                if (br != null && br.IsDynamicBlock)
                                {
                                    DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
                                    //Checking if it has correct properties in correct places
                                    if ((pc[Convert.ToInt32(Variables.blockDetailsParameters[i][1])].PropertyName == Variables.blockDetailsParameters[i][0]) && (Variables.blockDetailsParameters[i][2] == "-" || pc[Convert.ToInt32(Variables.blockDetailsParameters[i][3])].PropertyName == Variables.blockDetailsParameters[i][2]))
                                    {
                                        for (int k = 0; k < Variables.blockDetailsParametersVariants[i].Count; k++)
                                        {
                                            //Checking for property value to determine table row
                                            if (pc[Convert.ToInt32(Variables.blockDetailsParameters[i][1])].Value.ToString() == Variables.blockDetailsParametersVariants[i][k])
                                            {
                                                var amount = Variables.blockDetailsParameters[i][2] == "-" ? 1 : Convert.ToDouble(pc[Convert.ToInt32(Variables.blockDetailsParameters[i][3])].Value);
                                                paramBlocksModelList.Add(new DataElementModel(amount, paramTableRow + k, areBlocksInside[j]));
                                            }
                                        }
                                    }
                                }
                            }
                            paramTableRow += Variables.blockDetailsParametersVariants[i].Count;
                        }
                        //Filling blocks with parameters table
                        DataExport.FillTableWithData(tr, paramBlocksModelList, Variables.tbp, paramTableRow, "0.##");
                    }
                    catch (System.Exception ex)
                    {
                        System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
                    }
                    tr.Commit();
                }
            }
        }
        //Point Containment for checking out if point is inside plot or outside it
        public static Region RegionFromClosedCurve(Curve curve)
        {
            if (!curve.Closed)
                throw new ArgumentException("Curve must be closed.");
            DBObjectCollection curves = new DBObjectCollection();
            curves.Add(curve);
            using (DBObjectCollection regions = Region.CreateFromCurves(curves))
            {
                if (regions == null || regions.Count == 0)
                    throw new InvalidOperationException("Failed to create regions");
                if (regions.Count > 1)
                    throw new InvalidOperationException("Multiple regions created");
                return regions.Cast<Region>().First();
            }
        }
        public static PointContainment GetPointContainment(Curve curve, Point3d point)
        {
            if (!curve.Closed)
                throw new ArgumentException("Полилиния границы должна быть замкнута");
            Region region = RegionFromClosedCurve(curve);
            if (region == null)
                throw new InvalidOperationException("Ошибка, проверьте полилинию границы");
            using (region)
            { return GetPointContainment(region, point); }
        }
        public static PointContainment GetPointContainment(Region region, Point3d point)
        {
            PointContainment result = PointContainment.Outside;
            using (Brep brep = new Brep(region))
            {
                if (brep != null)
                {
                    using (BrepEntity ent = brep.GetPointContainment(point, out result))
                    {
                        if (ent is AcBr.Face)
                            result = PointContainment.Inside;
                    }
                }
            }
            return result;
        }
        //Getting hatch border or polyline points to check if it is inside plot or not
        public static List<Point3d> GetPointsFromObject<T>(T obj)
        {
            List<Point3d> output = new();
            if (obj is Hatch hat)
            {
                HatchLoop loop = hat.GetLoopAt(0);
                Plane plane = hat.GetPlane();
                var poly = GetBorderFromHatchLoop(loop, plane);
                for (var i = 0; i < poly.NumberOfVertices; i++)
                {
                    output.Add(poly.GetPoint3dAt(i));
                }
                return output;
            }
            if (obj is Polyline pl)
            {
                for (var i = 0; i < pl.NumberOfVertices; i++)
                {
                    output.Add(pl.GetPoint3dAt(i));
                }
                return output;
            }
            if (obj is BlockReference br)
            {
                output.Add(br.Position);
                return output;
            }
            throw new System.Exception("This method works with BlockReference, Polyline or Hatch only.");
        }
        //Checking if group of points is inside/on the polyline
        public static bool ArePointsInsidePolyline(List<Point3d> points, Polyline pl)
        {
            var isFirstPointIn = GetPointContainment(pl, points[0]);
            for (int i = 1; i < points.Count; i++)
            {
                var isThisPointIn = GetPointContainment(pl, points[i]);
                if (isThisPointIn != isFirstPointIn)
                {
                    if (isFirstPointIn == PointContainment.OnBoundary)
                    {
                        isFirstPointIn = isThisPointIn;
                    }
                    else if (isThisPointIn != PointContainment.OnBoundary)
                    {
                        throw new System.Exception("Одна из полилиний или штриховок пересекает границу участка, необходимо исправить.");
                    }
                }
            }
            return isFirstPointIn == PointContainment.Inside;
        }
        //Getting points that are on other side of plotBorder
        public static Point3d? ArePointsOnBothSidesOfBorder(List<Point3d> points, Polyline pl)
        {
            var isFirstPointIn = GetPointContainment(pl, points[0]);
            for (int i = 1; i < points.Count; i++)
            {
                var isThisPointIn = GetPointContainment(pl, points[i]);
                if (isThisPointIn != isFirstPointIn)
                {
                    if (isFirstPointIn == PointContainment.OnBoundary)
                    {
                        isFirstPointIn = isThisPointIn;
                    }
                    else if (isThisPointIn != PointContainment.OnBoundary)
                    {
                        return points[i];
                    }
                }
            }
            return null;
        }
        //Function to check if hatch/polyline/blockreference is inside plot (can have 2+ borders)
        public static List<bool> AreObjectsInsidePlot<T>(Polyline plotBorder, List<T> objects)
        {
            if (objects == null)
            {
                return null;
            }
            List<bool> results = new();
            foreach (var item in objects)
            {
                var tempResult = false;
                if (item is Point3d point)
                {
                    if (ArePointsInsidePolyline(new List<Point3d>() { point }, plotBorder))
                    {
                        tempResult = true;
                    }
                }
                else
                {
                    if (ArePointsInsidePolyline(GetPointsFromObject(item), plotBorder))
                    {
                        tempResult = true;
                    }
                }
                results.Add(tempResult);
            }
            return results;
        }
        //Function that returns Polyline from HatchLoop
        public static Polyline GetBorderFromHatchLoop(HatchLoop loop, Plane plane)
        {
            //Modified code from Rivilis Restore Hatch Boundary program
            Polyline looparea = new Polyline();
            if (loop.IsPolyline)
            {
                using (Polyline poly = new Polyline())
                {
                    int iVertex = 0;
                    foreach (BulgeVertex bv in loop.Polyline)
                    {
                        poly.AddVertexAt(iVertex++, bv.Vertex, bv.Bulge, 0.0, 0.0);
                    }
                    if (looparea != null)
                    {
                        try
                        {
                            looparea.JoinEntity(poly);
                        }
                        catch
                        {
                            throw new System.Exception("Граница штриховки не может быть воссоздана");
                        }
                    }
                    else
                    {
                        looparea = poly;
                    }
                }
            }
            else
            {
                foreach (Curve2d cv in loop.Curves)
                {
                    LineSegment2d line2d = cv as LineSegment2d;
                    CircularArc2d arc2d = cv as CircularArc2d;
                    EllipticalArc2d ellipse2d = cv as EllipticalArc2d;
                    NurbCurve2d spline2d = cv as NurbCurve2d;
                    if (line2d != null)
                    {
                        using (Line ent = new Line())
                        {
                            try
                            {
                                ent.StartPoint = new Point3d(plane, line2d.StartPoint);
                                ent.EndPoint = new Point3d(plane, line2d.EndPoint);
                                looparea.JoinEntity(ent);
                            }
                            catch
                            {
                                looparea.AddVertexAt(0, line2d.StartPoint, 0, 0, 0);
                                looparea.AddVertexAt(1, line2d.EndPoint, 0, 0, 0);
                            }

                        }
                    }
                    else if (arc2d != null)
                    {
                        try
                        {
                            if (arc2d.IsClosed() || Math.Abs(arc2d.EndAngle - arc2d.StartAngle) < 1e-5)
                            {
                                using (Circle ent = new Circle(new Point3d(plane, arc2d.Center), plane.Normal, arc2d.Radius))
                                {
                                    looparea.JoinEntity(ent);
                                }
                            }
                            else
                            {
                                if (arc2d.IsClockWise)
                                {
                                    arc2d = arc2d.GetReverseParameterCurve() as CircularArc2d;
                                }
                                double angle = new Vector3d(plane, arc2d.ReferenceVector).AngleOnPlane(plane);
                                double startAngle = arc2d.StartAngle + angle;
                                double endAngle = arc2d.EndAngle + angle;
                                using (Arc ent = new Arc(new Point3d(plane, arc2d.Center), plane.Normal, arc2d.Radius, startAngle, endAngle))
                                {
                                    looparea.JoinEntity(ent);
                                }
                            }
                        }
                        catch
                        {
                            // Calculating Bulge
                            double deltaAng = arc2d.EndAngle - arc2d.StartAngle;
                            if (deltaAng < 0)
                                deltaAng += 2 * Math.PI;
                            double GetArcBulge = Math.Tan(deltaAng * 0.25);
                            //Adding first arc to polyline
                            looparea.AddVertexAt(0, new Point2d(arc2d.StartPoint.X, arc2d.StartPoint.Y), GetArcBulge, 0, 0);
                            looparea.AddVertexAt(1, new Point2d(arc2d.EndPoint.X, arc2d.EndPoint.Y), 0, 0, 0);
                        }
                    }
                    else if (ellipse2d != null)
                    {
                        using (Ellipse ent = new Ellipse(new Point3d(plane, ellipse2d.Center), plane.Normal, new Vector3d(plane, ellipse2d.MajorAxis) * ellipse2d.MajorRadius, ellipse2d.MinorRadius / ellipse2d.MajorRadius, ellipse2d.StartAngle, ellipse2d.EndAngle))
                        {
                            ent.GetType().InvokeMember("StartParam", BindingFlags.SetProperty, null, ent, new object[] { ellipse2d.StartAngle });
                            ent.GetType().InvokeMember("EndParam", BindingFlags.SetProperty, null, ent, new object[] { ellipse2d.EndAngle });

                            looparea.JoinEntity(ent);
                        }
                    }
                    else if (spline2d != null)
                    {
                        if (spline2d.HasFitData)
                        {
                            NurbCurve2dFitData n2fd = spline2d.FitData;
                            using (Point3dCollection p3ds = new Point3dCollection())
                            {
                                foreach (Point2d p in n2fd.FitPoints) p3ds.Add(new Point3d(plane, p));
                                using (Spline ent = new Spline(p3ds, new Vector3d(plane, n2fd.StartTangent), new Vector3d(plane, n2fd.EndTangent), /* n2fd.KnotParam, */  n2fd.Degree, n2fd.FitTolerance.EqualPoint))
                                {
                                    looparea.JoinEntity(ent);
                                }
                            }
                        }
                        else
                        {
                            NurbCurve2dData n2fd = spline2d.DefinitionData;
                            using (Point3dCollection p3ds = new Point3dCollection())
                            {
                                DoubleCollection knots = new DoubleCollection(n2fd.Knots.Count);
                                foreach (Point2d p in n2fd.ControlPoints) p3ds.Add(new Point3d(plane, p));
                                foreach (double k in n2fd.Knots) knots.Add(k);
                                double period = 0;
                                using (Spline ent = new Spline(n2fd.Degree, n2fd.Rational, spline2d.IsClosed(), spline2d.IsPeriodic(out period), p3ds, knots, n2fd.Weights, n2fd.Knots.Tolerance, n2fd.Knots.Tolerance))
                                {
                                    looparea.JoinEntity(ent);
                                }
                            }
                        }
                    }
                }
            }
            return looparea;
        }
        //Function to get areas for a list on hatches.
        public static List<double> GetHatchArea(Transaction tr, List<Hatch> hatchList)
        {
            List<double> hatchAreaList = new();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            var bT = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var bTr = (BlockTableRecord)tr.GetObject(bT[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            for (var i = 0; i < hatchList.Count; i++)
            {
                try
                {
                    hatchAreaList.Add(hatchList[i].Area);
                }
                catch
                {
                    //changing to count self-intersecting hatches
                    Plane plane = hatchList[i].GetPlane();
                    double corArea = 0.0;
                    for (int k = 0; k < hatchList[i].NumberOfLoops; k++)
                    {
                        HatchLoop loop = hatchList[i].GetLoopAt(k);
                        HatchLoopTypes hlt = hatchList[i].LoopTypeAt(k);
                        Polyline looparea = GetBorderFromHatchLoop(loop, plane);
                        // Can get correct value from AcadObject, but need to add in first
                        bTr.AppendEntity(looparea);
                        tr.AddNewlyCreatedDBObject(looparea, true);
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
                    hatchAreaList.Add(corArea);
                }
            }
            return hatchAreaList;
        }
    }
}
