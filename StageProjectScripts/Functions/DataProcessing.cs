﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

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
        double tolerance = 0.01;
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
                    List<Hatch> hatchesOnRoof = new();
                    var layersToCheck = variables.LaylistHatch.ToList();
                    layersToCheck.AddRange(variables.LaylistHatchKindergarten);
                    var layersToCheckOnRoof = variables.LaylistHatchRoof.ToList();
                    layersToCheckOnRoof.AddRange(variables.LaylistHatchKindergartenOnRoof);

                    for (var i = 0; i < layersToCheck.Count; i++)
                    {
                        hatches.AddRange(_dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, layersToCheck[i]));
                    }

                    for (var i = 0; i < layersToCheckOnRoof.Count; i++)
                    {
                        hatchesOnRoof.AddRange(_dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, layersToCheckOnRoof[i]));
                    }
                    //Creating intersections from hatches
                    var intersections = GetIntersections(tr, btr, variables, hatches);
                    //Creating intersections from hatches on roof
                    var intersectionsOnRoof = GetIntersections(tr, btr, variables, hatchesOnRoof);
                    if (intersections != null)
                    {
                        if (intersectionsOnRoof != null && intersectionsOnRoof.Count > 0)
                        {
                            intersections.AddRange(intersectionsOnRoof);
                        }
                        //selecting created intersection regions
                        if (intersections != null && intersections.Count > 0)
                        {
                            ed.SetImpliedSelection(intersections.ToArray());
                            ed.SelectImplied();
                        }
                    }
                    tr.Commit();
                }
            }
        }
        //Creating regions from hatches
        private List<ObjectId> GetIntersections(Transaction tr, BlockTableRecord btr, Variables variables, List<Hatch> hatches)
        {
            var regions = CreateRegionsFromHatches(hatches, out int regionErrors);
            List<ObjectId> intersections = new();
            try
            {
                intersections = CheckForRegionIntersections(tr, btr, variables, regions);
            }
            catch (System.Exception e)
            {
                ObjectId[] errorsSelection = { hatches[Convert.ToInt32(e.Message.Split(',')[0])].ObjectId, hatches[Convert.ToInt32(e.Message.Split(',')[1])].ObjectId };
                ed.SetImpliedSelection(errorsSelection);
                ed.SelectImplied();
                System.Windows.MessageBox.Show($"Найдена пара штриховок, пересечение которых выдаёт ошибку, необходимо их проверить на самопересечения вручную или пересоздать", "Сообщение", System.Windows.MessageBoxButton.OK);
                return null;
            }
            //displaying results
            if (regionErrors != 0)
            {
                System.Windows.MessageBox.Show($"Найдено {intersections.Count} пересечений штриховок, найдено {regionErrors} штриховок для которых не возможно проверить пересечения, необходимо их проверить или пересоздать", "Сообщение", System.Windows.MessageBoxButton.OK);
            }
            else
            {
                System.Windows.MessageBox.Show($"Найдено {intersections.Count} пересечений штриховок", "Сообщение", System.Windows.MessageBoxButton.OK);
            }
            return intersections;
        }
        private List<Region> CreateRegionsFromHatches(List<Hatch> hatches, out int regionErrors)
        {
            regionErrors = 0;
            List<Region> regions = new();
            for (var i = 0; i < hatches.Count; i++)
            {
                try
                {
                    // can't intersec hatches, get polyline and intersect them or just get curves for each loop and intersect them??
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
        private Region CreateRegionFromHatchLoop(HatchLoop loop, Plane plane)
        {
            DBObjectCollection loopColl = new();
            var pl = GetBorderFromHatchLoop(loop, plane);
            loopColl.Add(pl);
            var reg = Region.CreateFromCurves(loopColl);
            Region region = reg.Cast<Region>().First();
            return region;
        }
        //function to check for intersections between regions
        private List<ObjectId> CheckForRegionIntersections(Transaction tr, BlockTableRecord btr, Variables variables, List<Region> regions)
        {
            List<ObjectId> intersections = new();
            for (var i = 0; i < regions.Count - 1; i++)
            {
                for (int j = i + 1; j < regions.Count; j++)
                {
                    try
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
                    catch
                    {
                        throw new System.Exception($"{i},{j}");
                    }
                }
            }
            return intersections;
        }
        //Function finding intersection region of 2 polylines
        private Region CreateRegionFromPolyline(Polyline pl)
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
        public void CheckForHatchesWithBorderRestorationErrors(Variables variables)
        {
            List<ObjectId> errorHatches = new();
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    List<Hatch>[] allHatches = GetAllHatchesToCheck(variables, tr);

                    foreach (var hatches in allHatches)
                    {
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
        public void CheckHatchesForSelfIntersections(Variables variables)
        {
            List<ObjectId> errorHatches = new();
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    List<Hatch>[] allHatches = GetAllHatchesToCheck(variables, tr);
                    foreach (var hatches in allHatches)
                    {
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
        public void CheckForBorderIntersections(Variables variables, string plotXref, string plotNumber)
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    //Getting elements to check
                    List<Hatch>[] hatches = GetHatchesToCheck(variables, tr);
                    List<Hatch>[] hatchesOnRoof = GetHatchesToCheckOnRoof(variables, tr);
                    List<Polyline>[] polylines = GetAllPolylinesToCheck(variables, tr);
                    FlattenPolylines(ref polylines);
                    List<Polyline>[] polylinesOnRoof = GetAllPolylinesToCheckOnRoof(variables, tr);
                    FlattenPolylines(ref polylinesOnRoof);
                    //Checking elements with every border
                    (List<(Point3d, Point3d)>, List<Region>) plotRegionResult = new(new List<(Point3d, Point3d)>(), new List<Region>());
                    (List<(Point3d, Point3d)>, List<Region>) plotRegionResultRoof = new(new List<(Point3d, Point3d)>(), new List<Region>());
                    (List<(Point3d, Point3d)>, List<Region>) workRegionResult = new(new List<(Point3d, Point3d)>(), new List<Region>());
                    (List<(Point3d, Point3d)>, List<Region>) workRegionResultRoof = new(new List<(Point3d, Point3d)>(), new List<Region>());
                    (List<(Point3d, Point3d)>, List<Region>) buildingRegionResult = new(new List<(Point3d, Point3d)>(), new List<Region>());
                    (List<(Point3d, Point3d)>, List<Region>) buildingRegionResultRoof = new(new List<(Point3d, Point3d)>(), new List<Region>());
                    (List<(Point3d, Point3d)>, List<Region>) kindergartenRegionResult = new(new List<(Point3d, Point3d)>(), new List<Region>());
                    (List<(Point3d, Point3d)>, List<Region>) kindergartenRegionResultRoof = new(new List<(Point3d, Point3d)>(), new List<Region>());
                    //Plotborder
                    List<Region> plotRegions = null;
                    MPolygon plotMPolygon = null;
                    try
                    {
                        plotRegions = GenerateRegionsFromBorders(tr, variables.PlotLayer, "ГПЗУ", plotNumber, plotXref);
                        plotMPolygon = GenerateMPolygonFromBorders(tr, variables.PlotLayer, "ГПЗУ", plotNumber, plotXref);
                    }
                    catch (System.Exception)
                    {
                        System.Windows.MessageBox.Show("Проблема при создании региона из участка ГПЗУ, проверьте полилинию.", "Сообщение", System.Windows.MessageBoxButton.OK);
                    }
                    if (plotRegions != null && plotMPolygon != null)
                    {
                        plotRegionResult = CheckHatchesAndPolylinesForIntersectionsWithRegions(variables, plotRegions, plotMPolygon, hatches, polylines, "ГПЗУ");
                        plotRegionResultRoof = CheckHatchesAndPolylinesForIntersectionsWithRegions(variables, plotRegions, plotMPolygon, hatchesOnRoof, polylinesOnRoof, "ГПЗУ");
                    }
                    //WorkingZoneBorder
                    try
                    {
                        plotRegions = GenerateRegionsFromBorders(tr, variables.LaylistPlA[0], "Благоустройства");
                        plotMPolygon = GenerateMPolygonFromBorders(tr, variables.LaylistPlA[0], "Благоустройства");
                    }
                    catch (System.Exception)
                    {
                        System.Windows.MessageBox.Show("Проблема при создании региона из границы благоустройства, проверьте полилинию.", "Сообщение", System.Windows.MessageBoxButton.OK);
                    }
                    if (plotRegions != null && plotMPolygon != null)
                    {
                        workRegionResult = CheckHatchesAndPolylinesForIntersectionsWithRegions(variables, plotRegions, plotMPolygon, hatches, polylines, "Благоустройства");
                        workRegionResultRoof = CheckHatchesAndPolylinesForIntersectionsWithRegions(variables, plotRegions, plotMPolygon, hatchesOnRoof, polylinesOnRoof, "Благоустройства");
                    }
                    //BuildingBorder
                    try
                    {
                        plotRegions = GenerateRegionsFromBorders(tr, variables.LaylistPlA[1], "Зданий");
                        plotMPolygon = GenerateMPolygonFromBorders(tr, variables.LaylistPlA[1], "Зданий");
                    }
                    catch (System.Exception)
                    {
                        System.Windows.MessageBox.Show("Проблема при создании региона из контура здания, проверьте полилинию.", "Сообщение", System.Windows.MessageBoxButton.OK);
                    }
                    if (plotRegions != null && plotMPolygon != null)
                    {
                        buildingRegionResult = CheckHatchesAndPolylinesForIntersectionsWithRegions(variables, plotRegions, plotMPolygon, hatches, polylines, "Зданий", 0.01);
                    }
                    //RoofBorder
                    try
                    {
                        plotRegions = GenerateRegionsFromBorders(tr, variables.RoofBorderLayerName, "Крыши");
                        plotMPolygon = GenerateMPolygonFromBorders(tr, variables.RoofBorderLayerName, "Крыши");

                    }
                    catch (System.Exception)
                    {
                        System.Windows.MessageBox.Show("Проблема при создании региона из контура крыши, проверьте полилинию.", "Сообщение", System.Windows.MessageBoxButton.OK);
                    }
                    if (plotRegions != null && plotMPolygon != null)
                    {
                        buildingRegionResultRoof = CheckHatchesAndPolylinesForIntersectionsWithRegions(variables, plotRegions, plotMPolygon, hatchesOnRoof, polylinesOnRoof, "Крыши", 0.01);
                    }
                    //KindergartenBorder
                    try
                    {
                        plotRegions = GenerateRegionsFromBorders(tr, variables.LaylistPlA[2], "Детского сада");
                        plotMPolygon = GenerateMPolygonFromBorders(tr, variables.LaylistPlA[2], "Детского сада");
                    }
                    catch (System.Exception)
                    {
                        System.Windows.MessageBox.Show("Проблема при создании региона из границы детсколго сада, проверьте полилинию.", "Сообщение", System.Windows.MessageBoxButton.OK);
                    }
                    if (plotRegions != null && plotMPolygon != null)
                    {
                        kindergartenRegionResult = CheckHatchesAndPolylinesForIntersectionsWithRegions(variables, plotRegions, plotMPolygon, hatches, polylines, "Детского сада");
                        kindergartenRegionResultRoof = CheckHatchesAndPolylinesForIntersectionsWithRegions(variables, plotRegions, plotMPolygon, hatchesOnRoof, polylinesOnRoof, "Детского сада");
                    }
                    //results
                    List<(Point3d, Point3d)> linesToDraw = new();
                    List<Region> regionsToDraw = new();
                    var message = new StringBuilder();
                    int count = plotRegionResult.Item1.Count() + plotRegionResultRoof.Item1.Count();
                    if (count > 0)
                    {
                        message.AppendLine($"Найдено {count} пересечений полилиний с границей ГПЗУ");
                        linesToDraw.AddRange(plotRegionResult.Item1);
                        linesToDraw.AddRange(plotRegionResultRoof.Item1);
                    }
                    count = plotRegionResult.Item2.Count() + plotRegionResultRoof.Item2.Count();
                    if (count > 0)
                    {
                        message.AppendLine($"Найдено {count} пересечений штриховок с границей ГПЗУ");
                        regionsToDraw.AddRange(plotRegionResult.Item2);
                        regionsToDraw.AddRange(plotRegionResultRoof.Item2);
                    }
                    count = workRegionResult.Item1.Count() + workRegionResultRoof.Item1.Count();
                    if (count > 0)
                    {
                        message.AppendLine($"Найдено {count} пересечений полилиний с границей благоустройства");
                        linesToDraw.AddRange(workRegionResult.Item1);
                        linesToDraw.AddRange(workRegionResultRoof.Item1);
                    }
                    count = workRegionResult.Item2.Count() + workRegionResultRoof.Item2.Count();
                    if (count > 0)
                    {
                        message.AppendLine($"Найдено {count} пересечений штриховок с границей благоустройства");
                        regionsToDraw.AddRange(workRegionResult.Item2);
                        regionsToDraw.AddRange(workRegionResultRoof.Item2);
                    }
                    count = buildingRegionResult.Item1.Count();
                    if (count > 0)
                    {
                        message.AppendLine($"Найдено {count} пересечений полилиний с границей здания");
                        linesToDraw.AddRange(buildingRegionResult.Item1);
                    }
                    count = buildingRegionResult.Item2.Count();
                    if (count > 0)
                    {
                        message.AppendLine($"Найдено {count} пересечений штриховок с границей здания");
                        regionsToDraw.AddRange(buildingRegionResult.Item2);
                    }
                    count = buildingRegionResultRoof.Item1.Count();
                    if (count > 0)
                    {
                        message.AppendLine($"Найдено {count} пересечений полилиний с границей кровли");
                        linesToDraw.AddRange(buildingRegionResultRoof.Item1);
                    }
                    count = buildingRegionResultRoof.Item2.Count();
                    if (count > 0)
                    {
                        message.AppendLine($"Найдено {count} пересечений штриховок с границей кровли");
                        regionsToDraw.AddRange(buildingRegionResultRoof.Item2);
                    }
                    count = kindergartenRegionResult.Item1.Count() + kindergartenRegionResultRoof.Item1.Count();
                    if (count > 0)
                    {
                        message.AppendLine($"Найдено {count} пересечений полилиний с границей дет.сада");
                        linesToDraw.AddRange(kindergartenRegionResult.Item1);
                        linesToDraw.AddRange(kindergartenRegionResultRoof.Item1);
                    }
                    count = kindergartenRegionResult.Item2.Count() + kindergartenRegionResultRoof.Item2.Count();
                    if (count > 0)
                    {
                        message.AppendLine($"Найдено {count} пересечений штриховок с границей дет.сада");
                        regionsToDraw.AddRange(kindergartenRegionResult.Item2);
                        regionsToDraw.AddRange(kindergartenRegionResultRoof.Item2);
                    }
                    //Drawing errors
                    var bT = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bT[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    _dataExport.LayerCheck(tr, variables.TempLayer, Color.FromColorIndex(ColorMethod.ByAci, variables.TempLayerColor), variables.TempLayerLineWeight, variables.TempLayerPrintable);
                    _dataExport.CreateTempLine(variables, tr, linesToDraw);
                    foreach (var item in regionsToDraw)
                    {
                        item.Layer = variables.TempLayer;
                        btr.AppendEntity(item);
                        tr.AddNewlyCreatedDBObject(item, true);
                    }
                    var reportMessage = message.Length == 0 ? "Пересечений не найдено" : message.ToString();
                    System.Windows.MessageBox.Show(reportMessage, "Сообщение", System.Windows.MessageBoxButton.OK);
                    tr.Commit();
                }
            }
        }
        private static void FlattenPolylines(ref List<Polyline>[] polylines)
        {
            foreach (var pls in polylines)
            {
                if (pls != null)
                {
                    foreach (var pl in pls)
                    {
                        if (pl.Elevation != 0)
                        {
                            pl.UpgradeOpen();
                            pl.Elevation = 0;
                        }
                    }
                }
            }
        }
        private List<Region> GenerateRegionsFromBorders(Transaction tr, string layer, string borderName, string plotNumber = null, string plotXref = null)
        {
            var borderLayer = plotNumber != null ? layer + plotNumber.Replace(':', '_') : layer;
            List<Polyline> plotBorders = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, borderLayer, plotXref);
            foreach (var item in plotBorders)
            {
                if (!item.Closed)
                {
                    System.Windows.MessageBox.Show($"Границы {borderName} должны быть замкнутыми, проверьте что все полилиниии границы замкнуты в свойствах", "Сообщение", System.Windows.MessageBoxButton.OK);
                    return null;
                }
            }
            if (plotBorders.Count != 0)
            {
                return CreateRegionsWithHoleSupport(plotBorders, borderName);
            }
            else
            {
                System.Windows.MessageBox.Show($"На слое границ {borderName} нет полилиний", "Сообщение", System.Windows.MessageBoxButton.OK);
                return null;
            }
        }
        private MPolygon GenerateMPolygonFromBorders(Transaction tr, string layer, string borderName, string plotNumber = null, string plotXref = null)
        {
            var borderLayer = plotNumber != null ? layer + plotNumber.Replace(':', '_') : layer;
            List<Polyline> plotBorders = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, borderLayer, plotXref);
            foreach (var item in plotBorders)
            {
                if (!item.Closed)
                {
                    System.Windows.MessageBox.Show($"Границы {borderName} должны быть замкнутыми, проверьте что все полилиниии границы замкнуты в свойствах", "Сообщение", System.Windows.MessageBoxButton.OK);
                    return null;
                }
            }
            if (plotBorders.Count != 0)
            {
                MPolygon mPolygon = new();
                ObjectIdCollection plotBorderIds = new(plotBorders.Select(x => x.ObjectId).ToArray());
                mPolygon.CreateLoopsFromBoundaries(plotBorderIds, true, Tolerance.Global.EqualPoint);
                return mPolygon;
            }
            else
            {
                //System.Windows.MessageBox.Show($"На слое границ {borderName} нет полилиний", "Сообщение", System.Windows.MessageBoxButton.OK);
                return null;
            }
        }
        private (List<(Point3d, Point3d)>, List<Region>) CheckHatchesAndPolylinesForIntersectionsWithRegions(Variables variables, List<Region> plotRegions, MPolygon plotMPolygon, List<Hatch>[] hatches, List<Polyline>[] polylinesForLines, string borderName, double tolerance = 0)
        {
            List<(Point3d, Point3d)> errorLinePoints = new();
            List<Region> errorRegions = new();
            //Checking hatches
            for (var i = 0; i < hatches.Length; i++)
            {

                var reg = CreateRegionsFromHatches(hatches[i], out _);
                foreach (var r in reg)
                {
                    var rOriginal = (Region)r.Clone();
                    try
                    {
                        foreach (var plReg in plotRegions)
                        {
                            r.BooleanOperation(BooleanOperationType.BoolSubtract, (Region)plReg.Clone());
                        }
                        if (r.Area != 0 && rOriginal.Area - r.Area > tolerance)
                        {
                            errorRegions.Add(r);
                        }
                    }
                    catch (System.Exception)
                    {
                        errorRegions.Add(rOriginal);
                        System.Windows.MessageBox.Show($"При проверке пересечений с границей {borderName} была получена ошибка при попытке проверки штриховок на слое {variables.LaylistHatch[i]}, штриховка имеет площадь {rOriginal.Area}. Штриховка выделена полностью зелёным цветом, просьба найти и перестроить.", "Сообщение", System.Windows.MessageBoxButton.OK);
                    }
                }
            }
            //Checking polylines

            for (var i = 0; i < polylinesForLines.Length; i++)
            {
                foreach (var pl in polylinesForLines[i])
                {
                    if (ArePointsOnBothSidesOfBorder(GetPointsFromObject<Polyline>(pl), plotMPolygon) is var res && res != null)
                    {
                        errorLinePoints.Add(((Point3d, Point3d))res);
                    }
                }
            }
            //Results
            return (errorLinePoints, errorRegions);
        }

        private List<Polyline>[] GetAllPolylinesToCheck(Variables variables, Transaction tr)
        {
            List<Polyline>[] polylinesForLines = new List<Polyline>[variables.LaylistPlL.Length];
            for (var i = 0; i < variables.LaylistPlL.Length; i++)
            {
                polylinesForLines[i] = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlL[i]);
            }
            return polylinesForLines;
        }
        private List<Polyline>[] GetAllPolylinesToCheckOnRoof(Variables variables, Transaction tr)
        {
            List<Polyline>[] polylinesForLinesOnRoof = new List<Polyline>[variables.LaylistPlLOnRoof.Length];
            for (var i = 0; i < variables.LaylistPlLOnRoof.Length; i++)
            {
                polylinesForLinesOnRoof[i] = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlLOnRoof[i]);
            }

            return polylinesForLinesOnRoof;
        }

        private List<Hatch>[] GetAllHatchesToCheck(Variables variables, Transaction tr)
        {
            var hatches = GetHatchesToCheck(variables, tr);
            var hatchesOnRoof = GetHatchesToCheckOnRoof(variables, tr);
            var allHatches = hatches.Concat(hatchesOnRoof).ToArray();
            return allHatches;
        }
        private List<Hatch>[] GetHatchesToCheck(Variables variables, Transaction tr)
        {
            var hatches = new List<Hatch>[variables.LaylistHatch.Length + variables.LaylistHatchKindergarten.Length];
            int currentId = 0;
            for (var i = 0; i < variables.LaylistHatch.Length; i++)
            {
                hatches[i + currentId] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatch[i]);
            }
            currentId += variables.LaylistHatch.Length;
            for (var i = 0; i < variables.LaylistHatchKindergarten.Length; i++)
            {
                hatches[i + currentId] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchKindergarten[i]);
            }
            return hatches;
        }
        private List<Hatch>[] GetHatchesToCheckOnRoof(Variables variables, Transaction tr)
        {
            var hatches = new List<Hatch>[variables.LaylistHatchRoof.Length + variables.LaylistHatchKindergartenOnRoof.Length];
            int currentId = 0;
            for (var i = 0; i < variables.LaylistHatchRoof.Length; i++)
            {
                hatches[i + currentId] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchRoof[i]);
            }
            currentId += variables.LaylistHatchRoof.Length;
            for (var i = 0; i < variables.LaylistHatchKindergartenOnRoof.Length; i++)
            {
                hatches[i + currentId] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchKindergartenOnRoof[i]);
            }
            return hatches;
        }
        public void LabelPavements(Variables variables, string xRef)
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        List<Hatch>[] hatches = new List<Hatch>[variables.PLabelValues.Length];
                        int currentNumber = 0;
                        for (var i = 0; i < variables.PlabelNumber; i++)
                        {
                            hatches[i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatch[i], xRef);
                        }
                        currentNumber += variables.PlabelNumber;
                        hatches[currentNumber] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatch[13], xRef);
                        currentNumber++;
                        for (var i = 0; i < variables.PlabelRoofNumber; i++)
                        {
                            hatches[currentNumber + i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchRoof[i], xRef);
                        }
                        currentNumber += variables.PlabelRoofNumber;
                        hatches[currentNumber] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchRoof[11], xRef);
                        currentNumber++;
                        for (var i = 0; i < variables.LaylistHatchKindergarten.Length; i++)
                        {
                            hatches[currentNumber + i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchKindergarten[i], xRef);
                        }
                        currentNumber += variables.LaylistHatchKindergarten.Length;
                        for (var i = 0; i < variables.LaylistHatchKindergartenOnRoof.Length; i++)
                        {
                            hatches[currentNumber + i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchKindergartenOnRoof[i], xRef);
                        }
                        currentNumber += variables.LaylistHatchKindergartenOnRoof.Length;
                        for (var i = 0; i < variables.LaylistHatchAdditional.Length; i++)
                        {
                            hatches[currentNumber + i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchAdditional[i], xRef);
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
                        var result = _dataExport.CreateMleaderWithText(tr, texts, pts, variables.PLabelLayer);
                        if (result != "ok")
                        {
                            System.Windows.MessageBox.Show(result, "Error", System.Windows.MessageBoxButton.OK);
                        }
                    }
                    catch (System.Exception e)
                    {
                        System.Windows.MessageBox.Show(e.Message, "Error", System.Windows.MessageBoxButton.OK);
                    }
                    tr.Commit();
                }
            }
        }
        public void LabelGreenery(Variables variables)
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    for (int i = 0; i < variables.GreeneryGroupingDistance.Length; i++)
                    {
                        var greeneryBlocks = GroupBlocksByDistance(_dataImport.GetBlocksPosition(tr, variables.LaylistBlockCount[i]), variables.GreeneryGroupingDistance[i]);
                        // TODO: Add better grouping mechanism
                        var result = _dataExport.CreateMleaderWithBlockForGroupOfobjects(tr, greeneryBlocks, variables.GreeneryId[i], variables.GreeneryMleaderStyleName, variables.OLabelLayer, variables.GreeneryMleaderBlockName, variables.GreeneryAttr);
                        if (result != "ok")
                        {
                            System.Windows.MessageBox.Show(result, "Error", System.Windows.MessageBoxButton.OK);
                        }
                    }
                    tr.Commit();
                }
            }
        }
        //Function to group elements based on distance between 2 of them
        private List<List<Point3d>> GroupBlocksByDistance(List<Point3d> Points, double Distance)
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
        public void CalculateVolumes(Variables variables, string xRef, string plotXref, string plotNumber)
        {
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var plotBorders = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.PlotLayer + plotNumber.Replace(':', '_'), plotXref);
                    if (plotBorders.Count == 0)
                    {
                        return;
                    }
                    var plotRegions = CreateRegionsWithHoleSupport(plotBorders, "ГПЗУ");
                    var plotPolygon = GenerateMPolygonFromBorders(tr, variables.PlotLayer, "ГПЗУ", plotNumber, plotXref);

                    CalculateAndFillHatchTable(variables, tr, xRef, plotRegions);

                    CalculateAndFillPolylineLengthTable(variables, tr, xRef, plotRegions, plotPolygon);

                    CalculateAndFillPolylineAreaTable(variables, tr, xRef, plotRegions);

                    CalculateAndFillNormalBlocksTable(variables, tr, plotPolygon, xRef);

                    CalculateAndFillParamBlocksTable(variables, tr, plotPolygon);

                    tr.Commit();
                }
            }
        }
        private void CalculateAndFillHatchTable(Variables variables, Transaction tr, string xRef, List<Region> plotRegions)
        {
            try
            {
                //Getting data for Hatch table
                var hatchModelList = GetHatchData(variables, tr, xRef, plotRegions);
                //Filling hatch table
                int tableLength = variables.LaylistHatch.Length + variables.LaylistHatchRoof.Length + variables.LaylistHatchKindergarten.Length + variables.LaylistHatchKindergartenOnRoof.Length + variables.LaylistHatchAdditional.Length + 14;
                _dataExport.FillTableWithData(tr, hatchModelList, variables.Th, tableLength, "0.##");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
            }
        }

        private List<DataElementModel> GetHatchData(Variables variables, Transaction tr, string xRef, List<Region> plotRegions)
        {
            List<DataElementModel> hatchModelList = new();
            //getting hatches
            List<Hatch>[] hatches = new List<Hatch>[variables.LaylistHatch.Length];
            for (var i = 0; i < variables.LaylistHatch.Length; i++)
            {
                hatches[i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatch[i], xRef);
            }
            List<Hatch>[] additionalHatches = new List<Hatch>[variables.LaylistHatchAdditional.Length];
            for (var i = 0; i < variables.LaylistHatchAdditional.Length; i++)
            {
                additionalHatches[i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchAdditional[i], xRef);
            }
            List<Hatch>[] roofHatches = new List<Hatch>[variables.LaylistHatchRoof.Length];
            for (var i = 0; i < variables.LaylistHatchRoof.Length; i++)
            {
                roofHatches[i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchRoof[i], xRef);
            }
            List<Hatch>[] kindergartenHatches = new List<Hatch>[variables.LaylistHatchKindergarten.Length];
            for (var i = 0; i < variables.LaylistHatchKindergarten.Length; i++)
            {
                kindergartenHatches[i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchKindergarten[i], xRef);
            }
            List<Hatch>[] kindergartenHatchesOnRoof = new List<Hatch>[variables.LaylistHatchKindergartenOnRoof.Length];
            for (var i = 0; i < variables.LaylistHatchKindergartenOnRoof.Length; i++)
            {
                kindergartenHatchesOnRoof[i] = _dataImport.GetAllElementsOfTypeOnLayer<Hatch>(tr, variables.LaylistHatchKindergartenOnRoof[i], xRef);
            }
            //splitting hatches between inside kindergarten and outside
            var kindergartenPlot = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlA[2], xRef); ;
            List<Hatch>[] pavementHatchesInKindergarten = new List<Hatch>[4];
            List<Hatch>[] greeneryHatchesInKindergarten = new List<Hatch>[3];
            List<Hatch>[] pavementHatchesInKindergartenOnRoof = new List<Hatch>[4];
            List<Hatch>[] greeneryHatchesInKindergartenOnRoof = new List<Hatch>[3];
            if (kindergartenPlot.Count != 0)
            {
                var kindergartenRegion = CreateRegionsWithHoleSupport(kindergartenPlot, "детского сада");
                for (int i = 0; i < 5; i++)
                {
                    if (i == 1)
                        continue;
                    int currentNumber = i > 1 ? i - 1 : i;
                    pavementHatchesInKindergarten[currentNumber] = GetListOfElementsThatAreInsideRegion<Hatch>(kindergartenRegion, hatches[i]);
                    foreach (var item in pavementHatchesInKindergarten[currentNumber])
                    {
                        hatches[i].Remove(item);
                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    greeneryHatchesInKindergarten[i] = GetListOfElementsThatAreInsideRegion<Hatch>(kindergartenRegion, hatches[i + 10]);
                    foreach (var item in greeneryHatchesInKindergarten[i])
                    {
                        hatches[i + 10].Remove(item);
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    pavementHatchesInKindergartenOnRoof[i] = GetListOfElementsThatAreInsideRegion<Hatch>(kindergartenRegion, roofHatches[i]);
                    foreach (var item in pavementHatchesInKindergartenOnRoof[i])
                    {
                        roofHatches[i].Remove(item);
                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    greeneryHatchesInKindergartenOnRoof[i] = GetListOfElementsThatAreInsideRegion<Hatch>(kindergartenRegion, roofHatches[i + 8]);
                    foreach (var item in greeneryHatchesInKindergartenOnRoof[i])
                    {
                        roofHatches[i + 8].Remove(item);
                    }
                }
            }

            //creating data for table
            int currentLine = 0;
            hatchModelList.AddRange(CreateDataElementsHatches(tr, plotRegions, hatches, currentLine));
            currentLine += hatches.Length;
            hatchModelList.AddRange(CreateDataElementsHatches(tr, plotRegions, pavementHatchesInKindergarten, currentLine));
            currentLine += pavementHatchesInKindergarten.Length;
            hatchModelList.AddRange(CreateDataElementsHatches(tr, plotRegions, kindergartenHatches, currentLine));
            currentLine += kindergartenHatches.Length;
            hatchModelList.AddRange(CreateDataElementsHatches(tr, plotRegions, greeneryHatchesInKindergarten, currentLine));
            currentLine += greeneryHatchesInKindergarten.Length;
            hatchModelList.AddRange(CreateDataElementsHatches(tr, plotRegions, roofHatches, currentLine));
            currentLine += roofHatches.Length;
            hatchModelList.AddRange(CreateDataElementsHatches(tr, plotRegions, pavementHatchesInKindergartenOnRoof, currentLine));
            currentLine += pavementHatchesInKindergartenOnRoof.Length;
            hatchModelList.AddRange(CreateDataElementsHatches(tr, plotRegions, kindergartenHatchesOnRoof, currentLine));
            currentLine += kindergartenHatchesOnRoof.Length;
            hatchModelList.AddRange(CreateDataElementsHatches(tr, plotRegions, greeneryHatchesInKindergartenOnRoof, currentLine));
            currentLine += greeneryHatchesInKindergartenOnRoof.Length;
            hatchModelList.AddRange(CreateDataElementsHatches(tr, plotRegions, additionalHatches, currentLine));
            return hatchModelList;
        }

        private List<DataElementModel> CreateDataElementsHatches(Transaction tr, List<Region> plotRegions, List<Hatch>[] hatches, int startingNumber)
        {
            List<DataElementModel> hatchModelList = new();
            for (var i = 0; i < hatches.Length; i++)
            {
                if (hatches[i] != null)
                {
                    var quantity = GetHatchArea(tr, hatches[i]);
                    var areHatchesInside = AreObjectsInsidePlot(plotRegions, hatches[i]);

                    for (var j = 0; j < hatches[i].Count; j++)
                    {
                        hatchModelList.Add(new DataElementModel(quantity[j], startingNumber + i, areHatchesInside[j]));
                    }
                }
            }
            return hatchModelList;
        }

        private List<T> GetListOfElementsThatAreInsideRegion<T>(List<Region> region, List<T> elements)
        {
            List<T> elementsInside = new();
            var areHatchesInside = AreObjectsInsidePlot<T>(region, elements);
            for (var i = 0; i < elements.Count; i++)
            {
                if (areHatchesInside[i])
                {
                    elementsInside.Add(elements[i]);
                }
            }
            return elementsInside;
        }
        private List<T> GetListOfElementsThatAreInsideRegion<T>(MPolygon polygon, List<T> elements)
        {
            List<T> elementsInside = new();
            var areHatchesInside = AreObjectsInsidePlot<T>(polygon, elements);
            for (var i = 0; i < elements.Count; i++)
            {
                if (areHatchesInside[i])
                {
                    elementsInside.Add(elements[i]);
                }
            }
            return elementsInside;
        }

        private void CalculateAndFillParamBlocksTable(Variables variables, Transaction tr, MPolygon plotPolygon)
        {
            try
            {
                //Counting blocks with parameters
                List<DataElementModel> paramBlocksModelList = new();
                List<BlockReference>[] blocksWithParams = new List<BlockReference>[variables.LaylistBlockWithParams.Length];
                for (var i = 0; i < variables.LaylistBlockWithParams.Length; i++)
                {
                    blocksWithParams[i] = _dataImport.GetAllElementsOfTypeOnLayer<BlockReference>(tr, variables.LaylistBlockWithParams[i]);
                }
                var paramTableRow = 0;
                for (var i = 0; i < variables.LaylistBlockWithParams.Length; i++)
                {
                    var areBlocksInside = AreObjectsInsidePlot<BlockReference>(plotPolygon, blocksWithParams[i]);
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
        private void CalculateAndFillNormalBlocksTable(Variables variables, Transaction tr, MPolygon plotPolygon, string xRef)
        {
            try
            {
                //Counting blocks including those in arrays
                List<DataElementModel> normalBlocksModelList = new();

                List<List<bool>> normalAreBlocksInside = new();
                List<List<bool>> normalAreBlocksInsideOnRoof = new();
                List<List<bool>> kindergartenAreBlocksInside = new();
                List<List<bool>> kindergartenAreBlocksInsideOnRoof = new();

                var kindergartenBorder = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlA[2], xRef);
                MPolygon kindergartenPolygon = new();
                if (kindergartenBorder != null && kindergartenBorder.Count > 0)
                {
                    kindergartenPolygon = GenerateMPolygonFromBorders(tr, variables.LaylistPlA[2], "Детского сада", null, xRef);
                }
                var buildingBorder = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlA[1], xRef);
                var buildingPolygon = GenerateMPolygonFromBorders(tr, variables.LaylistPlA[1], "Здания", null, xRef);
                for (int i = 0; i < variables.LaylistBlockCount.Length; i++)
                {
                    var blockPositions = _dataImport.GetBlocksPosition(tr, variables.LaylistBlockCount[i]);

                    var areBlocksOnRoof = AreObjectsInsidePlot(buildingPolygon, blockPositions);
                    List<Point3d> blocksOnRoof = new();
                    List<Point3d> blocksNotOnRoof = new();
                    for (var j = 0; j < areBlocksOnRoof.Count; j++)
                    {
                        if (areBlocksOnRoof[j])
                        {
                            blocksOnRoof.Add(blockPositions[j]);
                        }
                        else
                        {
                            blocksNotOnRoof.Add(blockPositions[j]);
                        }
                    }
                    if (kindergartenBorder != null && kindergartenBorder.Count > 0)
                    {
                        List<Point3d> blocksOutsideKindergartenOnRoof = new();
                        List<Point3d> blocksOutsideKindergartenNotOnRoof = new();
                        List<Point3d> blocksInsideKindergartenOnRoof = new();
                        List<Point3d> blocksInsideKindergartenNotOnRoof = new();

                        var areBlocksInsideKindergarten = AreObjectsInsidePlot(kindergartenPolygon, blocksNotOnRoof);
                        for (var j = 0; j < areBlocksInsideKindergarten.Count; j++)
                        {
                            if (areBlocksInsideKindergarten[j])
                            {
                                blocksInsideKindergartenNotOnRoof.Add(blocksNotOnRoof[j]);
                            }
                            else
                            {
                                blocksOutsideKindergartenNotOnRoof.Add(blocksNotOnRoof[j]);
                            }
                        }
                        var areBlocksInsideKindergartenOnRoof = AreObjectsInsidePlot(kindergartenPolygon, blocksOnRoof);
                        for (var j = 0; j < blocksOnRoof.Count; j++)
                        {
                            if (areBlocksInsideKindergartenOnRoof[j])
                            {
                                blocksInsideKindergartenOnRoof.Add(blocksOnRoof[j]);
                            }
                            else
                            {
                                blocksOutsideKindergartenOnRoof.Add(blocksOnRoof[j]);
                            }
                        }
                        normalAreBlocksInside.Add(AreObjectsInsidePlot(plotPolygon, blocksOutsideKindergartenNotOnRoof));
                        kindergartenAreBlocksInside.Add(AreObjectsInsidePlot(plotPolygon, blocksInsideKindergartenNotOnRoof));
                        normalAreBlocksInsideOnRoof.Add(AreObjectsInsidePlot(plotPolygon, blocksOutsideKindergartenOnRoof));
                        kindergartenAreBlocksInsideOnRoof.Add(AreObjectsInsidePlot(plotPolygon, blocksInsideKindergartenOnRoof));
                    }
                    else
                    {
                        normalAreBlocksInside.Add(AreObjectsInsidePlot(plotPolygon, blocksNotOnRoof));
                        normalAreBlocksInsideOnRoof.Add(AreObjectsInsidePlot(plotPolygon, blocksOnRoof));
                    }
                }
                //Creating table data
                for (var i = 0; i < variables.LaylistBlockCount.Length; i++)
                {
                    normalBlocksModelList.Add(new DataElementModel(normalAreBlocksInside[i].Where(x => x == true).Count(), i, true));
                    normalBlocksModelList.Add(new DataElementModel(normalAreBlocksInside[i].Where(x => x == false).Count(), i, false));
                    normalBlocksModelList.Add(new DataElementModel(normalAreBlocksInsideOnRoof[i].Where(x => x == true).Count(), i + 4, true));
                    normalBlocksModelList.Add(new DataElementModel(normalAreBlocksInsideOnRoof[i].Where(x => x == false).Count(), i + 4, false));
                    if (kindergartenBorder != null && kindergartenBorder.Count > 0)
                    {
                        normalBlocksModelList.Add(new DataElementModel(kindergartenAreBlocksInside[i].Where(x => x == true).Count(), i + 2, true));
                        normalBlocksModelList.Add(new DataElementModel(kindergartenAreBlocksInside[i].Where(x => x == false).Count(), i + 2, false));
                        normalBlocksModelList.Add(new DataElementModel(kindergartenAreBlocksInsideOnRoof[i].Where(x => x == true).Count(), i + 6, true));
                        normalBlocksModelList.Add(new DataElementModel(kindergartenAreBlocksInsideOnRoof[i].Where(x => x == false).Count(), i + 6, false));
                    }
                }
                //Filling Normal blocks table
                _dataExport.FillTableWithData(tr, normalBlocksModelList, variables.Tbn, variables.LaylistBlockCount.Length * 4, "0");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
            }
        }
        private void CalculateAndFillPolylineAreaTable(Variables variables, Transaction tr, string xRef, List<Region> plotRegions)
        {
            try
            {
                List<DataElementModel> plineAreaModelList = new();
                List<Polyline>[] polylinesForAreas = new List<Polyline>[variables.LaylistPlA.Length];
                for (var i = 0; i < variables.LaylistPlA.Length; i++)
                {
                    polylinesForAreas[i] = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlA[i], xRef);
                }
                //Splitting working zone between in and out GPZU
                //Creating region
                var workingRegions = CreateRegionsWithHoleSupport(polylinesForAreas[0], "благоустройства");
                plineAreaModelList.AddRange(SplitRegionByInsideOutsideAndCreateDataElement(plotRegions, workingRegions, 0));

                var buildingRegions = CreateRegionsWithHoleSupport(polylinesForAreas[1], "здания");
                plineAreaModelList.AddRange(SplitRegionByInsideOutsideAndCreateDataElement(plotRegions, buildingRegions, 1));
                //Splitting Kindergarten between roof and not roof
                if (polylinesForAreas[2].Count == 1)
                {
                    var kindergartenRegions = CreateRegionsWithHoleSupport(polylinesForAreas[2], "детского сада");
                    var kindergartenRegionOnRoof = (Region)kindergartenRegions[0].Clone();

                    foreach (var region in buildingRegions)
                    {
                        kindergartenRegions[0].BooleanOperation(BooleanOperationType.BoolSubtract, (Region)region.Clone());
                    }

                    plineAreaModelList.AddRange(SplitRegionByInsideOutsideAndCreateDataElement(plotRegions, new List<Region> { kindergartenRegions[0] }, 2));

                    kindergartenRegionOnRoof.BooleanOperation(BooleanOperationType.BoolSubtract, (Region)kindergartenRegions[0].Clone());

                    plineAreaModelList.AddRange(SplitRegionByInsideOutsideAndCreateDataElement(plotRegions, new List<Region> { kindergartenRegionOnRoof }, 3));
                }
                else if (polylinesForAreas[2].Count > 1)
                {
                    System.Windows.MessageBox.Show("На слое границы садика больше одной полилинии, что недопустимо", "Error", System.Windows.MessageBoxButton.OK);
                    return;
                }


                for (int i = 3; i < variables.LaylistPlA.Length; i++)
                {
                    var arePlinesInside = AreObjectsInsidePlot<Polyline>(plotRegions, polylinesForAreas[i]);
                    for (int j = 0; j < polylinesForAreas[i].Count; j++)
                    {
                        object pl = polylinesForAreas[i][j];
                        var plineArea = (double)pl.GetType().InvokeMember("Area", BindingFlags.GetProperty, null, pl, null);
                        plineAreaModelList.Add(new DataElementModel(plineArea, i + 1, arePlinesInside[j]));
                    }
                }
                //Filling Polyline Area table
                _dataExport.FillTableWithData(tr, plineAreaModelList, variables.Tpa, variables.LaylistPlA.Length + 1, "0.##");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
            }
        }

        private List<DataElementModel> SplitRegionByInsideOutsideAndCreateDataElement(List<Region> regionsToSplitBy, List<Region> regionsToSplit, int numberInTable)
        {
            List<DataElementModel> dataElementModelList = new();
            //Work with regions to determine what is outside GPZU
            double areaOutside = 0;
            double areaInside = 0;
            foreach (var workReg in regionsToSplit)
            {
                var tempReg = (Region)workReg.Clone();
                areaInside += workReg.Area;
                foreach (var item in regionsToSplitBy)
                {
                    tempReg.BooleanOperation(BooleanOperationType.BoolSubtract, (Region)item.Clone());
                }
                areaOutside += tempReg.Area;
            }
            areaInside -= areaOutside;

            dataElementModelList.Add(new DataElementModel(areaInside, numberInTable, true));
            dataElementModelList.Add(new DataElementModel(areaOutside, numberInTable, false));
            return dataElementModelList;
        }

        private void CalculateAndFillPolylineLengthTable(Variables variables, Transaction tr, string xRef, List<Region> plotRegions, MPolygon plotPolygon)
        {
            try
            {
                List<DataElementModel> plineLengthModelList = new();
                List<Polyline>[] polylinesForLines = new List<Polyline>[variables.LaylistPlL.Length];
                for (var i = 0; i < variables.LaylistPlL.Length; i++)
                {
                    polylinesForLines[i] = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlL[i], xRef);
                }
                FlattenPolylines(ref polylinesForLines);
                List<Polyline>[] polylinesForLinesOnRoof = new List<Polyline>[variables.LaylistPlLOnRoof.Length];
                for (var i = 0; i < variables.LaylistPlLOnRoof.Length; i++)
                {
                    polylinesForLinesOnRoof[i] = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlLOnRoof[i], xRef);
                }
                FlattenPolylines(ref polylinesForLinesOnRoof);
                //seperating kindergarten part
                var kindergartenBorders = _dataImport.GetAllElementsOfTypeOnLayer<Polyline>(tr, variables.LaylistPlA[2], xRef);
                List<Polyline>[] polylinesForLinesKindergarten = new List<Polyline>[variables.LaylistPlL.Length];
                List<Polyline>[] polylinesForLinesKindergartenOnRoof = new List<Polyline>[variables.LaylistPlL.Length];
                if (kindergartenBorders != null && kindergartenBorders.Count > 0)
                {
                    var kindergartenRegion = GenerateMPolygonFromBorders(tr, variables.LaylistPlA[2], "Детского сада", null, xRef);
                    for (int i = 0; i < polylinesForLines.Length; i++)
                    {
                        polylinesForLinesKindergarten[i] = GetListOfElementsThatAreInsideRegion<Polyline>(kindergartenRegion, polylinesForLines[i]);
                        foreach (var polyline in polylinesForLinesKindergarten[i])
                        {
                            polylinesForLines[i].Remove(polyline);
                        }
                    }
                    for (int i = 0; i < polylinesForLinesOnRoof.Length; i++)
                    {
                        polylinesForLinesKindergartenOnRoof[i] = GetListOfElementsThatAreInsideRegion<Polyline>(kindergartenRegion, polylinesForLinesOnRoof[i]);
                        foreach (var polyline in polylinesForLinesKindergartenOnRoof[i])
                        {
                            polylinesForLinesOnRoof[i].Remove(polyline);
                        }
                    }
                }
                //Creating dataelement Models
                int lineCounter = 0;
                plineLengthModelList.AddRange(CreateElementsModelListPolylines(variables, plotRegions, polylinesForLines, lineCounter, plotPolygon));
                lineCounter += polylinesForLines.Length;
                plineLengthModelList.AddRange(CreateElementsModelListPolylines(variables, plotRegions, polylinesForLinesKindergarten, lineCounter, plotPolygon));
                lineCounter += polylinesForLines.Length;
                plineLengthModelList.AddRange(CreateElementsModelListPolylines(variables, plotRegions, polylinesForLinesOnRoof, lineCounter, plotPolygon));
                lineCounter += polylinesForLines.Length;
                plineLengthModelList.AddRange(CreateElementsModelListPolylines(variables, plotRegions, polylinesForLinesKindergartenOnRoof, lineCounter, plotPolygon));
                //Filling Polyline length table
                _dataExport.FillTableWithData(tr, plineLengthModelList, variables.Tpl, lineCounter + polylinesForLines.Length, "0");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK);
            }
        }

        private List<DataElementModel> CreateElementsModelListPolylines(Variables variables, List<Region> plotRegions, List<Polyline>[] polylines, int startingLine, MPolygon plotPolygon)
        {
            List<DataElementModel> plineLengthModelList = new();
            for (var i = 0; i < variables.LaylistPlL.Length; i++)
            {
                if (polylines[i] != null && polylines[i].Count > 0)
                {
                    var plineLengths = polylines[i].Select(x => x.Length / variables.CurbLineCount[i]).ToList();
                    var arePlinesInside = AreObjectsInsidePlot<Polyline>(plotPolygon, polylines[i]);
                    for (var j = 0; j < polylines[i].Count; j++)
                    {
                        plineLengthModelList.Add(new DataElementModel(plineLengths[j], i + startingLine, arePlinesInside[j]));
                    }
                }
            }
            return plineLengthModelList;
        }

        //creating regions from polylines with hole support
        private List<Region> CreateRegionsWithHoleSupport(List<Polyline> polylines, string text)
        {
            List<Region> workingRegions = new();
            if (polylines.Count == 1)
            {
                workingRegions.Add(RegionFromClosedCurve(polylines[0]));
            }
            else if (polylines.Count == 0)
            {
                System.Windows.MessageBox.Show($"В файле основы нет границы {text}", "Error", System.Windows.MessageBoxButton.OK);
            }
            else
            {
                //We need to find external borders and internal borders of working area and create a region for it correctly.
                List<List<bool>> isInsideAnother = new();
                for (int i = 0; i < polylines.Count; i++)
                {
                    isInsideAnother.Add(new List<bool>());
                    isInsideAnother[i] = AreObjectsInsidePlot(polylines[i], polylines, false);
                    isInsideAnother[i][i] = false;
                }
                Region[] baseRegions = new Region[isInsideAnother.Count];
                List<List<Region>> regionToSubstract = new();
                for (int i = 0; i < isInsideAnother.Count; i++)
                {
                    regionToSubstract.Add(new());
                }
                for (int i = 0; i < isInsideAnother.Count; i++)
                {
                    var numberOfPlinesInside = isInsideAnother[i].Where(x => x == true).ToList().Count;
                    if (numberOfPlinesInside == 0)
                    {
                        var isRegionInsideAnother = false;
                        for (int j = 0; j < isInsideAnother.Count; j++)
                        {
                            if (isInsideAnother[j][i])
                            {
                                isRegionInsideAnother = true;
                            }
                        }
                        if (!isRegionInsideAnother)
                        {
                            baseRegions[i] = CreateRegionFromPolyline(polylines[i]);
                        }
                    }
                    else
                    {
                        baseRegions[i] = CreateRegionFromPolyline(polylines[i]);
                        for (var j = 0; j < isInsideAnother[i].Count; j++)
                        {
                            if (isInsideAnother[i][j])
                            {
                                regionToSubstract[i].Add(CreateRegionFromPolyline(polylines[j]));
                            }
                        }
                    }
                }
                for (int i = 0; i < baseRegions.Length; i++)
                {
                    if (baseRegions[i] != null)
                    {
                        if (regionToSubstract[i].Count != 0)
                        {
                            foreach (var item in regionToSubstract[i])
                            {
                                baseRegions[i].BooleanOperation(BooleanOperationType.BoolSubtract, item);
                            }
                        }

                        if (baseRegions[i].Area != 0)
                        {
                            workingRegions.Add(baseRegions[i]);
                        }
                        else
                        {
                            /*
                            //Ideally we are throwing exeption and selecting polyline afterwards, but currently this method is used a lot
                            System.Exception e = new();
                            e.Data.Add("pl", polylines[i]);
                            throw e;
                            */
                            System.Windows.MessageBox.Show($"В файле дублируется одна из границ, необходимо исключить дублирующую границу", "Error", System.Windows.MessageBoxButton.OK);
                        }
                    }
                }
            }
            return workingRegions;
        }
        //Point Containment for checking out if point is inside plot or outside it
        private Region RegionFromClosedCurve(Curve curve)
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
        private PointContainment GetPointContainment(Curve curve, Point3d point)
        {
            if (!curve.Closed)
                throw new ArgumentException("Полилиния границы должна быть замкнута");
            Region region = RegionFromClosedCurve(curve);
            if (region == null)
                throw new InvalidOperationException("Ошибка, проверьте полилинию границы");
            using (region)
            { return GetPointContainment(region, point); }
        }
        private PointContainment GetPointContainment(Region region, Point3d point)
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
        private PointContainment GetPointContainment(MPolygon mPolygon, Point3d point)
        {
            if (mPolygon.NumMPolygonLoops > 1)
            {
                for (var i = 0; i < mPolygon.NumMPolygonLoops; i++)
                {
                    if (mPolygon.IsPointOnLoopBoundary(point, i, tolerance))
                    {
                        return PointContainment.OnBoundary;
                    }
                }
            }
            else
            {
                if (mPolygon.IsPointOnLoopBoundary(point, 0, tolerance))
                {
                    return PointContainment.OnBoundary;
                }
            }
            var inside = PointContainment.Outside;
            if (mPolygon.IsPointInsideMPolygon(point, Tolerance.Global.EqualPoint).Count > 0)
            {
                if (mPolygon.NumMPolygonLoops <= 1)
                    inside = PointContainment.Inside;
                else
                {
                    int inslooop = 0;
                    for (int i = 0; i < mPolygon.NumMPolygonLoops; i++)
                    {
                        using (MPolygon mp = new MPolygon())
                        {
                            mp.AppendMPolygonLoop(mPolygon.GetMPolygonLoopAt(i), false, tolerance);
                            if (mp.IsPointInsideMPolygon(point, tolerance).Count > 0) inslooop++;
                        }
                    }
                    if (inslooop % 2 > 0)
                        inside = PointContainment.Inside;
                }
            }
            return inside;

        }
        private PointContainment GetPointContainment(List<Region> regions, Point3d point)
        {
            List<PointContainment> resultsByRegion = new();
            foreach (var region in regions)
            {
                var result = PointContainment.Outside;
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
                resultsByRegion.Add(result);
            }
            if (resultsByRegion.Where(x => x == PointContainment.Inside).Count() > 0)
            {
                return PointContainment.Inside;
            }
            if (resultsByRegion.Where(x => x == PointContainment.OnBoundary).Count() > 0)
            {
                return PointContainment.OnBoundary;
            }
            return PointContainment.Outside;
        }
        //Getting hatch border or polyline points to check if it is inside plot or not
        private List<Point3d> GetPointsFromObject<T>(T obj)
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
        private bool ArePointsInsidePolyline(List<Point3d> points, List<Region> regions, bool isPolyline = false, bool checkForIntersections = false)
        {
            List<PointContainment> results = new();
            foreach (var pt in points)
            {
                results.Add(GetPointContainment(regions, pt));
            }
            if (results.Where(x => x == PointContainment.Inside).Count() > 0)
            {
                if (results.Where(x => x == PointContainment.Outside).Count() > 0)
                {
                    if (checkForIntersections)
                    {
                        throw new System.Exception("Одна из полилиний или штриховок пересекает одну из границ, необходимо исправить.");
                    }
                    else
                    {
                        return (results.Where(x => x == PointContainment.Inside).Count() - results.Where(x => x == PointContainment.Outside).Count()) > 0;
                    }
                }
                else
                {
                    return true;
                }
            }
            else if (results.Where(x => x == PointContainment.Outside).Count() > 0)
            {
                return false;
            }
            else
            {
                if (isPolyline)
                {
                    return true;
                }
                else
                {
                    double xCoord = 0;
                    double yCoord = 0;
                    int numberOfPoints = points.Count;
                    for (int i = 0; i < numberOfPoints; i++)
                    {
                        xCoord += points[i].X / numberOfPoints;
                        yCoord += points[i].Y / numberOfPoints;
                    }
                    var testPointIn = GetPointContainment(regions, new Point3d(xCoord, yCoord, 0));

                    return testPointIn == PointContainment.Inside;
                }
            }
        }
        private bool ArePointsInsidePolyline(List<Point3d> points, MPolygon mPolygon, bool isPolyline = false, bool checkForIntersections = false)
        {
            List<PointContainment> results = new();
            foreach (var pt in points)
            {
                results.Add(GetPointContainment(mPolygon, pt));
            }
            if (results.Where(x => x == PointContainment.Inside).Count() > 0)
            {
                if (results.Where(x => x == PointContainment.Outside).Count() > 0)
                {
                    if (checkForIntersections)
                    {
                        throw new System.Exception("Одна из полилиний или штриховок пересекает одну из границ, необходимо исправить.");
                    }
                    else
                    {
                        return (results.Where(x => x == PointContainment.Inside).Count() - results.Where(x => x == PointContainment.Outside).Count()) > 0;
                    }
                }
                else
                {
                    return true;
                }
            }
            else if (results.Where(x => x == PointContainment.Outside).Count() > 0)
            {
                return false;
            }
            else
            {
                if (isPolyline)
                {
                    return true;
                }
                else
                {
                    double xCoord = 0;
                    double yCoord = 0;
                    int numberOfPoints = points.Count;
                    for (int i = 0; i < numberOfPoints; i++)
                    {
                        xCoord += points[i].X / numberOfPoints;
                        yCoord += points[i].Y / numberOfPoints;
                    }
                    var testPointIn = GetPointContainment(mPolygon, new Point3d(xCoord, yCoord, 0));

                    return testPointIn == PointContainment.Inside;
                }
            }
        }
        //Getting points that are on other side of plotBorder
        private (Point3d, Point3d)? ArePointsOnBothSidesOfBorder(List<Point3d> points, List<Region> regions)
        {
            List<PointContainment> results = new();
            foreach (var pt in points)
            {
                results.Add(GetPointContainment(regions, pt));
            }
            if (results.Where(x => x == PointContainment.Outside).Count() > 0)
            {
                if (results.Where(x => x == PointContainment.Inside).Count() > 0)
                {
                    if (points.Count == 2)
                    {
                        return (points[0], points[1]);
                    }
                    else
                    {
                        PointContainment? sideFound = null;
                        for (int i = 0; i < results.Count; i++)
                        {
                            if (sideFound != null && results[i] != PointContainment.OnBoundary && results[i] != sideFound)
                            {
                                return (points[i], points[i - 1]);
                            }
                            if (sideFound == null && results[i] != PointContainment.OnBoundary)
                            {
                                sideFound = results[i];
                            }
                        }
                        return (points[results.Count - 2], points[results.Count - 1]);
                    }
                }
                else
                {
                    return null;
                }
            }
            return null;

        }
        private (Point3d, Point3d)? ArePointsOnBothSidesOfBorder(List<Point3d> points, MPolygon mPolygon)
        {
            List<PointContainment> results = new();
            foreach (var pt in points)
            {
                results.Add(GetPointContainment(mPolygon, pt));
            }
            if (results.Where(x => x == PointContainment.Outside).Count() > 0)
            {
                if (results.Where(x => x == PointContainment.Inside).Count() > 0)
                {
                    if (points.Count == 2)
                    {
                        return (points[0], points[1]);
                    }
                    else
                    {
                        PointContainment? sideFound = null;
                        for (int i = 0; i < results.Count; i++)
                        {
                            if (sideFound != null && results[i] != PointContainment.OnBoundary && results[i] != sideFound)
                            {
                                return (points[i], points[i - 1]);
                            }
                            if (sideFound == null && results[i] != PointContainment.OnBoundary)
                            {
                                sideFound = results[i];
                            }
                        }
                        return (points[results.Count - 2], points[results.Count - 1]);
                    }
                }
                else
                {
                    return null;
                }
            }
            return null;

        }
        //Function to check if hatch/polyline/blockreference is inside plot (can have 2+ borders)
        private List<bool> AreObjectsInsidePlot<T>(List<Region> plotRegions, List<T> objects, bool checkForIntersections = true)
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
                    if (ArePointsInsidePolyline(new List<Point3d>() { point }, plotRegions))
                    {
                        tempResult = true;
                    }
                }
                else if (item is Polyline)
                {
                    if (ArePointsInsidePolyline(GetPointsFromObject(item), plotRegions, true, checkForIntersections))
                    {
                        tempResult = true;
                    }
                }
                else
                {
                    if (ArePointsInsidePolyline(GetPointsFromObject(item), plotRegions, false, checkForIntersections))
                    {
                        tempResult = true;
                    }
                }
                results.Add(tempResult);
            }
            return results;
        }
        private List<bool> AreObjectsInsidePlot<T>(MPolygon plotPolygon, List<T> objects, bool checkForIntersections = true)
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
                    if (ArePointsInsidePolyline(new List<Point3d>() { point }, plotPolygon))
                    {
                        tempResult = true;
                    }
                }
                else if (item is Polyline)
                {
                    if (ArePointsInsidePolyline(GetPointsFromObject(item), plotPolygon, true, checkForIntersections))
                    {
                        tempResult = true;
                    }
                }
                else
                {
                    if (ArePointsInsidePolyline(GetPointsFromObject(item), plotPolygon, false, checkForIntersections))
                    {
                        tempResult = true;
                    }
                }
                results.Add(tempResult);
            }
            return results;
        }
        private List<bool> AreObjectsInsidePlot<T>(Polyline plot, List<T> objects, bool checkForIntersections)
        {
            if (objects == null)
            {
                return null;
            }
            List<bool> results = new();
            var plotRegions = new List<Region>();
            plotRegions.Add(RegionFromClosedCurve(plot));
            foreach (var item in objects)
            {
                var tempResult = false;
                if (item is Point3d point)
                {
                    if (ArePointsInsidePolyline(new List<Point3d>() { point }, plotRegions))
                    {
                        tempResult = true;
                    }
                }
                else if (item is Polyline)
                {
                    if (ArePointsInsidePolyline(GetPointsFromObject(item), plotRegions, true, checkForIntersections))
                    {
                        tempResult = true;
                    }
                }
                else
                {
                    if (ArePointsInsidePolyline(GetPointsFromObject(item), plotRegions, false))
                    {
                        tempResult = true;
                    }
                }
                results.Add(tempResult);
            }
            return results;
        }
        //Function that returns Polyline from HatchLoop
        private Polyline GetBorderFromHatchLoop(HatchLoop loop, Plane plane)
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
        private List<double> GetHatchArea(Transaction tr, List<Hatch> hatchList)
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
