using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using StageProjectScripts.Models;

using AcBr = Autodesk.AutoCAD.BoundaryRepresentation;

namespace StageProjectScripts.Functions
{
    internal class DataProcessing
    {
        DataImport _dataImport;
        DataExport _dataExport;
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database db = Application.DocumentManager.MdiActiveDocument.Database;
        Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
        public DataProcessing()
        {
            _dataImport = new DataImport();
            _dataExport = new DataExport();
        }
        //Function to check hatches intersections
        public void HatchIntersections(Variables variables)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                using (DocumentLock acLckDoc = doc.LockDocument())
                {
                    var blockTable = tr.GetObject(db.BlockTableId, OpenMode.ForRead, false) as BlockTable;
                    var btr = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite, false) as BlockTableRecord; // ForWrite, because we will add new objects
                    List<Polyline> plines = new(); // list of hatch boundaries
                    //Finding intersecting Hatches
                    List<Hatch> hatches = new();
                    var layersToCheck = variables.LaylistHatch.ToList();
                    layersToCheck.AddRange(variables.LaylistPlA);
                    for (var i = 0; i < layersToCheck.Count; i++)
                    {
                        hatches.AddRange(_dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, layersToCheck[i]));
                    }
                    //Creating regions from hatches
                    var regions = CreateRegionsFromHatches(hatches, out int regionErrors);

                    //Checking for region intersections
                    var intersections = CheckForRegionIntersections(tr, btr, variables, regions);
                    //displaying results
                    if (regionErrors != 0)
                    {
                        System.Windows.MessageBox.Show($"Найдено {intersections.Count} пересечений штриховок, найдено {regionErrors} штриховок для которых не возможно проверить пересечения, необходимо их проверить или пересоздать", "Сообщение", System.Windows.MessageBoxButton.OK);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"Найдено {intersections.Count} пересечений штриховок", "Сообщение", System.Windows.MessageBoxButton.OK);
                    }
                    //selecting created intersection regions
                    if (intersections.Count > 0)
                    {
                        ed.SetImpliedSelection(intersections.ToArray());
                        ed.SelectImplied();
                    }
                    tr.Commit();
                }
            }
        }
        //Creating regions from hatches
        internal List<Region> CreateRegionsFromHatches(List<Hatch> hatches, out int regionErrors)
        {
            regionErrors = 0;
            List<Region> regions = new();
            for (var i = 0; i < hatches.Count; i++)
            {
                try
                {
                    Plane plane = hatches[i].GetPlane();
                    Region region = null;
                    if (hatches[i].GetLoopAt(0).LoopType == HatchLoopTypes.External)
                    {
                        //first loop as a start
                        region = CreateRegionFromHatchLoop(hatches[i].GetLoopAt(0), plane);
                        //for rest of loops
                        int nLoops = hatches[i].NumberOfLoops;
                        for (int k = 1; k < nLoops; k++)
                        {
                            var loop = hatches[i].GetLoopAt(k);
                            var region2 = CreateRegionFromHatchLoop(loop, plane);
                            if (loop.LoopType == HatchLoopTypes.External)
                            {
                                regions.Add(region2);
                            }
                            else
                            {
                                region.BooleanOperation(BooleanOperationType.BoolSubtract, region2);
                            }
                        }
                    }
                    else
                    {
                        //finding first external loop to work with
                        HatchLoop externalLoop;
                        int hatchLoopId = 0;
                        for (int j = 0; j < hatches[i].NumberOfLoops; j++)
                        {
                            if (hatches[i].LoopTypeAt(j) == HatchLoopTypes.External)
                            {
                                externalLoop = hatches[i].GetLoopAt(j);
                                hatchLoopId = j;
                                break;
                            }
                        }
                        //first loop as a start
                        region = CreateRegionFromHatchLoop(hatches[i].GetLoopAt(hatchLoopId), plane);
                        for (int k = 0; k < hatches[i].NumberOfLoops; k++)
                        {
                            if (k != hatchLoopId)
                            {
                                var loop = hatches[i].GetLoopAt(k);
                                var region2 = CreateRegionFromHatchLoop(loop, plane);
                                if (loop.LoopType == HatchLoopTypes.External)
                                {
                                    regions.Add(region2);
                                    System.Windows.MessageBox.Show($"Штриховки с несколькими внешними границами и отверстиями не поддерживаются, необходимо переделать штриховку чтобы исключить одновременное использование нескольких внешних границ и внутренней", "Сообщение", System.Windows.MessageBoxButton.OK);
                                }
                                else
                                {
                                    region.BooleanOperation(BooleanOperationType.BoolSubtract, region2);
                                }
                            }
                        }

                    }
                    regions.Add(region);
                }
                catch
                {
                    regionErrors++;
                }
            }
            return regions;
        }
        //Create region from hatch
        internal Region CreateRegionFromHatchLoop(HatchLoop loop, Plane plane)
        {
            DBObjectCollection loopColl = new();
            var pl = GetBorderFromHatchLoop(loop, plane);
            loopColl.Add(pl);
            var reg = Region.CreateFromCurves(loopColl);
            Region region = reg.Cast<Region>().First();
            return region;
        }
        //function to check for intersections between regions
        internal List<ObjectId> CheckForRegionIntersections(Transaction tr, BlockTableRecord btr, Variables variables, List<Region> regions)
        {
            List<ObjectId> intersections = new();
            for (var i = 0; i < regions.Count - 1; i++)
            {
                for (int j = i + 1; j < regions.Count; j++)
                {
                    var aReg = regions[i].Clone() as Region;
                    var aReg2 = regions[j].Clone() as Region;
                    aReg.BooleanOperation(BooleanOperationType.BoolIntersect, aReg2);
                    if (aReg != null && aReg.Area >= 0.01)
                    {
                        // checking if temporary layer exist, if not - creating it.
                        _dataExport.LayerCheck(tr, variables.TempLayer, Color.FromColorIndex(ColorMethod.ByAci, variables.TempLayerColor), variables.TempLayerLineWeight, variables.TempLayerPrintable);
                        aReg.Layer = variables.TempLayer;
                        btr.AppendEntity(aReg);
                        tr.AddNewlyCreatedDBObject(aReg, true);
                        intersections.Add(aReg.ObjectId);
                    }
                    else
                    {
                        aReg.Dispose();
                    }
                    aReg2.Dispose();
                }
            }
            return intersections;
        }
        //Function finding intersection region of 2 polylines
        public Region CreateRegionFromPolyline(Polyline pl)
        {
            DBObjectCollection c1 = new()
            {
                pl
            };
            try
            {
                var r1 = Region.CreateFromCurves(c1);
                Region output = r1.Cast<Region>().First();
                return output;
            }
            catch
            {
                return null;
            }
        }
        internal void CheckForHatchesWithBorderRestorationErrors(Variables variables)
        {
            List<ObjectId> errorHatches = new();
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    List<Hatch> hatches = new();
                    for (var i = 0; i < variables.LaylistHatch.Length; i++)
                    {
                        hatches.AddRange(_dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatch[i]));
                    }
                    foreach (var hat in hatches)
                    {
                        try
                        {
                            Plane plane = hat.GetPlane();
                            for (int k = 0; k < hat.NumberOfLoops; k++)
                            {
                                HatchLoop loop = hat.GetLoopAt(k);
                                HatchLoopTypes hlt = hat.LoopTypeAt(k);
                                Polyline loopArea = GetBorderFromHatchLoop(loop, plane);
                                if (CreateRegionFromPolyline(loopArea) == null)
                                {
                                    errorHatches.Add(hat.ObjectId);
                                }
                            }
                        }
                        catch
                        {
                            errorHatches.Add(hat.ObjectId);
                        }
                    }
                    System.Windows.MessageBox.Show($"Найдено {errorHatches.Count} проблемных штриховок, просьба проверить их или пересоздать", "Сообщение", System.Windows.MessageBoxButton.OK);
                    if (errorHatches.Count > 0)
                    {
                        ed.SetImpliedSelection(errorHatches.ToArray());
                        ed.SelectImplied(); // selecting bad hatches 
                    }
                    tr.Commit();
                }
            }
        }
        internal void CheckHatchesForSelfIntersections(Variables variables)
        {
            List<ObjectId> errorHatches = new();
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    List<Hatch> hatches = new();
                    for (var i = 0; i < variables.LaylistHatch.Length; i++)
                    {
                        hatches.AddRange(_dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatch[i]));
                    }
                    foreach (var hat in hatches)
                    {
                        try
                        {
                            var test = hat.Area;
                        }
                        catch
                        {
                            errorHatches.Add(hat.ObjectId);
                        }
                    }
                    System.Windows.MessageBox.Show($"Найдено {errorHatches.Count} самопересекающихся штриховок", "Сообщение", System.Windows.MessageBoxButton.OK);
                    if (errorHatches.Count > 0)
                    {
                        ed.SetImpliedSelection(errorHatches.ToArray());
                        ed.SelectImplied(); // selecting bad hatches 
                    }
                    tr.Commit();
                }
            }
        }
        internal void CheckForBorderIntersections(Variables variables, string plotXref, string plotNumber)
        {
            List<Point3d> errorPoints = new();
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline plotBorder = _dataImport.GetPlotBorder(variables.PlotLayer, tr, plotXref, plotNumber);
                    if (plotBorder == null)
                    {
                        return;
                    }
                    List<Hatch>[] hatches = new List<Hatch>[variables.LaylistHatch.Length];
                    for (var i = 0; i < variables.LaylistHatch.Length; i++)
                    {
                        hatches[i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatch[i]);
                    }
                    //Checking hatches
                    for (var i = 0; i < variables.LaylistHatch.Length; i++)
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
                    List<Polyline>[] polylinesForLines = new List<Polyline>[variables.LaylistPlL.Length + variables.LaylistPlA.Length - 1];
                    for (var i = 0; i < variables.LaylistPlL.Length; i++)
                    {
                        polylinesForLines[i] = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlL[i]);
                    }
                    for (int i = 1; i < variables.LaylistPlA.Length; i++)
                    {
                        polylinesForLines[variables.LaylistPlL.Length + i - 1] = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlA[i]);
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
                        _dataExport.CreateTempCircleOnPoint(variables, tr, errorPoints);
                    }
                    tr.Commit();
                }
            }
        }
        internal void LabelPavements(Variables variables, string xRef)
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    List<Hatch>[] hatches = new List<Hatch>[variables.LaylistHatch.Length];
                    for (var i = 0; i < variables.PLabelValues.Length; i++)
                    {
                        hatches[i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatch[i], xRef);
                    }
                    List<string> texts = new();
                    List<Point3d> pts = new();
                    //filling lists with data
                    for (var i = 0; i < variables.PLabelValues.Length; i++)
                    {
                        foreach (var hat in hatches[i])
                        {
                            //getting center of each hatch
                            Extents3d extents = hat.GeometricExtents;
                            pts.Add(extents.MinPoint + (extents.MaxPoint - extents.MinPoint) / 2.0);
                            //adding label texts based on layer
                            texts.Add(variables.PLabelValues[i]);
                        }
                    }
                    //creating MLeaders
                    _dataExport.CreateMleaderWithText(tr, texts, pts, variables.PLabelLayer);
                    tr.Commit();
                }
            }
        }
        internal void LabelGreenery(Variables variables)
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    for (int i = 0; i < variables.GreeneryGroupingDistance.Length; i++)
                    {
                        var greeneryBlocks = GroupBlocksByDistance(_dataImport.GetBlocksPosition(tr, variables.LaylistBlockCount[i]), variables.GreeneryGroupingDistance[i]);
                        // TODO: Add better grouping mechanism
                        _dataExport.CreateMleaderWithBlockForGroupOfobjects(tr, greeneryBlocks, variables.GreeneryId[i], variables.GreeneryMleaderStyleName, variables.OLabelLayer, variables.GreeneryMleaderBlockName, variables.GreeneryAttr);
                    }
                    tr.Commit();
                }
            }
        }
        //Function to group elements based on distance between 2 of them
        public List<List<Point3d>> GroupBlocksByDistance(List<Point3d> Points, double Distance)
        {
            // Calclulate distance between points
            List<List<double>> Dist = new();
            for (int i = 0; i < Points.Count; i++)
            {
                Dist.Add(new List<double>());

                for (int j = 0; j < Points.Count; j++)
                {
                    Dist[i].Add(Points[i].DistanceTo(Points[j]));
                }
            }
            // Making lists of close objects
            List<List<int>> Close = new();
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
            List<List<int>> Groups = new();
            for (int i = 0; i < Points.Count; i++)
            {
                List<int> temp = new();
                List<int> temp2 = new();
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
            var GroupsClean = Groups.Where(x => x != null).ToList();

            // Change back to points
            List<List<Point3d>> GroupPoints = new();
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
        internal void CalculateVolumes(Variables variables, string xRef, string plotXref, string plotNumber)
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline plotBorder = _dataImport.GetPlotBorder(variables.PlotLayer, tr, plotXref, plotNumber);
                    if (plotBorder == null)
                    {
                        return;
                    }

                    CalculateAndFillHatchTable(variables, tr, xRef, plotBorder);

                    CalculateAndFillPolylineLengthTable(variables, tr, xRef, plotBorder);

                    CalculateAndFillPolylineAreaTable(variables, tr, xRef, plotBorder);

                    CalculateAndFillNormalBlocksTable(variables, tr, xRef, plotBorder);

                    CalculateAndFillParamBlocksTable(variables, tr, xRef, plotBorder);

                    tr.Commit();
                }
            }
        }
        private void CalculateAndFillHatchTable(Variables variables, Transaction tr, string xRef, Polyline plotBorder)
        {
            //Getting data for Hatch table
            try
            {
                List<DataElementModel> hatchModelList = new();
                List<Hatch>[] hatches = _dataImport.GetAllElementsOfTypeOnLayers<Hatch>(tr, variables.LaylistHatch, xRef);
                for (var i = 0; i < variables.LaylistHatch.Length; i++)
                {
                    var hatchAreas = GetHatchArea(tr, hatches[i]);
                    var areHatchesInside = AreObjectsInsidePlot<Hatch>(plotBorder, hatches[i]);
                    for (var j = 0; j < hatches[i].Count; j++)
                    {
                        hatchModelList.Add(new DataElementModel(hatchAreas[j], i, areHatchesInside[j]));
                    }
                }
                //Filling hatch table
                _dataExport.FillTableWithData(tr, hatchModelList, variables.Th, variables.LaylistHatch.Length, "0.##");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
            }
        }
        private void CalculateAndFillParamBlocksTable(Variables variables, Transaction tr, string xRef, Polyline plotBorder)
        {
            try
            {
                //Counting blocks with parameters
                List<DataElementModel> paramBlocksModelList = new();
                List<BlockReference>[] blocksWithParams = _dataImport.GetAllElementsOfTypeOnLayers<BlockReference>(tr, variables.LaylistBlockWithParams);
                var paramTableRow = 0;
                for (var i = 0; i < variables.LaylistBlockWithParams.Length; i++)
                {
                    var areBlocksInside = AreObjectsInsidePlot<BlockReference>(plotBorder, blocksWithParams[i]);
                    for (int j = 0; j < blocksWithParams[i].Count; j++)
                    {
                        var br = blocksWithParams[i][j];
                        if (br != null && br.IsDynamicBlock)
                        {
                            DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
                            //Checking if it has correct properties in correct places
                            if ((pc[Convert.ToInt32(variables.BlockDetailsParameters[i][1])].PropertyName == variables.BlockDetailsParameters[i][0]) && (variables.BlockDetailsParameters[i][2] == "-" || pc[Convert.ToInt32(variables.BlockDetailsParameters[i][3])].PropertyName == variables.BlockDetailsParameters[i][2]))
                            {
                                for (int k = 0; k < variables.BlockDetailsParametersVariants[i].Count; k++)
                                {
                                    //Checking for property value to determine table row
                                    if (pc[Convert.ToInt32(variables.BlockDetailsParameters[i][1])].Value.ToString() == variables.BlockDetailsParametersVariants[i][k])
                                    {
                                        var amount = variables.BlockDetailsParameters[i][2] == "-" ? 1 : Convert.ToDouble(pc[Convert.ToInt32(variables.BlockDetailsParameters[i][3])].Value);
                                        paramBlocksModelList.Add(new DataElementModel(amount, paramTableRow + k, areBlocksInside[j]));
                                    }
                                }
                            }
                        }
                    }
                    paramTableRow += variables.BlockDetailsParametersVariants[i].Count;
                }
                //Filling blocks with parameters table
                _dataExport.FillTableWithData(tr, paramBlocksModelList, variables.Tbp, paramTableRow, "0.##");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
            }
        }
        private void CalculateAndFillNormalBlocksTable(Variables variables, Transaction tr, string xRef, Polyline plotBorder)
        {
            try
            {
                //Counting blocks including those in arrays
                List<DataElementModel> normalBlocksModelList = new();

                for (int i = 0; i < variables.LaylistBlockCount.Length; i++)
                {
                    var blockPositions = _dataImport.GetBlocksPosition(tr, variables.LaylistBlockCount[i]);
                    var areBlocksInside = AreObjectsInsidePlot(plotBorder, blockPositions);
                    //Creating table data
                    normalBlocksModelList.Add(new DataElementModel(areBlocksInside.Where(x => x == true).Count(), i, true));
                    normalBlocksModelList.Add(new DataElementModel(areBlocksInside.Where(x => x == false).Count(), i, false));
                }
                //Filling Normal blocks table
                _dataExport.FillTableWithData(tr, normalBlocksModelList, variables.Tbn, variables.LaylistBlockCount.Length, "0");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
            }
        }
        private void CalculateAndFillPolylineAreaTable(Variables variables, Transaction tr, string xRef, Polyline plotBorder)
        {
            try
            {
                List<DataElementModel> plineAreaModelList = new();
                List<Polyline>[] polylinesForAreas = _dataImport.GetAllElementsOfTypeOnLayers<Polyline>(tr, variables.LaylistPlA, xRef);
                //Creating regions for GPZU
                var workingRegions = CreateRegionsWithHoleSupport(polylinesForAreas[0]);
                //Work with regions to determine what is outside GPZU
                var gpzuRegion = RegionFromClosedCurve(plotBorder);
                double areaOutside = 0;
                double areaInside = 0;
                foreach (var workReg in workingRegions)
                {
                    areaInside += workReg.Area;
                    workReg.BooleanOperation(BooleanOperationType.BoolSubtract, gpzuRegion);
                    areaOutside += workReg.Area;
                }
                areaInside -= areaOutside;

                plineAreaModelList.Add(new DataElementModel(areaInside, 0, true));
                plineAreaModelList.Add(new DataElementModel(areaOutside, 0, false));

                for (int i = 1; i < variables.LaylistPlA.Length; i++)
                {
                    var arePlinesInside = AreObjectsInsidePlot<Polyline>(plotBorder, polylinesForAreas[i]);
                    for (int j = 0; j < polylinesForAreas[i].Count; j++)
                    {
                        object pl = polylinesForAreas[i][j];
                        var plineArea = (double)pl.GetType().InvokeMember("Area", BindingFlags.GetProperty, null, pl, null);
                        plineAreaModelList.Add(new DataElementModel(plineArea, i, arePlinesInside[j]));
                    }
                }
                //Filling Polyline Area table
                _dataExport.FillTableWithData(tr, plineAreaModelList, variables.Tpa, variables.LaylistPlA.Length, "0.##");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
            }
        }

        internal void CalculateAndFillPolylineLengthTable(Variables variables, Transaction tr, string xRef, Polyline plotBorder)
        {
            try
            {
                List<DataElementModel> plineLengthModelList = new();
                List<Polyline>[] polylinesForLines = _dataImport.GetAllElementsOfTypeOnLayers<Polyline>(tr, variables.LaylistPlL, xRef);

                for (var i = 0; i < variables.LaylistPlL.Length; i++)
                {
                    var plineLengths = polylinesForLines[i].Select(x => x.Length / variables.CurbLineCount[i]).ToList();
                    var arePlinesInside = AreObjectsInsidePlot<Polyline>(plotBorder, polylinesForLines[i]);
                    for (var j = 0; j < polylinesForLines[i].Count; j++)
                    {
                        plineLengthModelList.Add(new DataElementModel(plineLengths[j], i, arePlinesInside[j]));
                    }
                }
                //Filling Polyline length table
                _dataExport.FillTableWithData(tr, plineLengthModelList, variables.Tpl, variables.LaylistPlL.Length, "0");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
            }
        }
        //creating regions from polylines with hole support
        public List<Region> CreateRegionsWithHoleSupport(List<Polyline> polylines)
        {
            List<Region> workingRegions = new();
            if (polylines.Count == 1)
            {
                workingRegions.Add(RegionFromClosedCurve(polylines[0]));
            }
            else if (polylines.Count == 0)
            {
                System.Windows.MessageBox.Show("В файле основы нет границы благоустройства", "Error", System.Windows.MessageBoxButton.OK);
            }
            else
            {
                //We need to find external borders and internal borders of working area and create a region for it correctly.
                List<List<bool>> isInsideAnother = new();
                for (int i = 0; i < polylines.Count; i++)
                {
                    isInsideAnother.Add(new List<bool>());
                    isInsideAnother[i] = AreObjectsInsidePlot(polylines[i], polylines);
                    isInsideAnother[i][i] = false;
                }
                Region[] baseRegions = new Region[isInsideAnother.Count];
                Region[] regionToSubstract = new Region[isInsideAnother.Count];
                for (int i = 0; i < isInsideAnother.Count; i++)
                {
                    var numberOfPlinesInside = isInsideAnother[i].Where(x => x == true).ToList().Count;
                    if (numberOfPlinesInside == 0)
                    {
                        var isRegionInsideAnother = isInsideAnother.Where(x => x[i]).Select(x => x[i]).ToList().Count > 0;
                        if (!isRegionInsideAnother)
                        {
                            baseRegions[i] = CreateRegionFromPolyline(polylines[i]);
                        }
                    }
                    else if (numberOfPlinesInside == 1)
                    {
                        baseRegions[i] = CreateRegionFromPolyline(polylines[i]);
                        var insideBitIndex = isInsideAnother[i].IndexOf(true);
                        regionToSubstract[i] = CreateRegionFromPolyline(polylines[insideBitIndex]);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Островок внутри отверстия не поддерживается для границ участка. Если эта ошибка появилась в другом случае, обратитесь к разработчику", "Error", System.Windows.MessageBoxButton.OK);
                    }
                }
                for (int i = 0; i < baseRegions.Length; i++)
                {
                    if (baseRegions[i] != null)
                    {
                        if (regionToSubstract[i] != null)
                        {
                            baseRegions[i].BooleanOperation(BooleanOperationType.BoolSubtract, regionToSubstract[i]);
                        }
                        workingRegions.Add(baseRegions[i]);
                    }
                }
            }
            return workingRegions;
        }
        //Point Containment for checking out if point is inside plot or outside it
        public Region RegionFromClosedCurve(Curve curve)
        {
            if (!curve.Closed)
                throw new ArgumentException("Curve must be closed.");
            DBObjectCollection curves = new()
            {
                curve
            };
            using (DBObjectCollection regions = Region.CreateFromCurves(curves))
            {
                if (regions == null || regions.Count == 0)
                    throw new InvalidOperationException("Failed to create regions");
                if (regions.Count > 1)
                    throw new InvalidOperationException("Multiple regions created");
                return regions.Cast<Region>().First();
            }
        }
        public PointContainment GetPointContainment(Curve curve, Point3d point)
        {
            if (!curve.Closed)
                throw new ArgumentException("Полилиния границы должна быть замкнута");
            Region region = RegionFromClosedCurve(curve);
            if (region == null)
                throw new InvalidOperationException("Ошибка, проверьте полилинию границы");
            using (region)
            { return GetPointContainment(region, point); }
        }
        public PointContainment GetPointContainment(Region region, Point3d point)
        {
            PointContainment result = PointContainment.Outside;
            using (Brep brep = new(region))
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
        public List<Point3d> GetPointsFromObject<T>(T obj)
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
        public bool ArePointsInsidePolyline(List<Point3d> points, Polyline pl)
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
        public Point3d? ArePointsOnBothSidesOfBorder(List<Point3d> points, Polyline pl)
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
        public List<bool> AreObjectsInsidePlot<T>(Polyline plotBorder, List<T> objects)
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
        public Polyline GetBorderFromHatchLoop(HatchLoop loop, Plane plane)
        {
            //Modified code from Rivilis Restore Hatch Boundary program
            Polyline looparea = new();
            if (loop.IsPolyline)
            {
                using (Polyline poly = new())
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
                        using (Line ent = new())
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
                                using (Circle ent = new(new Point3d(plane, arc2d.Center), plane.Normal, arc2d.Radius))
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
                                using (Arc ent = new(new Point3d(plane, arc2d.Center), plane.Normal, arc2d.Radius, startAngle, endAngle))
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
                        using (Ellipse ent = new(new Point3d(plane, ellipse2d.Center), plane.Normal, new Vector3d(plane, ellipse2d.MajorAxis) * ellipse2d.MajorRadius, ellipse2d.MinorRadius / ellipse2d.MajorRadius, ellipse2d.StartAngle, ellipse2d.EndAngle))
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
                            using (Point3dCollection p3ds = new())
                            {
                                foreach (Point2d p in n2fd.FitPoints) p3ds.Add(new Point3d(plane, p));
                                using (Spline ent = new(p3ds, new Vector3d(plane, n2fd.StartTangent), new Vector3d(plane, n2fd.EndTangent), n2fd.Degree, n2fd.FitTolerance.EqualPoint))
                                {
                                    looparea.JoinEntity(ent);
                                }
                            }
                        }
                        else
                        {
                            NurbCurve2dData n2fd = spline2d.DefinitionData;
                            using (Point3dCollection p3ds = new())
                            {
                                DoubleCollection knots = new(n2fd.Knots.Count);
                                foreach (Point2d p in n2fd.ControlPoints) p3ds.Add(new Point3d(plane, p));
                                foreach (double k in n2fd.Knots) knots.Add(k);
                                double period = 0;
                                using (Spline ent = new(n2fd.Degree, n2fd.Rational, spline2d.IsClosed(), spline2d.IsPeriodic(out period), p3ds, knots, n2fd.Weights, n2fd.Knots.Tolerance, n2fd.Knots.Tolerance))
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
        public List<double> GetHatchArea(Transaction tr, List<Hatch> hatchList)
        {
            List<double> hatchAreaList = new();
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
